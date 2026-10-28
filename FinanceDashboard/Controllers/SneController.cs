using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceDashboard.Controllers
{
    [Authorize(Roles = "SNEDepartmentUser,DepartmentUser,Administrator")]
    public class SneController : Controller
    {
        private readonly ISneRepository _sneRepository;
        private readonly ILogger<SneController> _logger;

        // Default financial year for the BM-II form: FY 2027-28
        private const int DefaultFinancialYear = 2027;

        public SneController(ISneRepository sneRepository, ILogger<SneController> logger)
        {
            _sneRepository = sneRepository;
            _logger = logger;
        }

        /// <summary>
        /// Department codes the current user is allowed to see/edit SNE data for: the union of
        /// their SNEDepartmentUser assignment (if any) and their DepartmentUser assignment (if any).
        /// This lets a pure DepartmentUser (reviewer, no SNEDepartmentUser role) access the SNE
        /// module for their own department, so they can review/edit/submit after the SNE user's
        /// initial submission.
        /// </summary>
        private static List<string>? GetEffectiveDepartments(ClaimsPrincipal user)
        {
            var sneDepartments = user.GetSneDepartmentCodes();
            var restrictedDepartments = user.GetRestrictedDepartments();

            if (sneDepartments == null && restrictedDepartments == null)
                return null;

            return (sneDepartments ?? new List<string>())
                .Concat(restrictedDepartments ?? new List<string>())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>
        /// True when the current user is allowed to perform the final department-level submission
        /// (DepartmentUser role or Administrator), as opposed to a plain SNEDepartmentUser whose
        /// submission is only the first stage (pending department review).
        /// </summary>
        private static bool IsDepartmentSubmitter(ClaimsPrincipal user)
        {
            return user.IsAdministrator() || user.IsInRole("DepartmentUser");
        }

        public async Task<IActionResult> Index(int? financialYear, string? department)
        {
            var isAdmin = User.IsAdministrator();
            var allowedDepartments = GetEffectiveDepartments(User);
            var isPlainSneUser = !isAdmin && !User.IsInRole("DepartmentUser") && User.IsInRole("SNEDepartmentUser");

            if (!isAdmin && (allowedDepartments == null || allowedDepartments.Count == 0))
            {
                // Administrator without a department assigned - nothing to show yet.
                TempData["Error"] = "No department is assigned to your account.";
                return View(new FinanceDashboard.ViewModels.SneLandingViewModel
                {
                    FinancialYear = financialYear ?? DefaultFinancialYear
                });
            }

            // Single-department SNE users go straight to their own department landing page.
            if (!isAdmin && allowedDepartments!.Count == 1 && string.IsNullOrWhiteSpace(department))
            {
                var single = await _sneRepository.GetDepartmentLandingAsync(allowedDepartments[0], financialYear ?? DefaultFinancialYear);
                single.IsPlainSneUserView = isPlainSneUser;
                return View(single);
            }

            var model = await _sneRepository.GetMultiDepartmentLandingAsync(allowedDepartments, department, financialYear ?? DefaultFinancialYear, isAdmin);
            model.IsPlainSneUserView = isPlainSneUser;
            return View(model);
        }

        // Placeholder for the SNE BM-II form; fields to be added in a follow-up step.
        [HttpGet]
        public async Task<IActionResult> CreateOrEdit(string costCenterCode, int? financialYear)
        {
            var isAdmin = User.IsAdministrator();
            var allowedDepartments = GetEffectiveDepartments(User);

            if (!isAdmin && (allowedDepartments == null || allowedDepartments.Count == 0))
            {
                TempData["Error"] = "No department is assigned to your account.";
                return RedirectToAction(nameof(Index));
            }

            var year = financialYear ?? DefaultFinancialYear;
            // Administrators are not restricted to a specific set of departments; pass an empty list
            // so the repository allows any department for the cost center lookup.
            var model = await _sneRepository.GetCreateOrEditDataAsync(isAdmin ? new List<string>() : allowedDepartments!, costCenterCode, year);
            if (model == null)
            {
                TempData["Error"] = "This cost center does not belong to your assigned department(s).";
                return RedirectToAction(nameof(Index));
            }

            // Compute per-role editability:
            // - Administrator: always editable, and can revert a finally-Submitted form back to
            //   Draft so the Budget Officer can edit it again (not the plain SNE user).
            // - DepartmentUser (incl. "Budget Officer" who is also SNEDepartmentUser): editable
            //   while Draft or pending department review (SubmittedBySNE); read-only once finally
            //   Submitted. Can revert the SNE user's SubmittedBySNE form back to Draft so the SNE
            //   user can edit it again; cannot revert a finally-Submitted form themselves (only an
            //   Administrator can do that).
            // - Plain SNEDepartmentUser (no DepartmentUser role): editable only while Draft AND the
            //   header is not currently SneUserLocked (set only when an Administrator reverts a
            //   finally-Submitted form, leaving it to the Budget Officer/Administrator). Cannot revert.
            var isDepartmentSubmitter = IsDepartmentSubmitter(User);
            if (isAdmin)
            {
                model.IsReadOnly = false;
                model.CanRevert = model.Status == SneFormViewModel.StatusSubmitted;
            }
            else if (isDepartmentSubmitter)
            {
                model.IsReadOnly = model.Status == SneFormViewModel.StatusSubmitted;
                model.CanRevert = model.Status == SneFormViewModel.StatusSubmittedBySne;
            }
            else
            {
                // Once a plain SNE Department User submits, the form moves out of their hands for
                // editing. They may still view it read-only (showing exactly what they submitted,
                // via the immutable snapshot) unless it is still in Draft and not locked, in which
                // case it's still theirs to edit.
                if (model.Status != SneFormViewModel.StatusDraft || model.SneUserLocked)
                {
                    model.IsReadOnly = true;
                    model.CanRevert = false;

                    if (model.HeaderId.HasValue)
                    {
                        var snapshotLines = await _sneRepository.GetSneSnapshotLinesAsync(model.HeaderId.Value);
                        if (snapshotLines.Count > 0)
                            model.Lines = snapshotLines;
                    }
                }
                else
                {
                    model.IsReadOnly = false;
                    model.CanRevert = false;
                }
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetGlCodeInfo(string glCode)
        {
            if (string.IsNullOrWhiteSpace(glCode))
                return Json(new { success = false, message = "GL Code is required." });

            try
            {
                var info = await _sneRepository.GetGlCodeInfoAsync(glCode.Trim());
                if (info == null)
                    return Json(new { success = false, message = $"GL Code '{glCode}' does not exist." });

                return Json(new { success = true, data = info });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error looking up GL Code {GlCode}", glCode);
                return Json(new { success = false, message = "Failed to look up GL Code." });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCostCenter(string costCenterCode, int? financialYear)
        {
            var isAdmin = User.IsAdministrator();
            var allowedDepartments = GetEffectiveDepartments(User);

            if (!isAdmin && (allowedDepartments == null || allowedDepartments.Count == 0))
            {
                TempData["Error"] = "No department is assigned to your account.";
                return RedirectToAction(nameof(Index));
            }

            var year = financialYear ?? DefaultFinancialYear;

            try
            {
                var result = await _sneRepository.ExportCostCenterExcelAsync(isAdmin ? new List<string>() : allowedDepartments!, costCenterCode, year);
                if (result == null)
                {
                    TempData["Error"] = "This cost center does not belong to your assigned department(s).";
                    return RedirectToAction(nameof(Index));
                }

                return File(result.Value.content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", result.Value.fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting SNE cost center {CostCenterCode}", costCenterCode);
                TempData["Error"] = "Failed to export the cost center.";
                return RedirectToAction(nameof(CreateOrEdit), new { costCenterCode, financialYear = year });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ImportCostCenter(IFormFile file, string costCenterCode, string? fundCode)
        {
            var allowedDepartments = GetEffectiveDepartments(User);
            if (!User.IsAdministrator() && (allowedDepartments == null || allowedDepartments.Count == 0))
                return Json(new { success = false, message = "No department is assigned to your account." });

            if (file == null || file.Length == 0)
                return Json(new { success = false, message = "Please choose a file to import." });

            if (string.IsNullOrWhiteSpace(costCenterCode))
                return Json(new { success = false, message = "Cost Center Code is required to validate the import." });

            var extension = Path.GetExtension(file.FileName);
            if (!string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
                return Json(new { success = false, message = "Only .xlsx files are supported." });

            try
            {
                using var stream = file.OpenReadStream();
                var (lines, errors) = await _sneRepository.ImportCostCenterExcelAsync(stream, costCenterCode, fundCode);

                if (errors.Count > 0)
                    return Json(new { success = false, message = "The uploaded file has validation errors.", errors });

                return Json(new { success = true, lines });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing SNE cost center Excel file");
                return Json(new { success = false, message = "Failed to read the uploaded file. Please ensure it is a valid Excel file." });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Save([FromBody] SneSaveRequest request)
        {
            try
            {
                var allowedDepartments = GetEffectiveDepartments(User);
                if (!User.IsAdministrator() && (allowedDepartments == null || allowedDepartments.Count == 0))
                    return Json(new { success = false, message = "No department is assigned to your account." });

                request.IsSubmit = false;
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var headerId = await _sneRepository.SaveSneAsync(request, userId, IsDepartmentSubmitter(User));
                return Json(new { success = true, message = "SNE saved as draft.", headerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving SNE draft");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit([FromBody] SneSaveRequest request)
        {
            try
            {
                var allowedDepartments = GetEffectiveDepartments(User);
                if (!User.IsAdministrator() && (allowedDepartments == null || allowedDepartments.Count == 0))
                    return Json(new { success = false, message = "No department is assigned to your account." });

                if (request.Lines.Count == 0)
                    return Json(new { success = false, message = "Please add at least one row before submitting." });

                foreach (var line in request.Lines)
                {
                    if (line.PostCurrent == null || line.ProposedBudget1 == null || line.ProposedBudget2 == null || line.ProposedBudget3 == null)
                    {
                        return Json(new { success = false, message = "Please fill No of Posts 2027-28, Proposed Budget 2027-28, Budget Estimate 2028-29 and Budget Estimate 2029-30 for every row before submitting." });
                    }
                }

                request.IsSubmit = true;
                var isDepartmentSubmitter = IsDepartmentSubmitter(User);
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                var headerId = await _sneRepository.SaveSneAsync(request, userId, isDepartmentSubmitter);
                var message = isDepartmentSubmitter
                    ? "SNE submitted successfully (final)."
                    : "SNE submitted successfully and sent for department review.";
                return Json(new { success = true, message, headerId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error submitting SNE");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Revert([FromBody] SneRevertRequest request)
        {
            var isAdmin = User.IsAdministrator();
            var isDepartmentSubmitter = IsDepartmentSubmitter(User);
            if (!isAdmin && !isDepartmentSubmitter)
                return Json(new { success = false, message = "You are not authorized to revert this SNE form." });

            try
            {
                var status = await _sneRepository.GetHeaderStatusAsync(request.HeaderId);

                // Administrator may only revert a finally-Submitted form (giving control back to the
                // Budget Officer). A DepartmentUser/Budget Officer may only revert a SubmittedBySNE
                // form (giving control back to the SNE Department User); they cannot revert their own
                // final Submitted form — only an Administrator can do that.
                if (isAdmin && status != SneFormViewModel.StatusSubmitted)
                    return Json(new { success = false, message = "An Administrator can only revert a finally submitted SNE form." });
                if (!isAdmin && status != SneFormViewModel.StatusSubmittedBySne)
                    return Json(new { success = false, message = "You can only revert this SNE form while it is pending department review." });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
                // Admin revert locks the SNE Department User out of the new Draft (only the Budget
                // Officer/Administrator can touch it again); Budget Officer revert does not.
                await _sneRepository.RevertSneAsync(request.HeaderId, userId, lockOutSneUser: isAdmin);
                return Json(new { success = true, message = "SNE form reverted to Draft. You can now edit and resubmit it." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reverting SNE header {HeaderId}", request.HeaderId);
                return Json(new { success = false, message = "Failed to revert the SNE form." });
            }
        }
    }
}
