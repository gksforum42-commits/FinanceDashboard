using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceDashboard.Controllers
{
    [Authorize(Policy = "NotSneDepartmentUser")]
    public class ReportsController : Controller
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly IExcelService _excelService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<ReportsController> _logger;

        public ReportsController(
            IBudgetRepository budgetRepository,
            IExcelService excelService,
            IPdfService pdfService,
            ILogger<ReportsController> logger)
        {
            _budgetRepository = budgetRepository;
            _excelService = excelService;
            _pdfService = pdfService;
            _logger = logger;
        }

        // Fund Wise Report
        public IActionResult FundWise()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetFundWiseData(int? financialYear, string? fundCode, string? keyword, string? sector)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetFundWiseReportAsync(financialYear, fundCode, keyword, sector, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fund wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Cost Center Wise Report
        public IActionResult CostCenterWise()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterWiseData(int? financialYear, string? fundCode,
            string? costCenterCode, [FromQuery] List<string>? costCenterCodes, [FromQuery] List<string>? objectCodes,
            string? sector, string? departmentCode)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                // Prefer the multi-value lists when provided
                var ccList  = (costCenterCodes  != null && costCenterCodes.Count  > 0) ? costCenterCodes  : null;
                var objList = (objectCodes       != null && objectCodes.Count       > 0) ? objectCodes       : null;
                var data = await _budgetRepository.GetCostCenterWiseReportAsync(
                    financialYear, fundCode, ccList == null ? costCenterCode : null, ccList, objList, sector, departmentCode, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost center wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterGLDetail(string costCenterCode, int? financialYear, string? fundCode, [FromQuery] List<string>? objectCodes)
        {
            try
            {
                var objList = (objectCodes != null && objectCodes.Count > 0) ? objectCodes : null;
                var data = await _budgetRepository.GetCostCenterGLDetailAsync(costCenterCode, financialYear, fundCode, objList);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GL detail for cost center {CC}", costCenterCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Department Wise Report
        public IActionResult DepartmentWise()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentWiseData(int? financialYear, string? sector, string? departmentCode, string? fundCode)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var data = await _budgetRepository.GetDepartmentWiseReportAsync(financialYear, sector, departmentCode, fundCode, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDrillFunds(string departmentName, int? financialYear, string? sector)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetDrillFundsAsync(departmentName, financialYear, sector, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting drill funds for {Dept}", departmentName);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Grant Cost Centers page (new page opened when clicking a Fund/Grant row on Department Wise)
        public IActionResult GrantCostCenters(string departmentName, string fundCode, string? fundName,
            int? financialYear, string? sector)
        {
            ViewBag.DepartmentName = departmentName;
            ViewBag.FundCode       = fundCode;
            ViewBag.FundName       = fundName ?? fundCode;
            ViewBag.FinancialYear  = financialYear;
            ViewBag.Sector         = sector;
            return View();
        }

        // Cost Center Detail page (opened when clicking a CC row on GrantCostCenters)
        public IActionResult CostCenterDetail(string costCenterCode, string? costCenterName,
            string? fundCode, string? fundName, string? departmentName,
            int? financialYear, string? sector)
        {
            ViewBag.CostCenterCode = costCenterCode;
            ViewBag.CostCenterName = costCenterName ?? costCenterCode;
            ViewBag.FundCode       = fundCode ?? "";
            ViewBag.FundName       = fundName ?? fundCode ?? "";
            ViewBag.DepartmentName = departmentName ?? "";
            ViewBag.FinancialYear  = financialYear;
            ViewBag.Sector         = sector ?? "";
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetGrantCostCentersData(string departmentName, string fundCode,
            int? financialYear, string? sector, string? costCenterCode)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetDrillCostCentersAsync(departmentName, fundCode, financialYear, sector, restrictedDepartments);
                if (!string.IsNullOrWhiteSpace(costCenterCode))
                    data = data.Where(x => x.FundCenterCode == costCenterCode ||
                        (x.CostCenterDescription ?? "").Contains(costCenterCode, StringComparison.OrdinalIgnoreCase)).ToList();
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grant cost centers for {Dept}/{Fund}", departmentName, fundCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetObjectCodeLookup(string costCenterCode, int? financialYear, string? fundCode)
        {
            try
            {
                var data = await _budgetRepository.GetCostCenterGLDetailAsync(costCenterCode, financialYear, fundCode);
                var items = data.Select(x => new { value = x.GLCode, text = string.IsNullOrEmpty(x.GLDescription) ? x.GLCode : $"{x.GLCode} - {x.GLDescription}" })
                                .DistinctBy(x => x.value)
                                .OrderBy(x => x.value)
                                .ToList();
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object code lookup for {CC}", costCenterCode);
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetObjectCodeLookupByFilters(int? financialYear, string? sector, string? departmentCode, string? fundCode, [FromQuery] List<string>? costCenterCodes)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var ccList = (costCenterCodes != null && costCenterCodes.Count > 0) ? costCenterCodes : null;
                var data = await _budgetRepository.GetObjectCodeLookupAsync(financialYear, sector, departmentCode, fundCode, ccList, User.GetRestrictedDepartments());
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object code lookup by filters");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Function Wise Report
        public IActionResult FunctionWise()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetFunctionWiseData(int? financialYear, string? keyword)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetFunctionWiseReportAsync(financialYear, keyword, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting function wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Object Wise Report
        public IActionResult ObjectWise()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetObjectWiseData(int? financialYear, string? departmentCode, string? fundCode, string? costCenterCode, string? keyword, string? sector, [FromQuery] List<string>? costCenterCodes, [FromQuery] List<string>? objectCodes)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var ccList  = (costCenterCodes != null && costCenterCodes.Count > 0) ? costCenterCodes : null;
                var objList = (objectCodes     != null && objectCodes.Count     > 0) ? objectCodes     : null;
                var data = await _budgetRepository.GetObjectWiseReportAsync(financialYear, departmentCode, fundCode,
                    ccList == null ? costCenterCode : null, objList == null ? keyword : null, sector, ccList, objList);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting object wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetObjectWiseGroupedData(int? financialYear, string? sector, string? departmentCode, string? fundCode, string? costCenterCode, string? keyword, [FromQuery] List<string>? costCenterCodes, [FromQuery] List<string>? objectCodes)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var ccList  = (costCenterCodes != null && costCenterCodes.Count > 0) ? costCenterCodes : null;
                var objList = (objectCodes     != null && objectCodes.Count     > 0) ? objectCodes     : null;
                var data = await _budgetRepository.GetObjectWiseGroupedAsync(financialYear, sector, departmentCode, fundCode,
                    ccList == null ? costCenterCode : null, objList == null ? keyword : null, ccList, objList);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting grouped object wise report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetObjectWiseGLDetail(string detailObject, int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetObjectWiseGLDetailAsync(detailObject, financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GL detail for object {Obj}", detailObject);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Complete Budget Report
        public IActionResult CompleteBudget()
        {
            return View();
        }

        // Dev Budget Report
        public IActionResult DevBudget()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetDevBudgetData(int? financialYear, [FromQuery] List<string>? parentProjects = null, [FromQuery] List<string>? gsNos = null, [FromQuery] List<string>? sectors = null)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetDevBudgetReportAsync(financialYear, parentProjects, gsNos, sectors, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dev budget report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetParentProjectLookup(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetParentProjectLookupAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting parent project lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetGsNoLookup(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetGsNoLookupAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting GS No lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDevBudgetSectorLookup(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetDevBudgetSectorLookupAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportDevBudgetExcel(int? financialYear, [FromQuery] List<string>? parentProjects = null, [FromQuery] List<string>? gsNos = null, [FromQuery] List<string>? sectors = null)
        {
            try
            {
                var bytes = await _excelService.ExportDevBudgetExcelAsync(financialYear, parentProjects, gsNos, sectors);
                var fileName = $"Development_Expenditure_Report_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Dev Budget report to Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(nameof(DevBudget));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportDevBudgetPdf(int? financialYear, [FromQuery] List<string>? parentProjects = null, [FromQuery] List<string>? gsNos = null, [FromQuery] List<string>? sectors = null)
        {
            try
            {
                var bytes = await _pdfService.GenerateDevBudgetPdfAsync(financialYear, parentProjects, gsNos, sectors);
                var fileName = $"Development_Expenditure_Report_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(bytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Dev Budget report to PDF");
                TempData["Error"] = "Failed to export PDF file.";
                return RedirectToAction(nameof(DevBudget));
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCompleteBudgetData(int? financialYear, string? fundCode, string? departmentCode, string? costCenterCode, string? keyword, int page = 1, int pageSize = 50)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel
                {
                    FinancialYear = financialYear,
                    FundCode = fundCode,
                    DepartmentCode = departmentCode,
                    CostCenterCode = costCenterCode,
                    Keyword = keyword
                };
                var data = await _budgetRepository.GetCompleteBudgetReportAsync(filters, page, pageSize);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting complete budget report");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Department Wise full drill-down Excel export
        [HttpGet]
        public async Task<IActionResult> ExportDepartmentWiseFull(int? financialYear, string? sector, string? departmentCode, string? fundCode)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel
                {
                    FinancialYear  = financialYear,
                    Sector         = sector,
                    DepartmentCode = departmentCode,
                    FundCode       = fundCode
                };
                var excelBytes = await _excelService.ExportDepartmentWiseFullAsync(filters);
                var fileName = $"DepartmentWise_Report_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting Department Wise Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(nameof(DepartmentWise));
            }
        }

        // Export Actions
        [HttpGet]
        public async Task<IActionResult> ExportExcel(string reportType, int? financialYear, string? fundCode, string? departmentCode, string? costCenterCode, string? keyword, string? sector, bool showSector = true, bool showDept = true, bool showFund = true, bool showCC = true, [FromQuery] List<string>? costCenterCodes = null, [FromQuery] List<string>? objectCodes = null)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel
                {
                    FinancialYear  = financialYear,
                    FundCode       = fundCode,
                    DepartmentCode = departmentCode,
                    CostCenterCode = costCenterCode,
                    Keyword        = keyword,
                    Sector         = sector,
                    ShowSector     = showSector,
                    ShowDept       = showDept,
                    ShowFund       = showFund,
                    ShowCC         = showCC,
                    CostCenterCodes = costCenterCodes ?? new List<string>(),
                    ObjectCodes     = objectCodes     ?? new List<string>()
                };

                var excelBytes = await _excelService.ExportReportAsync(reportType, filters);
                var fileName = $"{reportType}_Report_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report to Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(reportType switch
                {
                    "fund" => nameof(FundWise),
                    "department" => nameof(DepartmentWise),
                    "costcenter" => nameof(CostCenterWise),
                    "function" => nameof(FunctionWise),
                    "object" => nameof(ObjectWise),
                    _ => nameof(FundWise)
                });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportPdf(string reportType, int? financialYear, string? fundCode, string? departmentCode, string? keyword)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel
                {
                    FinancialYear = financialYear,
                    FundCode = fundCode,
                    DepartmentCode = departmentCode,
                    Keyword = keyword
                };

                var columns = reportType.ToLower() switch
                {
                    "fund" => new List<string> { "FundCode", "TotalFinalGrant", "TotalActualExpenditure", "TotalBalance", "UtilizationPercentage" },
                    "department" => new List<string> { "DepartmentName", "TotalAllocatedBudget", "TotalBudgetGrant", "TotalReleases", "ReleasePercentage", "TotalActualExpenditure", "UtilizationPercentage", "TotalBalance" },
                    "costcenter" => new List<string> { "FundCenterCode", "TotalFinalGrant", "TotalActualExpenditure", "TotalBalance" },
                    _ => new List<string>()
                };

                var pdfBytes = await _pdfService.GenerateReportPdfAsync(reportType, filters, columns, $"{reportType} Wise Report");
                var fileName = $"{reportType}_Report_{DateTime.Now:yyyyMMddHHmmss}.pdf";
                return File(pdfBytes, "application/pdf", fileName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting report to PDF");
                TempData["Error"] = "Failed to export PDF file.";
                return RedirectToAction(reportType switch
                {
                    "fund" => nameof(FundWise),
                    "department" => nameof(DepartmentWise),
                    "costcenter" => nameof(CostCenterWise),
                    "function" => nameof(FunctionWise),
                    "object" => nameof(ObjectWise),
                    _ => nameof(FundWise)
                });
            }
        }
    }
}
