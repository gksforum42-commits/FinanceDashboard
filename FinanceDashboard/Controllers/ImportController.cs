using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FinanceDashboard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class ImportController : Controller
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly IExcelService _excelService;
        private readonly IAuditRepository _auditRepository;
        private readonly IWebHostEnvironment _environment;
        private readonly ILogger<ImportController> _logger;

        public ImportController(
            IBudgetRepository budgetRepository,
            IExcelService excelService,
            IAuditRepository auditRepository,
            IWebHostEnvironment environment,
            ILogger<ImportController> logger)
        {
            _budgetRepository = budgetRepository;
            _excelService = excelService;
            _auditRepository = auditRepository;
            _environment = environment;
            _logger = logger;
        }

        public IActionResult UploadOriginalBudget()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DownloadOriginalBudgetSample()
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Original_budget");

            var headers = new[]
            {
                "Financial Year", "Document Number", "Department Code", "Budget Type",
                "Fund Code", "Fund Description", "Cost Center Code", "SNE Code",
                "Commitment Item", "Sub Head", "BPS", "Designation Code",
                "Posts", "Original Budget Amount", "Revised Budget Amount",
                "Designation Description", "Description", "Posting Date",
                "User Name", "Function Code", "Type"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(26, 58, 92));
                ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                ws.Cells[1, i + 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            object[] sampleRow =
            {
                DateTime.Now.Year, "DOC-2025-001", "FD001", "Original",
                "P01", "Punjab Finance Fund", "CC-FIN-001", "SNE-001",
                "A01101", "SH-001", "17", "DES-001",
                5, 5000000m, 5000000m,
                "Finance Officer", "Budget Allocation for Finance Dept", DateTime.Now.ToString("dd/MM/yyyy"),
                "admin", "0101", "Budget"
            };

            for (int i = 0; i < sampleRow.Length; i++)
                ws.Cells[2, i + 1].Value = sampleRow[i];

            // Format numeric columns
            ws.Cells[2, 14].Style.Numberformat.Format = "#,##0.00";
            ws.Cells[2, 15].Style.Numberformat.Format = "#,##0.00";

            ws.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "OriginalBudget_Sample_Format.xlsx");
        }

        public IActionResult UploadBER()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DownloadBERSample()
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("ber");

            // Header row
            var headers = new[]
            {
                "Financial Year", "Fund Code", "Fund Center Code", "Fund Center Description",
                "Ministry/Department", "Attached Department", "Profit Center",
                "Sub Detail Function", "Detail Object",
                "Budget Grant", "Supplementary Grant", "Reappropriation (+)", "Reappropriation (-)",
                "TSS/Surrender", "Final Grant", "Releases",
                "Jul Exp", "Aug Exp", "Sep Exp", "Oct Exp", "Nov Exp", "Dec Exp",
                "Jan Exp", "Feb Exp", "Mar Exp", "Apr Exp", "May Exp", "Jun Exp",
                "Actual Expenditure", "Final Grant Balance"
            };

            for (int i = 0; i < headers.Length; i++)
            {
                ws.Cells[1, i + 1].Value = headers[i];
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(26, 58, 92));
                ws.Cells[1, i + 1].Style.Font.Color.SetColor(System.Drawing.Color.White);
                ws.Cells[1, i + 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            // Sample data row
            object[] sampleRow =
            {
                DateTime.Now.Year, "P01", "BR4683", "Finance Department Punjab", "Finance", "Attached Dept A",
                "PC001", "0101", "A01101",
                5000000m, 0m, 200000m, 0m, 0m, 5200000m, 4000000m,
                300000m, 350000m, 400000m, 380000m, 420000m, 450000m,
                410000m, 390000m, 430000m, 470000m, 0m, 0m,
                4000000m, 1200000m
            };

            for (int i = 0; i < sampleRow.Length; i++)
                ws.Cells[2, i + 1].Value = sampleRow[i];

            // Format numeric columns
            for (int col = 10; col <= 30; col++)
                ws.Cells[2, col].Style.Numberformat.Format = "#,##0.00";

            ws.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BER_Sample_Format.xlsx");
        }

        public async Task<IActionResult> History(int page = 1)
        {
            var history = await _budgetRepository.GetImportHistoryAsync(null, page, 20);
            return View(history);
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> Preview([FromForm] ImportViewModel model)
        {
            try
            {
                if (model.ExcelFile == null || model.ExcelFile.Length == 0)
                    return Json(new { success = false, message = "Please select a file." });

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var extension = Path.GetExtension(model.ExcelFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Invalid file type. Only .xlsx and .xls files are allowed." });

                if (model.ExcelFile.Length > 50 * 1024 * 1024)
                    return Json(new { success = false, message = "File size exceeds 50 MB limit." });

                var preview = await _excelService.PreviewExcelAsync(model.ExcelFile, model.ImportType);
                preview.FinancialYear = model.FinancialYear;
                preview.ImportType = model.ImportType;

                // Store file temporarily
                var tempPath = Path.Combine(_environment.WebRootPath, "uploads", $"temp_{Guid.NewGuid()}{extension}");
                Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);

                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await model.ExcelFile.CopyToAsync(stream);
                }

                HttpContext.Session.SetString("TempFilePath", tempPath);
                HttpContext.Session.SetInt32("ImportFinancialYear", model.FinancialYear);
                HttpContext.Session.SetString("ImportType", model.ImportType);

                return Json(new { success = true, preview });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing Excel file");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> Import()
        {
            try
            {
                var tempPath = HttpContext.Session.GetString("TempFilePath");
                var financialYear = HttpContext.Session.GetInt32("ImportFinancialYear") ?? DateTime.Now.Year;
                var importType = HttpContext.Session.GetString("ImportType") ?? "Both";

                if (string.IsNullOrEmpty(tempPath) || !System.IO.File.Exists(tempPath))
                    return Json(new { success = false, message = "No file to import. Please upload again." });

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                var userName = User.Identity?.Name ?? "unknown";

                // Create import history record
                var fileInfo = new FileInfo(tempPath);
                var importHistory = new ImportHistory
                {
                    FileName = Path.GetFileName(tempPath),
                    OriginalFileName = Path.GetFileName(tempPath).Replace("temp_", ""),
                    FileSize = fileInfo.Length,
                    FilePath = tempPath,
                    ImportType = importType,
                    Status = "Processing",
                    FinancialYear = financialYear,
                    ImportedBy = userId,
                    ImportedDate = DateTime.UtcNow
                };

                var importId = await _budgetRepository.AddImportHistoryAsync(importHistory);

                // Perform import
                using var fileStream = new FileStream(tempPath, FileMode.Open, FileAccess.Read);
                var formFile = new FormFile(fileStream, 0, fileInfo.Length, "file", importHistory.OriginalFileName);

                var (originalCount, berCount, errors) = await _excelService.ImportExcelAsync(formFile, financialYear, importType, importId);

                // Update import history
                importHistory.RecordsImported = originalCount + berCount;
                importHistory.OriginalBudgetCount = originalCount;
                importHistory.BERCount = berCount;
                importHistory.ValidationErrors = errors.Count;
                importHistory.Status = errors.Count > 0 ? "Completed with Errors" : "Completed";
                importHistory.CompletedDate = DateTime.UtcNow;
                importHistory.ErrorMessage = errors.Count > 0 ? string.Join("; ", errors.Take(10)) : null;

                await _budgetRepository.UpdateImportHistoryAsync(importHistory);

                // Audit log
                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Action = "Import",
                    EntityType = "ImportHistory",
                    EntityId = importId.ToString(),
                    NewValues = $"Imported {originalCount} OB and {berCount} BER records for FY {financialYear}",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Status = "Success"
                });

                // Cleanup temp file
                try { System.IO.File.Delete(tempPath); } catch { }
                HttpContext.Session.Remove("TempFilePath");
                HttpContext.Session.Remove("ImportFinancialYear");
                HttpContext.Session.Remove("ImportType");

                return Json(new
                {
                    success = true,
                    message = $"Import completed successfully. Original Budget: {originalCount}, BER: {berCount}",
                    originalCount,
                    berCount,
                    errors
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing Excel file");

                // Audit log for error
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = User.Identity?.Name ?? "unknown",
                    Action = "Import",
                    Status = "Error",
                    ErrorMessage = ex.Message,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateBERData()
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                var userName = User.Identity?.Name ?? "unknown";

                var (rowsCopied, failedStep, message) = await _budgetRepository.UpdateBERDataAsync();

                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Action = "UpdateBERData",
                    EntityType = "BERData",
                    NewValues = message,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Status = "Success"
                });

                return Json(new { success = true, message, rowsCopied, failedStep });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating BER data");

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = User.Identity?.Name ?? "unknown",
                    Action = "UpdateBERData",
                    EntityType = "BERData",
                    Status = "Error",
                    ErrorMessage = ex.Message,
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                });

                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateBERDataStep([FromQuery] int step)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                object result;

                switch (step)
                {
                    case 1:
                        var rows = await _budgetRepository.UpdateBERDataStep1Async();
                        result = new { success = true, rowsCopied = rows, message = $"Step 1 complete — {rows} records copied from BerSapData to BERData." };
                        break;
                    case 2:
                        await _budgetRepository.UpdateBERDataStep2Async();
                        result = new { success = true, rowsCopied = 0, message = "Step 2 complete — allocated budget merged from OriginalBudget." };
                        break;
                    case 3:
                        await _budgetRepository.UpdateBERDataStep3Async();
                        result = new { success = true, rowsCopied = 0, message = "Step 3 complete — Fund description and sector updated." };
                        break;
                    case 4:
                        await _budgetRepository.UpdateBERDataStep4Async();
                        result = new { success = true, rowsCopied = 0, message = "Step 4 complete — GL descriptions updated." };
                        break;
                    case 5:
                        await _budgetRepository.UpdateBERDataStep5Async();
                        result = new { success = true, rowsCopied = 0, message = "Step 5 complete — cost center mapping updated." };
                        break;
                    case 6:
                        await _budgetRepository.UpdateBERDataStep6Async();
                        await _auditRepository.LogAsync(new AuditLog
                        {
                            UserId = userId,
                            UserName = User.Identity?.Name ?? "unknown",
                            Action = "UpdateBERData",
                            EntityType = "BERData",
                            NewValues = "All 6 steps completed successfully.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            Status = "Success"
                        });
                        result = new { success = true, rowsCopied = 0, message = "Step 6 complete — department names updated. Dashboard data is ready." };
                        break;
                    default:
                        return Json(new { success = false, message = $"Unknown step: {step}" });
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateBERDataStep {Step}", step);
                return Json(new { success = false, message = $"Step {step} failed: {ex.Message}" });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteImport(long id)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                var userName = User.Identity?.Name ?? "unknown";

                var importHistory = await _budgetRepository.GetImportHistoryByIdAsync(id);
                if (importHistory == null)
                    return Json(new { success = false, message = "Import record not found." });

                // BER no longer has ImportId/IsDeleted columns, so delete by import is not supported for BER imports.
                if (importHistory.BERCount > 0 || string.Equals(importHistory.ImportType, "BER", StringComparison.OrdinalIgnoreCase) || string.Equals(importHistory.ImportType, "Both", StringComparison.OrdinalIgnoreCase))
                {
                    return Json(new { success = false, message = "Delete is disabled for BER/Both imports because BER records are no longer linked by ImportId." });
                }

                var result = await _budgetRepository.DeleteImportAsync(id, userId);

                if (result)
                {
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = userId,
                        UserName = userName,
                        Action = "Delete",
                        EntityType = "ImportHistory",
                        EntityId = id.ToString(),
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Status = "Success"
                    });

                    return Json(new { success = true, message = "Import deleted successfully." });
                }

                return Json(new { success = false, message = "Failed to delete import." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting import");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetHistoryData(int page = 1)
        {
            try
            {
                var history = await _budgetRepository.GetImportHistoryAsync(null, page, 20);
                return Json(new { success = true, data = history });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting import history");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
