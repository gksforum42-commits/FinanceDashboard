using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.Services;
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
        private readonly ImportJobService _importJobService;
        private readonly IServiceScopeFactory _scopeFactory;

        public ImportController(
            IBudgetRepository budgetRepository,
            IExcelService excelService,
            IAuditRepository auditRepository,
            IWebHostEnvironment environment,
            ILogger<ImportController> logger,
            ImportJobService importJobService,
            IServiceScopeFactory scopeFactory)
        {
            _budgetRepository = budgetRepository;
            _excelService = excelService;
            _auditRepository = auditRepository;
            _environment = environment;
            _logger = logger;
            _importJobService = importJobService;
            _scopeFactory = scopeFactory;
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

        public IActionResult UploadYpProject()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DownloadYpProjectSample()
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("ypproject");

            var headers = new[]
            {
                "FiscalYear", "ParentProject", "ProjectID", "WBSElement", "ProfitCenter",
                "CostCenter", "MinistryDept", "AttachedDept", "DetailFunctionCode",
                "OriginalBudget", "SupplementaryBudget", "Surrender", "ReappPlus", "ReappMinus",
                "FinalBudget", "ProgReleases", "InterimDateExp", "ProgressiveExp", "Balance",
                "SectorID", "SubSect", "Fund",
                "JulyExp", "AugustExp", "SeptemberExp", "OctoberExp", "NovemberExp", "DecemberExp",
                "JanuaryExp", "FebruaryExp", "MarchExp", "AprilExp", "MayExp", "JuneExp",
                "ProjectDescription"
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

            var sectors = new[] { "Non Development", "Development", "Capital-A/c-I", "Capital-A/c-II" };
            var rnd = new Random(42);
            for (int i = 0; i < 8; i++)
            {
                int row = i + 2;
                object[] sampleRow =
                {
                    DateTime.Now.Year, i % 3 == 0 ? "" : $"PRJ-PARENT-{i:000}", $"PRJ-{i:0000}", $"WBS-{i:0000}",
                    $"PC{i:000}", $"CC-FIN-{i:000}", "FD001", $"ATD-{i:000}", 100 + i,
                    2000000m + i * 50000, 0m, 0m, 100000m, 0m,
                    2100000m + i * 50000, 1500000m + i * 30000, 200000m + i * 10000, 1400000m + i * 25000, 700000m + i * 20000,
                    sectors[i % sectors.Length], $"SS-{i:00}", "P01",
                    100000m, 110000m, 120000m, 115000m, 105000m, 130000m,
                    125000m, 118000m, 122000m, 128000m, 0m, 0m,
                    $"Sample Project {i + 1} Description"
                };

                for (int c = 0; c < sampleRow.Length; c++)
                    ws.Cells[row, c + 1].Value = sampleRow[c];
            }

            for (int col = 10; col <= 19; col++)
                ws.Cells[2, col, 9, col].Style.Numberformat.Format = "#,##0.00";
            for (int col = 23; col <= 34; col++)
                ws.Cells[2, col, 9, col].Style.Numberformat.Format = "#,##0.00";

            ws.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "YpProject_Sample_Format.xlsx");
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

                // Store file metadata in the in-memory pending-import store and return a
                // fileToken to the browser. The browser will send this token back with the
                // Import POST. This replaces session, which is unreliable on hosted servers
                // (monsterASP.NET, Azure) because app pool recycles wipe in-memory session.
                var fileToken = _importJobService.StorePendingImport(tempPath, model.FinancialYear, model.ImportType);

                return Json(new { success = true, preview, fileToken });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error previewing Excel file");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> Import([FromForm] ImportViewModel model)
        {
            if (model.ExcelFile == null || model.ExcelFile.Length == 0)
                return Json(new { success = false, message = "No file received. Please select the file and try again." });

            var allowedExtensions = new[] { ".xlsx", ".xls" };
            var extension = Path.GetExtension(model.ExcelFile.FileName).ToLowerInvariant();
            if (!allowedExtensions.Contains(extension))
                return Json(new { success = false, message = "Invalid file type. Only .xlsx and .xls files are allowed." });

            var userId       = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var financialYear = model.FinancialYear;
            var importType   = model.ImportType;

            // Save the uploaded file to disk — subsequent chunk requests read it back by path.
            // No background thread is started here: on constrained shared hosting (e.g.
            // MonsterASP, which has no "Always On"/keep-alive), a fire-and-forget Task.Run
            // background import gets silently killed once this request completes, leaving the
            // UI polling forever with no error. Instead, the browser drives progress by calling
            // /Import/ImportChunk repeatedly — each call is a normal bounded HTTP request/response.
            var tempPath = Path.Combine(_environment.WebRootPath, "uploads", $"temp_{Guid.NewGuid()}{extension}");
            Directory.CreateDirectory(Path.GetDirectoryName(tempPath)!);
            using (var fs = new FileStream(tempPath, FileMode.Create))
                await model.ExcelFile.CopyToAsync(fs);

            var importHistory = new ImportHistory
            {
                FileName         = model.ExcelFile.FileName,
                OriginalFileName = model.ExcelFile.FileName,
                FileSize         = new FileInfo(tempPath).Length,
                FilePath         = tempPath,
                ImportType       = importType,
                Status           = "Processing",
                FinancialYear    = financialYear,
                ImportedBy       = userId,
                ImportedDate     = DateTime.UtcNow
            };
            var importId = await _budgetRepository.AddImportHistoryAsync(importHistory);

            // Truncate BER staging up front (only ever done once, before the first chunk).
            if (importType == "BER" || importType == "Both")
                await _budgetRepository.TruncateBerSapDataAsync();

            return Json(new
            {
                success = true,
                importId,
                financialYear,
                importType,
                needsOriginalBudget = importType == "OriginalBudget" || importType == "Both",
                needsBer            = importType == "BER" || importType == "Both",
                needsYpProject      = importType == "YpProject"
            });
        }

        private const int ImportChunkSize = 10000;

        /// <summary>
        /// Processes one bounded chunk of a sheet already saved to disk (see Import()) and
        /// returns whether more chunks remain. The browser calls this repeatedly — driving the
        /// whole import via ordinary short-lived HTTP requests instead of a detached background
        /// thread, which is what was silently dying on constrained shared hosting.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ImportChunk(long importId, string sheetKind, int financialYear, string importType, int startRow)
        {
            var record = await _budgetRepository.GetImportHistoryByIdAsync(importId);
            if (record == null || string.IsNullOrEmpty(record.FilePath))
                return Json(new { success = false, message = "Import record not found." });

            try
            {
                var (inserted, totalRows, done) = await _excelService.ImportChunkAsync(
                    record.FilePath, importType, sheetKind, financialYear, importId, startRow, ImportChunkSize,
                    _importJobService.GetOrOpenWorkbook);

                if (done)
                    _importJobService.CloseWorkbook(importId);

                return Json(new
                {
                    success = true,
                    inserted,
                    totalRows,
                    done,
                    nextStartRow = startRow + ImportChunkSize
                });
            }
            catch (Exception ex)
            {
                _importJobService.CloseWorkbook(importId);
                _logger.LogError(ex, "Error processing import chunk for importId {ImportId}, sheet {Sheet}, startRow {StartRow}", importId, sheetKind, startRow);

                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                record.Status        = "Failed";
                record.ErrorMessage  = ex.Message;
                record.CompletedDate = DateTime.UtcNow;
                try { await _budgetRepository.UpdateImportHistoryAsync(record); } catch { }
                try
                {
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = userId, UserName = User.Identity?.Name ?? "unknown", Action = "Import",
                        Status = "Error", ErrorMessage = ex.Message, IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    });
                }
                catch { }

                return Json(new { success = false, message = ex.Message });
            }
        }

        /// <summary>
        /// Called by the browser once every chunk (of every requested sheet) has completed.
        /// Finalizes the ImportHistory record and cleans up the temp file — the same
        /// bookkeeping that used to run at the end of the background Task.Run.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> ImportFinalize(long importId, int originalCount, int berCount, int ypProjectCount = 0)
        {
            _importJobService.CloseWorkbook(importId);

            var record = await _budgetRepository.GetImportHistoryByIdAsync(importId);
            if (record == null)
                return Json(new { success = false, message = "Import record not found." });

            var userId   = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var userName = User.Identity?.Name ?? "unknown";

            record.RecordsImported     = originalCount + berCount + ypProjectCount;
            record.OriginalBudgetCount = originalCount;
            record.BERCount            = berCount;
            record.Status              = "Completed";
            record.CompletedDate       = DateTime.UtcNow;
            await _budgetRepository.UpdateImportHistoryAsync(record);

            await _auditRepository.LogAsync(new AuditLog
            {
                UserId = userId, UserName = userName, Action = "Import",
                EntityType = "ImportHistory", EntityId = importId.ToString(),
                NewValues = $"Imported {originalCount} OB, {berCount} BER, {ypProjectCount} YpProject records for FY {record.FinancialYear}",
                IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(), Status = "Success"
            });

            if (!string.IsNullOrEmpty(record.FilePath))
            {
                try { System.IO.File.Delete(record.FilePath); } catch { }
            }

            var msg = ypProjectCount > 0
                ? $"Import completed. YpProject: {ypProjectCount}"
                : $"Import completed. BER: {berCount}" + (originalCount > 0 ? $", OB: {originalCount}" : "");
            return Json(new { success = true, message = msg });
        }

        /// <summary>
        /// Polling endpoint — reads import status from the DATABASE so it works on any server
        /// instance regardless of which instance is running the background job.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> ImportProgress(long importId)
        {
            var record = await _budgetRepository.GetImportHistoryByIdAsync(importId);
            if (record == null)
                return Json(new { success = false, message = "Import record not found." });

            var isDone   = record.Status == "Completed" || record.Status == "Completed with Errors";
            var isFailed = record.Status == "Failed";

            // While running, RecordsImported is updated as a live heartbeat after every ~20,000-row
            // chunk (see ExcelService.ImportBERSheetAsync). This lets the UI show real,
            // continuously increasing progress (with decimal precision) instead of a flat
            // percentage that looks frozen even when the import is still working — and a
            // stalled count across several polls is a real signal that the background job
            // died (e.g. killed by the host) rather than just being slow.
            double runningPct = record.RecordsImported > 0
                ? Math.Min(95.0, 30.0 + record.RecordsImported / 3000.0)
                : 30.0;
            runningPct = Math.Round(runningPct, 1);
            var runningMsg = record.RecordsImported > 0
                ? $"Importing records… {record.RecordsImported:N0} processed so far"
                : "Importing records…";

            return Json(new
            {
                success       = true,
                status        = isFailed ? "error" : (isDone ? "done" : "running"),
                progressPct   = isFailed ? 100 : (isDone ? 100 : runningPct),
                message       = isFailed ? (record.ErrorMessage ?? "Import failed.") : (isDone ? record.ErrorMessage ?? "" : runningMsg),
                berCount      = record.BERCount,
                originalCount = record.OriginalBudgetCount,
                importStatus  = record.Status
            });
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
                        result = new { success = true, rowsCopied = 0, message = "Step 6 complete — department names updated." };
                        break;
                    case 7:
                        await _budgetRepository.UpdateBERDataStep7Async();
                        await _auditRepository.LogAsync(new AuditLog
                        {
                            UserId = userId,
                            UserName = User.Identity?.Name ?? "unknown",
                            Action = "UpdateBERData",
                            EntityType = "BERData",
                            NewValues = "All 7 steps completed successfully.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            Status = "Success"
                        });
                        result = new { success = true, rowsCopied = 0, message = "Step 7 complete — 9-Item classification updated. Dashboard data is ready." };
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
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateYpProjectStep([FromQuery] int step, [FromQuery] int financialYear)
        {
            try
            {
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
                object result;

                switch (step)
                {
                    case 1:
                        await _budgetRepository.DeleteDevExpByYearAsync(financialYear);
                        result = new { success = true, rowsCopied = 0, message = $"Step 1 complete — existing devExp records for FY {financialYear} cleared." };
                        break;
                    case 2:
                        var rows = await _budgetRepository.MergeYpProjectIntoDevExpAsync(financialYear);
                        await _auditRepository.LogAsync(new AuditLog
                        {
                            UserId = userId,
                            UserName = User.Identity?.Name ?? "unknown",
                            Action = "UpdateYpProjectData",
                            EntityType = "devExp",
                            NewValues = $"Merged {rows} records into devExp for FY {financialYear}.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            Status = "Success"
                        });
                        result = new { success = true, rowsCopied = rows, message = $"Step 2 complete — {rows} records merged from ypProject/devOb into devExp." };
                        break;
                    case 3:
                        var glRows = await _budgetRepository.UpdateDevExpGlCodeAsync();
                        result = new { success = true, rowsCopied = glRows, message = $"Step 3 complete — gl_Object updated for {glRows} records from WBS Element." };
                        break;
                    case 4:
                        var glDescRows = await _budgetRepository.UpdateDevExpGlDescriptionAsync();
                        result = new { success = true, rowsCopied = glDescRows, message = $"Step 4 complete — gldesc updated for {glDescRows} records from GL_Codes." };
                        break;
                    case 5:
                        var pndSectorRows = await _budgetRepository.UpdateDevExpPnDSectorAsync();
                        await _auditRepository.LogAsync(new AuditLog
                        {
                            UserId = userId,
                            UserName = User.Identity?.Name ?? "unknown",
                            Action = "UpdateYpProjectData",
                            EntityType = "devExp",
                            NewValues = $"Updated pndSector for {pndSectorRows} records from pdSec.",
                            IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                            Status = "Success"
                        });
                        result = new { success = true, rowsCopied = pndSectorRows, message = $"Step 5 complete — pndSector updated for {pndSectorRows} records from pdSec." };
                        break;
                    default:
                        return Json(new { success = false, message = $"Unknown step: {step}" });
                }

                return Json(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in UpdateYpProjectStep {Step}", step);
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

        // PnD File
        public IActionResult UploadPnDFile()
        {
            return View();
        }

        [HttpGet]
        public IActionResult DownloadPnDFileSample()
        {
            using var package = new OfficeOpenXml.ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("PnDFile");

            var headers = new[]
            {
                "SRNo", "GSNo", "SchemeNo", "FullSchemeNo", "LoNumberCapital", "LoNumberRevenue",
                "Name", "District", "DistrictName", "Scope", "Objectives", "IssueRemarks",
                "GestationPeriod", "ApprovalStatus", "InitialCost", "Cost", "ExpenseUptoJune",
                "ActualExpenditureUptoJune2026", "LocalSouthPunjab", "ForeignSouthPunjab", "AllocationSouthPunjab",
                "LocalAllocation", "ForeignAllocation", "Allocation", "LocalRevisedAllocation",
                "ForeignRevisedAllocation", "RevisedAllocation", "LocalPDDRelease", "ForeignPDDRelease",
                "ReleaseByPDD", "ReleaseReceivedByAD", "FDRelease", "SpendingRelease", "UtilizationPifra",
                "TotalThrowForward", "Utilization", "PctUtilCont", "PctUtilRevAlloc", "SectorID", "SectorOrder",
                "SectorName", "SchemeType", "SchemeSubType", "AcToBeCredited", "ProjectStatusType", "ApprovalForum",
                "AA", "TS", "ExecutingAgency", "SponsoringAgency", "IsSNERequired", "IsPCI",
                "AttachDepartment", "AdminDepartment", "IsMOM", "IsAA", "ProjectID"
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

            ws.Cells.AutoFitColumns();

            var bytes = package.GetAsByteArray();
            return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "PnDFile_Sample_Format.xlsx");
        }

        [HttpPost]
        [RequestSizeLimit(52_428_800)]
        [RequestFormLimits(MultipartBodyLengthLimit = 52_428_800)]
        public async Task<IActionResult> ImportPnDFile(IFormFile excelFile)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var userName = User.Identity?.Name ?? "unknown";

            try
            {
                if (excelFile == null || excelFile.Length == 0)
                    return Json(new { success = false, message = "Please select a file." });

                var allowedExtensions = new[] { ".xlsx", ".xls" };
                var extension = Path.GetExtension(excelFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Invalid file type. Only .xlsx and .xls files are allowed." });

                if (excelFile.Length > 50 * 1024 * 1024)
                    return Json(new { success = false, message = "File size exceeds 50 MB limit." });

                var count = await _excelService.ImportPnDFileAsync(excelFile);

                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Action = "Import",
                    EntityType = "PnDFile",
                    NewValues = $"Imported {count} PnD records (existing data replaced)",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Status = "Success"
                });

                return Json(new { success = true, message = $"Import completed successfully. {count} records imported.", count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error importing PnD file");
                try
                {
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = userId,
                        UserName = userName,
                        Action = "Import",
                        EntityType = "PnDFile",
                        NewValues = $"Import failed: {ex.Message}",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Status = "Failed"
                    });
                }
                catch { /* swallow audit failure */ }

                return Json(new { success = false, message = "Import failed: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdatePdSec()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown";
            var userName = User.Identity?.Name ?? "unknown";

            try
            {
                var count = await _budgetRepository.UpdatePdSecFromPnDFileAsync();

                await _auditRepository.LogAsync(new AuditLog
                {
                    UserId = userId,
                    UserName = userName,
                    Action = "Update",
                    EntityType = "PdSec",
                    NewValues = $"Updated {count} PdSec records from PnD file data",
                    IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                    Status = "Success"
                });

                return Json(new { success = true, message = $"PdSec data updated successfully. {count} records processed.", count });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating PdSec data");

                try
                {
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = userId,
                        UserName = userName,
                        Action = "Update",
                        EntityType = "PdSec",
                        Status = "Error",
                        ErrorMessage = ex.Message,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString()
                    });
                }
                catch { }

                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
