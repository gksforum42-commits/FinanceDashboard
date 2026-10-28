using OfficeOpenXml;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;
using System.Data;
using System.Reflection;

namespace FinanceDashboard.Services
{
    public class ExcelService : IExcelService
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<ExcelService> _logger;

        public ExcelService(IBudgetRepository budgetRepository, ILogger<ExcelService> logger)
        {
            _budgetRepository = budgetRepository;
            _logger = logger;
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }

        public async Task<ImportPreviewViewModel> PreviewExcelAsync(IFormFile file, string importType)
        {
            var preview = new ImportPreviewViewModel
            {
                FileName = file.FileName,
                ImportType = importType
            };

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);

            var errors = new List<string>();

            if (importType == "OriginalBudget" || importType == "Both")
            {
                var ws = package.Workbook.Worksheets["Original_budget"];
                if (ws != null)
                {
                    var rowCount = ws.Dimension?.Rows ?? 0;
                    preview.TotalRecords += rowCount > 0 ? rowCount - 1 : 0;

                    // Validate first 10 rows
                    for (int row = 2; row <= Math.Min(rowCount, 11); row++)
                    {
                        var rowData = new Dictionary<string, object>();
                        for (int col = 1; col <= ws.Dimension.Columns; col++)
                        {
                            rowData[ws.Cells[1, col].Text.Trim()] = ws.Cells[row, col].Text.Trim();
                        }
                        preview.PreviewData.Add(rowData);
                    }
                }
                else
                {
                    errors.Add("Sheet 'Original_budget' not found in the workbook.");
                }
            }

            if (importType == "BER" || importType == "Both")
            {
                var ws = package.Workbook.Worksheets["ber"];
                if (ws != null)
                {
                    var rowCount = ws.Dimension?.Rows ?? 0;
                    preview.TotalRecords += rowCount > 0 ? rowCount - 1 : 0;
                }
                else
                {
                    errors.Add("Sheet 'ber' not found in the workbook.");
                }
            }

            if (importType == "YpProject")
            {
                var ws = package.Workbook.Worksheets["ypproject"];
                if (ws != null)
                {
                    var rowCount = ws.Dimension?.Rows ?? 0;
                    preview.TotalRecords += rowCount > 0 ? rowCount - 1 : 0;

                    // Validate first 10 rows
                    for (int row = 2; row <= Math.Min(rowCount, 11); row++)
                    {
                        var rowData = new Dictionary<string, object>();
                        for (int col = 1; col <= ws.Dimension.Columns; col++)
                        {
                            rowData[ws.Cells[1, col].Text.Trim()] = ws.Cells[row, col].Text.Trim();
                        }
                        preview.PreviewData.Add(rowData);
                    }
                }
                else
                {
                    errors.Add("Sheet 'ypproject' not found in the workbook.");
                }
            }

            preview.ValidationErrors = errors;
            preview.ValidRecords = preview.TotalRecords;
            preview.InvalidRecords = 0;

            return preview;
        }

        public async Task<(int originalCount, int berCount, List<string> errors)> ImportExcelAsync(IFormFile file, int financialYear, string importType, long importId)
        {
            var errors = new List<string>();
            int originalCount = 0;
            int berCount = 0;

            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);

            await _budgetRepository.UpdateImportHeartbeatAsync(importId, 0);

            if (importType == "OriginalBudget" || importType == "Both")
            {
                var ws = package.Workbook.Worksheets["Original_budget"];
                if (ws != null)
                {
                    originalCount = await ImportOriginalBudgetSheetAsync(ws, financialYear, importId);
                }
            }

            if (importType == "BER" || importType == "Both")
            {
                var ws = package.Workbook.Worksheets["ber"];
                if (ws != null)
                {
                    berCount = await ImportBERSheetAsync(ws, financialYear, importId);
                }
            }

            return (originalCount, berCount, errors);
        }

        private async Task<int> ImportOriginalBudgetSheetAsync(ExcelWorksheet ws, int financialYear, long importId)
        {
            int rowCount = ws.Dimension?.Rows ?? 0;
            int colCount = ws.Dimension?.Columns ?? 0;
            if (rowCount < 2) return 0;
            const int batchSize = 10000;

            int totalInserted = 0;

            // Read + insert in bounded row-range chunks (not one giant upfront array read) so
            // progress can be heartbeated continuously. Without this, when "Both Sheets" import
            // is selected, this method runs FIRST and blocks all progress reporting until it
            // finishes — the BER heartbeat never even starts, so the UI looks frozen.
            for (int chunkStart = 2; chunkStart <= rowCount; chunkStart += batchSize)
            {
                int chunkEnd = Math.Min(chunkStart + batchSize - 1, rowCount);
                var values = (object[,])ws.Cells[chunkStart, 1, chunkEnd, colCount].Value;

                var records = new List<OriginalBudget>(chunkEnd - chunkStart + 1);
                for (int row = chunkStart; row <= chunkEnd; row++)
                {
                    int r = row - chunkStart;
                    try
                    {
                        var record = new OriginalBudget
                        {
                            FinancialYear = CellInt(values, r, 0) is int fy && fy > 0 ? fy : financialYear,
                            DocumentNumber = CellStr(values, r, 1),
                            DepartmentCode = CellStr(values, r, 2),
                            BudgetType = CellStr(values, r, 3),
                            FundCode = CellStr(values, r, 4),
                            FundDescription = CellStr(values, r, 5),
                            CostCenterCode = CellStr(values, r, 6),
                            SNECode = CellStr(values, r, 7),
                            CommitmentItem = CellStr(values, r, 8),
                            SubHead = CellStr(values, r, 9),
                            BPS = CellStr(values, r, 10),
                            DesignationCode = CellStr(values, r, 11),
                            Posts = CellInt(values, r, 12),
                            OriginalBudgetAmount = CellDecimal(values, r, 13),
                            RevisedBudgetAmount = CellDecimal(values, r, 14),
                            DesignationDescription = CellStr(values, r, 15),
                            Description = CellStr(values, r, 16),
                            PostingDate = CellDate(values, r, 17),
                            UserName = CellStr(values, r, 18),
                            FunctionCode = CellStr(values, r, 19),
                            Type = CellStr(values, r, 20),
                            ImportId = importId
                        };

                        records.Add(record);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error parsing row {Row}: {Message}", row, ex.Message);
                    }
                }

                if (records.Count > 0)
                {
                    await _budgetRepository.InsertOriginalBudgetAsync(records);
                    totalInserted += records.Count;
                }

                await _budgetRepository.UpdateImportHeartbeatAsync(importId, totalInserted);
            }

            return totalInserted;
        }

        private async Task<int> ImportBERSheetAsync(ExcelWorksheet ws, int financialYear, long importId)
        {
            int rowCount = ws.Dimension?.Rows ?? 0;
            if (rowCount < 2) return 0;

            // Wipe all existing staging data first so we always start clean
            await _budgetRepository.TruncateBerSapDataAsync();
            await _budgetRepository.UpdateImportHeartbeatAsync(importId, 0);

            int colCount = ws.Dimension!.Columns;

            // Build a header-name → 0-based column-index map (case-insensitive, trimmed)
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cells[1, c].Text.Trim();
                if (!string.IsNullOrEmpty(h) && !headerMap.ContainsKey(h))
                    headerMap[h] = c - 1;
            }

            // Helper: get value by header name, fall back to positional (1-based) index if header not found
            int ColIdx(string name, int fallbackCol1Based) =>
                headerMap.TryGetValue(name, out int ci) ? ci : fallbackCol1Based - 1;

            const int batchSize = 10000;
            int totalInserted = 0;
            var records = new List<BERData>(batchSize);

            // Read + insert in bounded row-range chunks instead of pulling the ENTIRE sheet
            // into one giant object[,] array up front. On a 300k+ row file, that single bulk
            // read of millions of cells can itself take a very long time (or exhaust memory)
            // on a constrained shared host — with zero progress reported until it finishes,
            // which looks exactly like the UI freezing at a fixed percentage. Chunking the
            // EPPlus range read means we can heartbeat progress after every chunk instead of
            // going dark for the whole file.
            for (int chunkStart = 2; chunkStart <= rowCount; chunkStart += batchSize)
            {
                int chunkEnd = Math.Min(chunkStart + batchSize - 1, rowCount);
                var values = (object[,])ws.Cells[chunkStart, 1, chunkEnd, colCount].Value;

                string Col(int r, string name, int fallbackCol1Based)
                    => CellStr(values, r, ColIdx(name, fallbackCol1Based));
                decimal ColD(int r, string name, int fallbackCol1Based)
                    => CellDecimal(values, r, ColIdx(name, fallbackCol1Based));
                int ColI(int r, string name, int fallbackCol1Based)
                    => CellInt(values, r, ColIdx(name, fallbackCol1Based));

                for (int row = chunkStart; row <= chunkEnd; row++)
                {
                    int r = row - chunkStart;
                    try
                    {
                        var fyVal = ColI(r, "FinancialYear", 1);
                        var record = new BERData
                        {
                            ImportId              = importId,
                            FinancialYear         = fyVal > 0 ? fyVal : financialYear,
                            FundCode              = Col(r, "FundCode",              2),
                            FundCenterCode        = Col(r, "FundCenterCode",        3),
                            FundCenterDescription = Col(r, "FundCenterDescription", 4),
                            MinistryDepartment    = Col(r, "MinistryDepartment",    5),
                            AttachedDepartment    = Col(r, "AttachedDepartment",    6),
                            ProfitCenter          = Col(r, "ProfitCenter",          7),
                            SubDetailFunction     = Col(r, "SubDetailFunction",     8),
                            DetailObject          = Col(r, "DetailObject",          9),
                            BudgetGrant           = ColD(r, "BudgetGrant",          10),
                            SupplementaryGrant    = ColD(r, "SupplementaryGrant",   11),
                            ReappropriationPlus   = ColD(r, "ReappropriationPlus",  12),
                            ReappropriationMinus  = ColD(r, "ReappropriationMinus", 13),
                            TSSSurrender          = ColD(r, "TSSSurrender",         14),
                            FinalGrant            = ColD(r, "FinalGrant",           15),
                            Releases              = ColD(r, "Releases",             16),
                            JulExp                = ColD(r, "JulExp",               17),
                            AugExp                = ColD(r, "AugExp",               18),
                            SepExp                = ColD(r, "SepExp",               19),
                            OctExp                = ColD(r, "OctExp",               20),
                            NovExp                = ColD(r, "NovExp",               21),
                            DecExp                = ColD(r, "DecExp",               22),
                            JanExp                = ColD(r, "JanExp",               23),
                            FebExp                = ColD(r, "FebExp",               24),
                            MarExp                = ColD(r, "MarExp",               25),
                            AprExp                = ColD(r, "AprExp",               26),
                            MayExp                = ColD(r, "MayExp",               27),
                            JunExp                = ColD(r, "JunExp",               28),
                            ActualExpenditure     = ColD(r, "ActualExpenditure",    29),
                            FinalGrantBalance     = ColD(r, "FinalGrantBalance",    30)
                        };
                        records.Add(record);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("BER parse error row {Row}: {Msg}", row, ex.Message);
                    }
                }

                if (records.Count > 0)
                {
                    await _budgetRepository.InsertBERDataAsync(records);
                    totalInserted += records.Count;
                    records.Clear();
                }

                // Heartbeat after every chunk (whether or not it had parse errors) so the poller
                // always sees forward motion tied to rows actually processed, not just inserted.
                await _budgetRepository.UpdateImportHeartbeatAsync(importId, Math.Min(chunkEnd - 1, rowCount - 1));
            }

            return totalInserted;
        }

        /// <summary>
        /// Processes ONE bounded chunk of rows (default handled by caller) from a sheet already
        /// saved to disk, then returns immediately. This is the building block of the
        /// client-driven chunked import: each HTTP request opens the file, seeks straight to
        /// the requested row range, inserts it, and returns — never spawning a detached
        /// background thread that a constrained shared host (MonsterASP has no "Always On")
        /// could kill mid-import once the triggering request completes.
        /// </summary>
        public async Task<(int inserted, int totalRows, bool done)> ImportChunkAsync(
            string filePath, string importType, string sheetKind, int financialYear, long importId, int startRow, int chunkSize,
            Func<long, string, ExcelPackage> getOrOpenWorkbook)
        {
            // Re-use the same opened workbook across chunk calls for this importId instead of
            // re-parsing the entire XLSX from disk on every request — repeatedly decompressing
            // a large workbook was spiking memory enough to trigger OutOfMemoryException on
            // constrained shared hosting.
            var package = getOrOpenWorkbook(importId, filePath);
            var sheetName = sheetKind == "BER" ? "ber" : sheetKind == "YpProject" ? "ypproject" : "Original_budget";
            var ws = package.Workbook.Worksheets[sheetName];
            if (ws == null) return (0, 0, true);

            int rowCount = ws.Dimension?.Rows ?? 0;
            if (rowCount < 2) return (0, 0, true);

            int colCount = ws.Dimension!.Columns;
            int chunkStart = Math.Max(startRow, 2);
            if (chunkStart > rowCount) return (0, rowCount - 1, true);
            int chunkEnd = Math.Min(chunkStart + chunkSize - 1, rowCount);

            var values = (object[,])ws.Cells[chunkStart, 1, chunkEnd, colCount].Value;
            int inserted;

            if (sheetKind == "YpProject")
            {
                // On the first chunk of the file, wipe existing rows for this fiscal year
                // before inserting the fresh data (per-year replace, not a full table truncate).
                if (chunkStart == 2)
                    await _budgetRepository.DeleteYpProjectByYearAsync(financialYear);

                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= colCount; c++)
                {
                    var h = ws.Cells[1, c].Text.Trim();
                    if (!string.IsNullOrEmpty(h) && !headerMap.ContainsKey(h))
                        headerMap[h] = c - 1;
                }
                int ColIdx(string name, int fallbackCol1Based) =>
                    headerMap.TryGetValue(name, out int ci) ? ci : fallbackCol1Based - 1;
                string Col(int r, string name, int fallbackCol1Based)
                    => CellStr(values, r, ColIdx(name, fallbackCol1Based));
                decimal ColD(int r, string name, int fallbackCol1Based)
                    => CellDecimal(values, r, ColIdx(name, fallbackCol1Based));
                int ColI(int r, string name, int fallbackCol1Based)
                    => CellInt(values, r, ColIdx(name, fallbackCol1Based));

                var records = new List<YpProject>(chunkEnd - chunkStart + 1);
                for (int row = chunkStart; row <= chunkEnd; row++)
                {
                    int r = row - chunkStart;
                    try
                    {
                        var fyVal = ColI(r, "FiscalYear", 1);
                        records.Add(new YpProject
                        {
                            FiscalYear          = fyVal > 0 ? fyVal : financialYear,
                            ParentProject       = Col(r, "ParentProject",       2),
                            ProjectID           = Col(r, "ProjectID",          3),
                            WBSElement          = Col(r, "WBSElement",         4),
                            ProfitCenter        = Col(r, "ProfitCenter",       5),
                            CostCenter          = Col(r, "CostCenter",         6),
                            MinistryDept        = Col(r, "MinistryDept",       7),
                            AttachedDept        = Col(r, "AttachedDept",       8),
                            DetailFunctionCode  = ColI(r, "DetailFunctionCode", 9),
                            OriginalBudget      = ColD(r, "OriginalBudget",     10),
                            SupplementaryBudget = ColD(r, "SupplementaryBudget",11),
                            Surrender           = ColD(r, "Surrender",         12),
                            ReappPlus           = ColD(r, "ReappPlus",         13),
                            ReappMinus          = ColD(r, "ReappMinus",        14),
                            FinalBudget         = ColD(r, "FinalBudget",       15),
                            ProgReleases        = ColD(r, "ProgReleases",      16),
                            InterimDateExp      = ColD(r, "InterimDateExp",    17),
                            ProgressiveExp      = ColD(r, "ProgressiveExp",    18),
                            Balance             = ColD(r, "Balance",           19),
                            SectorID            = Col(r, "SectorID",           20),
                            SubSect             = Col(r, "SubSect",            21),
                            Fund                = Col(r, "Fund",               22),
                            JulyExp             = ColD(r, "JulyExp",           23),
                            AugustExp           = ColD(r, "AugustExp",         24),
                            SeptemberExp        = ColD(r, "SeptemberExp",      25),
                            OctoberExp          = ColD(r, "OctoberExp",        26),
                            NovemberExp         = ColD(r, "NovemberExp",       27),
                            DecemberExp         = ColD(r, "DecemberExp",       28),
                            JanuaryExp          = ColD(r, "JanuaryExp",        29),
                            FebruaryExp         = ColD(r, "FebruaryExp",       30),
                            MarchExp            = ColD(r, "MarchExp",          31),
                            AprilExp            = ColD(r, "AprilExp",          32),
                            MayExp              = ColD(r, "MayExp",            33),
                            JuneExp             = ColD(r, "JuneExp",           34),
                            ProjectDescription  = Col(r, "ProjectDescription", 35)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("YpProject parse error row {Row}: {Msg}", row, ex.Message);
                    }
                }

                if (records.Count > 0)
                    await _budgetRepository.InsertYpProjectAsync(records);
                inserted = records.Count;
            }
            else if (sheetKind == "BER")
            {
                var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
                for (int c = 1; c <= colCount; c++)
                {
                    var h = ws.Cells[1, c].Text.Trim();
                    if (!string.IsNullOrEmpty(h) && !headerMap.ContainsKey(h))
                        headerMap[h] = c - 1;
                }
                int ColIdx(string name, int fallbackCol1Based) =>
                    headerMap.TryGetValue(name, out int ci) ? ci : fallbackCol1Based - 1;
                string Col(int r, string name, int fallbackCol1Based)
                    => CellStr(values, r, ColIdx(name, fallbackCol1Based));
                decimal ColD(int r, string name, int fallbackCol1Based)
                    => CellDecimal(values, r, ColIdx(name, fallbackCol1Based));
                int ColI(int r, string name, int fallbackCol1Based)
                    => CellInt(values, r, ColIdx(name, fallbackCol1Based));

                var records = new List<BERData>(chunkEnd - chunkStart + 1);
                for (int row = chunkStart; row <= chunkEnd; row++)
                {
                    int r = row - chunkStart;
                    try
                    {
                        var fyVal = ColI(r, "FinancialYear", 1);
                        records.Add(new BERData
                        {
                            ImportId              = importId,
                            FinancialYear         = fyVal > 0 ? fyVal : financialYear,
                            FundCode              = Col(r, "FundCode",              2),
                            FundCenterCode        = Col(r, "FundCenterCode",        3),
                            FundCenterDescription = Col(r, "FundCenterDescription", 4),
                            MinistryDepartment    = Col(r, "MinistryDepartment",    5),
                            AttachedDepartment    = Col(r, "AttachedDepartment",    6),
                            ProfitCenter          = Col(r, "ProfitCenter",          7),
                            SubDetailFunction     = Col(r, "SubDetailFunction",     8),
                            DetailObject          = Col(r, "DetailObject",          9),
                            BudgetGrant           = ColD(r, "BudgetGrant",          10),
                            SupplementaryGrant    = ColD(r, "SupplementaryGrant",   11),
                            ReappropriationPlus   = ColD(r, "ReappropriationPlus",  12),
                            ReappropriationMinus  = ColD(r, "ReappropriationMinus", 13),
                            TSSSurrender          = ColD(r, "TSSSurrender",         14),
                            FinalGrant            = ColD(r, "FinalGrant",           15),
                            Releases              = ColD(r, "Releases",             16),
                            JulExp                = ColD(r, "JulExp",               17),
                            AugExp                = ColD(r, "AugExp",               18),
                            SepExp                = ColD(r, "SepExp",               19),
                            OctExp                = ColD(r, "OctExp",               20),
                            NovExp                = ColD(r, "NovExp",               21),
                            DecExp                = ColD(r, "DecExp",               22),
                            JanExp                = ColD(r, "JanExp",               23),
                            FebExp                = ColD(r, "FebExp",               24),
                            MarExp                = ColD(r, "MarExp",               25),
                            AprExp                = ColD(r, "AprExp",               26),
                            MayExp                = ColD(r, "MayExp",               27),
                            JunExp                = ColD(r, "JunExp",               28),
                            ActualExpenditure     = ColD(r, "ActualExpenditure",    29),
                            FinalGrantBalance     = ColD(r, "FinalGrantBalance",    30)
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("BER parse error row {Row}: {Msg}", row, ex.Message);
                    }
                }

                if (records.Count > 0)
                    await _budgetRepository.InsertBERDataAsync(records);
                inserted = records.Count;
            }
            else
            {
                var records = new List<OriginalBudget>(chunkEnd - chunkStart + 1);
                for (int row = chunkStart; row <= chunkEnd; row++)
                {
                    int r = row - chunkStart;
                    try
                    {
                        records.Add(new OriginalBudget
                        {
                            FinancialYear = CellInt(values, r, 0) is int fy && fy > 0 ? fy : financialYear,
                            DocumentNumber = CellStr(values, r, 1),
                            DepartmentCode = CellStr(values, r, 2),
                            BudgetType = CellStr(values, r, 3),
                            FundCode = CellStr(values, r, 4),
                            FundDescription = CellStr(values, r, 5),
                            CostCenterCode = CellStr(values, r, 6),
                            SNECode = CellStr(values, r, 7),
                            CommitmentItem = CellStr(values, r, 8),
                            SubHead = CellStr(values, r, 9),
                            BPS = CellStr(values, r, 10),
                            DesignationCode = CellStr(values, r, 11),
                            Posts = CellInt(values, r, 12),
                            OriginalBudgetAmount = CellDecimal(values, r, 13),
                            RevisedBudgetAmount = CellDecimal(values, r, 14),
                            DesignationDescription = CellStr(values, r, 15),
                            Description = CellStr(values, r, 16),
                            PostingDate = CellDate(values, r, 17),
                            UserName = CellStr(values, r, 18),
                            FunctionCode = CellStr(values, r, 19),
                            Type = CellStr(values, r, 20),
                            ImportId = importId
                        });
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning("Error parsing row {Row}: {Message}", row, ex.Message);
                    }
                }

                if (records.Count > 0)
                    await _budgetRepository.InsertOriginalBudgetAsync(records);
                inserted = records.Count;
            }

            bool done = chunkEnd >= rowCount;
            await _budgetRepository.UpdateImportHeartbeatAsync(importId, Math.Min(chunkEnd - 1, rowCount - 1));

            return (inserted, rowCount - 1, done);
        }

        public async Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add(sheetName);

            if (data.Count == 0)
            {
                ws.Cells[1, 1].Value = "No data available";
                return await Task.FromResult(package.GetAsByteArray());
            }

            // Get properties
            var properties = typeof(T).GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanRead && !p.GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.Schema.NotMappedAttribute), false).Any())
                .ToList();

            // Headers
            for (int i = 0; i < properties.Count; i++)
            {
                var displayName = properties[i].GetCustomAttributes(typeof(System.ComponentModel.DataAnnotations.DisplayAttribute), false)
                    .Cast<System.ComponentModel.DataAnnotations.DisplayAttribute>()
                    .FirstOrDefault()?.Name ?? properties[i].Name;
                ws.Cells[1, i + 1].Value = displayName;
                ws.Cells[1, i + 1].Style.Font.Bold = true;
                ws.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            // Data
            for (int row = 0; row < data.Count; row++)
            {
                for (int col = 0; col < properties.Count; col++)
                {
                    var value = properties[col].GetValue(data[row]);
                    ws.Cells[row + 2, col + 1].Value = value;
                }
            }

            ws.Cells.AutoFitColumns();
            return await Task.FromResult(package.GetAsByteArray());
        }

        public async Task<byte[]> ExportOriginalBudgetAsync(FilterViewModel filters)
        {
            var pagination = new PaginationViewModel { PageNumber = 1, PageSize = int.MaxValue };
            var result = await _budgetRepository.GetOriginalBudgetAsync(filters, pagination);
            return await ExportToExcelAsync(result.Data, "Original Budget");
        }

        public async Task<byte[]> ExportBERDataAsync(FilterViewModel filters)
        {
            var pagination = new PaginationViewModel { PageNumber = 1, PageSize = int.MaxValue };
            var result = await _budgetRepository.GetBERDataAsync(filters, pagination);
            return await ExportToExcelAsync(result.Data, "BER Data");
        }

        public async Task<byte[]> ExportReportAsync(string reportType, FilterViewModel filters)
        {
            return reportType.ToLower() switch
            {
                "fund" => await ExportFundWiseAsync(filters),
                "department" => await ExportToExcelAsync(await _budgetRepository.GetDepartmentWiseReportAsync(filters.FinancialYear, filters.DepartmentCode, filters.FundCode), "Department Wise Report"),
                "costcenter" => await ExportCostCenterWiseAsync(filters),
                "function" => await ExportToExcelAsync(await _budgetRepository.GetFunctionWiseReportAsync(filters.FinancialYear, filters.Keyword), "Function Wise Report"),
                "object" => await ExportObjectWiseReportAsync(filters),
                _ => await ExportToExcelAsync(new List<object>(), "Report")
            };
        }

        public async Task<byte[]> ExportObjectWiseReportAsync(FilterViewModel filters)
        {
            var ccList  = filters.CostCenterCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var objList = filters.ObjectCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            var data = await _budgetRepository.GetObjectWiseGroupedAsync(
                filters.FinancialYear, filters.Sector, filters.DepartmentCode, filters.FundCode,
                ccList.Count  > 0 ? null : filters.CostCenterCode,
                objList.Count > 0 ? null : filters.Keyword,
                ccList.Count  > 0 ? ccList  : null,
                objList.Count > 0 ? objList : null);

            bool showSector = filters.ShowSector;
            bool showDept   = filters.ShowDept;
            bool showFund   = filters.ShowFund;
            bool showCC     = filters.ShowCC;

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Object Wise Report");

            const int totalCols = 14;
            var navy    = System.Drawing.Color.FromArgb(11, 43, 82);
            var white   = System.Drawing.Color.White;
            var sectorBg  = System.Drawing.Color.FromArgb(0, 70, 127);
            var deptBg    = System.Drawing.Color.FromArgb(14, 99, 160);
            var fundBg    = System.Drawing.Color.FromArgb(41, 128, 185);
            var ccBg      = System.Drawing.Color.FromArgb(30, 116, 173);   // blue for cost center (was teal)
            var subtotBg  = System.Drawing.Color.FromArgb(214, 229, 247);
            var ccSubBg   = System.Drawing.Color.FromArgb(206, 226, 244);
            var grandBg   = System.Drawing.Color.FromArgb(11, 43, 82);
            var altGrey   = System.Drawing.Color.FromArgb(248, 250, 255);

            string MergeRange(int r, int c1, int c2) => $"{ExcelCellAddress.GetColumnLetter(c1)}{r}:{ExcelCellAddress.GetColumnLetter(c2)}{r}";
            void StyleBand(int r, System.Drawing.Color bg, System.Drawing.Color fg, float sz, bool bold)
            {
                ws.Cells[MergeRange(r, 1, totalCols)].Merge = true;
                for (int c = 1; c <= totalCols; c++)
                {
                    ws.Cells[r, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[r, c].Style.Fill.BackgroundColor.SetColor(bg);
                    ws.Cells[r, c].Style.Font.Color.SetColor(fg);
                    ws.Cells[r, c].Style.Font.Bold = bold;
                    ws.Cells[r, c].Style.Font.Size = sz;
                }
            }
            void WriteNumRow(int r, decimal al, decimal bg2, decimal rl, decimal supp, decimal rp, decimal rm, decimal sur, decimal fg2, decimal ex, decimal bl, System.Drawing.Color fillColor, bool bold)
            {
                decimal relPct  = al  > 0 ? rl / al  : 0;
                decimal utilPct = fg2 > 0 ? ex / fg2 : 0;
                ws.Cells[r, 3].Value = Math.Round(al);  ws.Cells[r, 4].Value = Math.Round(bg2);
                ws.Cells[r, 5].Value = Math.Round(rl);  ws.Cells[r, 6].Value = relPct;
                ws.Cells[r, 7].Value = Math.Round(supp); ws.Cells[r, 8].Value = Math.Round(rp);
                ws.Cells[r, 9].Value = Math.Round(rm);   ws.Cells[r, 10].Value = Math.Round(sur);
                ws.Cells[r, 11].Value = Math.Round(fg2);
                ws.Cells[r, 12].Value = Math.Round(ex); ws.Cells[r, 13].Value = utilPct;
                ws.Cells[r, 14].Value = Math.Round(bl);
                for (int c = 3; c <= 5; c++) ws.Cells[r, c].Style.Numberformat.Format = "#,##0";
                ws.Cells[r, 6].Style.Numberformat.Format = "0.0%";
                for (int c = 7; c <= 12; c++) ws.Cells[r, c].Style.Numberformat.Format = "#,##0";
                ws.Cells[r, 13].Style.Numberformat.Format = "0.0%";
                ws.Cells[r, 14].Style.Numberformat.Format = "#,##0";
                for (int c = 1; c <= totalCols; c++)
                {
                    ws.Cells[r, c].Style.Font.Bold = bold;
                    ws.Cells[r, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[r, c].Style.Fill.BackgroundColor.SetColor(fillColor);
                    if (c >= 3) ws.Cells[r, c].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                }
            }

            int row = 1;
            // ── Title ─────────────────────────────────────────────────────────────
            StyleBand(row, navy, white, 14, true);
            ws.Cells[row, 1].Value = "Finance Department Punjab";
            row++;
            StyleBand(row, navy, white, 11, false);
            ws.Cells[row, 1].Value = "Object Wise Budget Report" + (filters.FinancialYear.HasValue ? $"  —  FY {filters.FinancialYear}" : "");
            row++;
            StyleBand(row, System.Drawing.Color.FromArgb(40, 60, 100), System.Drawing.Color.FromArgb(200, 215, 240), 9, false);
            ws.Cells[row, 1].Value = $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}   |   Sector: {filters.Sector ?? "All"}   |   Dept: {filters.DepartmentCode ?? "All"}   |   Fund: {filters.FundCode ?? "All"}   |   CC: {filters.CostCenterCode ?? "All"}   |   Object: {filters.Keyword ?? "All"}";
            row++;

            // ── Column headers ─────────────────────────────────────────────────────
            var headers = new[] { "Object Code", "Object Description", "Allocation", "Budget Grant", "Releases", "% Release", "Supplementary", "Reapp (+)", "Reapp (-)", "Surrender", "Final Budget", "Expenditure", "% Util", "Balance" };
            for (int c = 1; c <= totalCols; c++)
            {
                var cell = ws.Cells[row, c];
                cell.Value = headers[c - 1];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(navy);
                cell.Style.Font.Color.SetColor(white);
                cell.Style.HorizontalAlignment = c <= 2 ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Left : OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
                cell.Style.WrapText = true;
            }
            ws.Row(row).Height = 32;
            row++;

            // ── Hierarchical data ──────────────────────────────────────────────────
            decimal gAl = 0, gBg2 = 0, gRl = 0, gSupp = 0, gRp = 0, gRm = 0, gSur = 0, gFg = 0, gEx = 0, gBl = 0;

            var bySector = data.GroupBy(d => showSector ? d.Sector : string.Empty).OrderBy(g => g.Key);
            foreach (var sGrp in bySector)
            {
                if (showSector)
                {
                    StyleBand(row, sectorBg, white, 10, true);
                    ws.Cells[row, 1].Value = $"  SECTOR:  {sGrp.Key}";
                    ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    row++;
                }

                decimal sAl=0, sBg=0, sRl=0, sSupp=0, sRp=0, sRm=0, sSur=0, sFg=0, sEx=0, sBl=0;
                var byDept = sGrp.GroupBy(d => showDept ? d.Department : string.Empty).OrderBy(g => g.Key);
                foreach (var dGrp in byDept)
                {
                    if (showDept)
                    {
                        StyleBand(row, deptBg, white, 9.5f, true);
                        ws.Cells[row, 1].Value = $"    Department:  {dGrp.Key}";
                        ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                        row++;
                    }

                    decimal dAl=0, dBg=0, dRl=0, dSupp=0, dRp=0, dRm=0, dSur=0, dFg=0, dEx=0, dBl=0;
                    var byFund = dGrp.GroupBy(d => new { FundCode = showFund ? d.FundCode : string.Empty, d.FundDescription }).OrderBy(g => g.Key.FundCode);
                    foreach (var fGrp in byFund)
                    {
                        if (showFund)
                        {
                            StyleBand(row, fundBg, white, 9, false);
                            ws.Cells[row, 1].Value = $"      Fund:  {fGrp.Key.FundCode}  {fGrp.Key.FundDescription}";
                            ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                            row++;
                        }

                        decimal fAl=0, fBg=0, fRl=0, fSupp=0, fRp=0, fRm=0, fSur=0, fFg=0, fEx=0, fBl=0;
                        var byCC = fGrp.GroupBy(d => new { CostCenterCode = showCC ? d.CostCenterCode : string.Empty, d.CostCenterDescription }).OrderBy(g => g.Key.CostCenterCode);
                        foreach (var cGrp in byCC)
                        {
                            if (showCC)
                            {
                                StyleBand(row, ccBg, white, 8.5f, false);
                                ws.Cells[row, 1].Value = $"        Cost Center:  {cGrp.Key.CostCenterCode}" +
                                    (string.IsNullOrEmpty(cGrp.Key.CostCenterDescription) ? "" : $"  —  {cGrp.Key.CostCenterDescription}");
                                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                                row++;
                            }

                            decimal ccAl=0, ccBg2=0, ccRl=0, ccSupp=0, ccRp=0, ccRm=0, ccSur=0, ccFg=0, ccEx=0, ccBl=0;
                            bool alt = false;
                            // Aggregate by DetailObject so items sharing the same object code (which can happen
                            // when Cost Center/Fund/Dept/Sector grouping levels are collapsed) are summed, not duplicated.
                            var aggregatedItems = cGrp
                                .GroupBy(x => x.DetailObject)
                                .Select(g => new ObjectWiseGroupedViewModel
                                {
                                    DetailObject = g.Key,
                                    ObjectDescription = g.Select(x => x.ObjectDescription).FirstOrDefault(d => !string.IsNullOrEmpty(d)),
                                    TotalAllocatedBudget = g.Sum(x => x.TotalAllocatedBudget),
                                    TotalBudgetGrant = g.Sum(x => x.TotalBudgetGrant),
                                    TotalReleases = g.Sum(x => x.TotalReleases),
                                    TotalSupplementaryGrant = g.Sum(x => x.TotalSupplementaryGrant),
                                    TotalReappropriationPlus = g.Sum(x => x.TotalReappropriationPlus),
                                    TotalReappropriationMinus = g.Sum(x => x.TotalReappropriationMinus),
                                    TotalSurrender = g.Sum(x => x.TotalSurrender),
                                    TotalFinalGrant = g.Sum(x => x.TotalFinalGrant),
                                    TotalActualExpenditure = g.Sum(x => x.TotalActualExpenditure),
                                    TotalBalance = g.Sum(x => x.TotalBalance)
                                })
                                .OrderByDescending(x => x.TotalAllocatedBudget);
                            foreach (var item in aggregatedItems)
                            {
                                var rowFill = alt ? altGrey : white;
                                ws.Cells[row, 1].Value = item.DetailObject;
                                ws.Cells[row, 2].Value = item.ObjectDescription ?? string.Empty;
                                WriteNumRow(row, item.TotalAllocatedBudget, item.TotalBudgetGrant, item.TotalReleases,
                                    item.TotalSupplementaryGrant, item.TotalReappropriationPlus, item.TotalReappropriationMinus, item.TotalSurrender,
                                    item.TotalFinalGrant, item.TotalActualExpenditure, item.TotalBalance, rowFill, false);
                                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                                ws.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                                ccAl += item.TotalAllocatedBudget; ccBg2 += item.TotalBudgetGrant;
                                ccRl += item.TotalReleases; ccFg += item.TotalFinalGrant;
                                ccSupp += item.TotalSupplementaryGrant; ccRp += item.TotalReappropriationPlus;
                                ccRm += item.TotalReappropriationMinus; ccSur += item.TotalSurrender;
                                ccEx += item.TotalActualExpenditure; ccBl += item.TotalBalance;
                                alt = !alt; row++;
                            }
                            if (showCC)
                            {
                                // CC subtotal
                                ws.Cells[row, 1, row, 2].Merge = true;
                                ws.Cells[row, 1].Value = $"        Subtotal — CC: {cGrp.Key.CostCenterCode}";
                                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                                WriteNumRow(row, ccAl, ccBg2, ccRl, ccSupp, ccRp, ccRm, ccSur, ccFg, ccEx, ccBl, ccSubBg, true);
                                row++;
                            }
                            fAl += ccAl; fBg += ccBg2; fRl += ccRl; fFg += ccFg; fEx += ccEx; fBl += ccBl;
                            fSupp += ccSupp; fRp += ccRp; fRm += ccRm; fSur += ccSur;
                        }
                        if (showFund)
                        {
                            ws.Cells[row, 1, row, 2].Merge = true;
                            ws.Cells[row, 1].Value = $"      Subtotal — Fund: {fGrp.Key.FundCode}";
                            ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                            WriteNumRow(row, fAl, fBg, fRl, fSupp, fRp, fRm, fSur, fFg, fEx, fBl, subtotBg, true);
                            row++;
                        }
                        dAl += fAl; dBg += fBg; dRl += fRl; dFg += fFg; dEx += fEx; dBl += fBl;
                        dSupp += fSupp; dRp += fRp; dRm += fRm; dSur += fSur;
                    }
                    if (showDept)
                    {
                        ws.Cells[row, 1, row, 2].Merge = true;
                        ws.Cells[row, 1].Value = $"    Subtotal — Dept: {dGrp.Key}";
                        ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                        WriteNumRow(row, dAl, dBg, dRl, dSupp, dRp, dRm, dSur, dFg, dEx, dBl, System.Drawing.Color.FromArgb(185, 210, 240), true);
                        row++;
                    }
                    sAl += dAl; sBg += dBg; sRl += dRl; sFg += dFg; sEx += dEx; sBl += dBl;
                    sSupp += dSupp; sRp += dRp; sRm += dRm; sSur += dSur;
                }
                if (showSector)
                {
                    ws.Cells[row, 1, row, 2].Merge = true;
                    ws.Cells[row, 1].Value = $"  Subtotal — Sector: {sGrp.Key}";
                    ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    WriteNumRow(row, sAl, sBg, sRl, sSupp, sRp, sRm, sSur, sFg, sEx, sBl, System.Drawing.Color.FromArgb(155, 190, 230), true);
                    row++;
                }
                gAl += sAl; gBg2 += sBg; gRl += sRl; gFg += sFg; gEx += sEx; gBl += sBl;
                gSupp += sSupp; gRp += sRp; gRm += sRm; gSur += sSur;
            }

            // ── Grand total ───────────────────────────────────────────────────────
            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Value = "GRAND TOTAL";
            ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            WriteNumRow(row, gAl, gBg2, gRl, gSupp, gRp, gRm, gSur, gFg, gEx, gBl, grandBg, true);
            for (int c = 1; c <= totalCols; c++) ws.Cells[row, c].Style.Font.Color.SetColor(white);

            ws.Cells.AutoFitColumns();
            ws.Column(2).Width = 40;
            ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            return await Task.FromResult(package.GetAsByteArray());
        }

        private async Task<byte[]> ExportFundWiseAsync(FilterViewModel filters)
        {
            var data = await _budgetRepository.GetFundWiseReportAsync(filters.FinancialYear, filters.FundCode, filters.Keyword, filters.Sector);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Fund Wise");

            var headerFill = System.Drawing.Color.FromArgb(11, 43, 82);
            var headerText = System.Drawing.Color.White;
            var totalFill  = System.Drawing.Color.FromArgb(220, 230, 245);
            var altFill    = System.Drawing.Color.FromArgb(248, 250, 255);

            int row = 1;
            ws.Cells[row, 1].Value = "Fund Wise Budget Report";
            ws.Cells[row, 1, row, 13].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row++;

            var yrLabel = filters.FinancialYear.HasValue ? filters.FinancialYear.ToString() : "All Years";
            var fnLabel = string.IsNullOrWhiteSpace(filters.FundCode) ? "All Funds" : filters.FundCode;
            var scLabel = string.IsNullOrWhiteSpace(filters.Sector) ? "All Sectors" : filters.Sector;
            ws.Cells[row, 1].Value = $"Year: {yrLabel}   |   Sector: {scLabel}   |   Fund: {fnLabel}";
            ws.Cells[row, 1, row, 13].Merge = true;
            ws.Cells[row, 1].Style.Font.Italic = true;
            row += 2;

            string[] headers = { "Grant", "Allocation", "Budget Grant", "Releases", "% Release", "Supplementary", "Reapp (+)", "Reapp (-)", "Surrender", "Final Budget", "Expenditure", "% Util", "Balance" };
            for (int c = 1; c <= headers.Length; c++)
            {
                ws.Cells[row, c].Value = headers[c - 1];
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Font.Color.SetColor(headerText);
                ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(headerFill);
                ws.Cells[row, c].Style.HorizontalAlignment = (c >= 2) ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Right : OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                ws.Cells[row, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }
            row++;

            long totAl = 0, totGr = 0, totRl = 0, totEx = 0, totBl = 0;
            long totSupp = 0, totReappPlus = 0, totReappMinus = 0, totSurr = 0, totFinalGrant = 0;
            bool alt = false;
            foreach (var r in data)
            {
                var relPct  = r.TotalAllocatedBudget > 0 ? Math.Round((double)r.TotalReleases / (double)r.TotalAllocatedBudget * 100, 1) : 0.0;
                var utilPct = r.TotalFinalGrant       > 0 ? Math.Round((double)r.TotalActualExpenditure / (double)r.TotalFinalGrant * 100, 1) : 0.0;

                ws.Cells[row, 1].Value = string.IsNullOrWhiteSpace(r.FundDes) ? r.FundCode : $"{r.FundCode} - {r.FundDes}";
                ws.Cells[row, 2].Value = (long)r.TotalAllocatedBudget;
                ws.Cells[row, 3].Value = (long)r.TotalBudgetGrant;
                ws.Cells[row, 4].Value = (long)r.TotalReleases;
                ws.Cells[row, 5].Value = relPct / 100;
                ws.Cells[row, 6].Value = (long)r.TotalSupplementaryGrant;
                ws.Cells[row, 7].Value = (long)r.TotalReappropriationPlus;
                ws.Cells[row, 8].Value = (long)r.TotalReappropriationMinus;
                ws.Cells[row, 9].Value = (long)r.TotalSurrender;
                ws.Cells[row, 10].Value = (long)r.TotalFinalGrant;
                ws.Cells[row, 11].Value = (long)r.TotalActualExpenditure;
                ws.Cells[row, 12].Value = utilPct / 100;
                ws.Cells[row, 13].Value = (long)r.TotalBalance;

                ws.Cells[row, 2, row, 4].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 6, row, 11].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 13].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 5].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 12].Style.Numberformat.Format = "0.0%";
                for (int c = 2; c <= 13; c++) ws.Cells[row, c].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                if (alt)
                {
                    for (int c = 1; c <= 13; c++)
                    {
                        ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(altFill);
                    }
                }
                alt = !alt;

                totAl += (long)r.TotalAllocatedBudget;
                totGr += (long)r.TotalBudgetGrant;
                totRl += (long)r.TotalReleases;
                totSupp += (long)r.TotalSupplementaryGrant;
                totReappPlus += (long)r.TotalReappropriationPlus;
                totReappMinus += (long)r.TotalReappropriationMinus;
                totSurr += (long)r.TotalSurrender;
                totFinalGrant += (long)r.TotalFinalGrant;
                totEx += (long)r.TotalActualExpenditure;
                totBl += (long)r.TotalBalance;
                row++;
            }

            var tRelPct  = totAl > 0 ? Math.Round((double)totRl / totAl * 100, 1) : 0.0;
            var tUtilPct = totFinalGrant > 0 ? Math.Round((double)totEx / totFinalGrant * 100, 1) : 0.0;
            ws.Cells[row, 1].Value = "Total";
            ws.Cells[row, 2].Value = totAl;
            ws.Cells[row, 3].Value = totGr;
            ws.Cells[row, 4].Value = totRl;
            ws.Cells[row, 5].Value = tRelPct / 100;
            ws.Cells[row, 6].Value = totSupp;
            ws.Cells[row, 7].Value = totReappPlus;
            ws.Cells[row, 8].Value = totReappMinus;
            ws.Cells[row, 9].Value = totSurr;
            ws.Cells[row, 10].Value = totFinalGrant;
            ws.Cells[row, 11].Value = totEx;
            ws.Cells[row, 12].Value = tUtilPct / 100;
            ws.Cells[row, 13].Value = totBl;
            ws.Cells[row, 2, row, 4].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 6, row, 11].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 13].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 5].Style.Numberformat.Format = "0.0%";
            ws.Cells[row, 12].Style.Numberformat.Format = "0.0%";
            for (int c = 1; c <= 13; c++)
            {
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(totalFill);
                ws.Cells[row, c].Style.HorizontalAlignment = (c >= 2) ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Right : OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                ws.Cells[row, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }

            ws.Cells.AutoFitColumns();
            ws.Column(1).Width = 40;
            ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            return await Task.FromResult(package.GetAsByteArray());
        }

        private async Task<byte[]> ExportCostCenterWiseAsync(FilterViewModel filters)
        {
            var ccList  = filters.CostCenterCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var objList = filters.ObjectCodes.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            var data = await _budgetRepository.GetCostCenterWiseReportAsync(
                filters.FinancialYear,
                string.IsNullOrWhiteSpace(filters.FundCode) ? null : filters.FundCode,
                null,
                ccList.Count  > 0 ? ccList  : null,
                objList.Count > 0 ? objList : null,
                string.IsNullOrWhiteSpace(filters.Sector) ? null : filters.Sector,
                string.IsNullOrWhiteSpace(filters.DepartmentCode) ? null : filters.DepartmentCode);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Cost Center Wise");

            // ── Styles ─────────────────────────────────────────────────────────
            var headerFill  = System.Drawing.Color.FromArgb(11, 43, 82);
            var headerText  = System.Drawing.Color.White;
            var totalFill   = System.Drawing.Color.FromArgb(220, 230, 245);
            var altFill     = System.Drawing.Color.FromArgb(248, 250, 255);

            int row = 1;
            // Title
            ws.Cells[row, 1].Value = "Cost Center Wise Budget Report";
            ws.Cells[row, 1, row, 14].Merge = true;
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            row++;

            // Filter info
            var yrLabel = filters.FinancialYear.HasValue ? filters.FinancialYear.ToString() : "All Years";
            var fnLabel = string.IsNullOrWhiteSpace(filters.FundCode) ? "All Funds" : filters.FundCode;
            var ccLabel = ccList.Count > 0 ? string.Join(", ", ccList) : "All Cost Centers";
            var ojLabel = objList.Count > 0 ? string.Join(", ", objList) : "All Objects";
            ws.Cells[row, 1].Value = $"Year: {yrLabel}   |   Fund: {fnLabel}   |   Cost Center: {ccLabel}   |   Object Code: {ojLabel}";
            ws.Cells[row, 1, row, 14].Merge = true;
            ws.Cells[row, 1].Style.Font.Italic = true;
            row += 2;

            // Header row
            string[] headers = { "Cost Center Code", "Cost Center Description", "Alloc Budget", "Budget Grant", "Releases", "% Release", "Supplementary", "Reapp (+)", "Reapp (-)", "Surrender", "Final Budget", "Expenditure", "% Util", "Balance" };
            for (int c = 1; c <= headers.Length; c++)
            {
                ws.Cells[row, c].Value = headers[c - 1];
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Font.Color.SetColor(headerText);
                ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(headerFill);
                ws.Cells[row, c].Style.HorizontalAlignment = (c >= 3) ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Right : OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                ws.Cells[row, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }
            row++;

            // Data rows
            long totAl = 0, totGr = 0, totRl = 0, totEx = 0, totBl = 0;
            long totSupp = 0, totReappPlus = 0, totReappMinus = 0, totSurr = 0, totFinalGrant = 0;
            int dataStart = row;
            bool alt = false;
            foreach (var r in data)
            {
                var relPct  = r.TotalAllocatedBudget > 0 ? Math.Round((double)r.TotalReleases / (double)r.TotalAllocatedBudget * 100, 1) : 0.0;
                var utilPct = r.TotalBudgetGrant     > 0 ? Math.Round((double)r.TotalActualExpenditure / (double)r.TotalBudgetGrant * 100, 1) : 0.0;

                ws.Cells[row, 1].Value = r.FundCenterCode;
                ws.Cells[row, 2].Value = r.CostCenterDescription;
                ws.Cells[row, 3].Value = (long)r.TotalAllocatedBudget;
                ws.Cells[row, 4].Value = (long)r.TotalBudgetGrant;
                ws.Cells[row, 5].Value = (long)r.TotalReleases;
                ws.Cells[row, 6].Value = relPct / 100;
                ws.Cells[row, 7].Value = (long)r.TotalSupplementaryGrant;
                ws.Cells[row, 8].Value = (long)r.TotalReappropriationPlus;
                ws.Cells[row, 9].Value = (long)r.TotalReappropriationMinus;
                ws.Cells[row, 10].Value = (long)r.TotalSurrender;
                ws.Cells[row, 11].Value = (long)r.TotalFinalGrant;
                ws.Cells[row, 12].Value = (long)r.TotalActualExpenditure;
                ws.Cells[row, 13].Value = utilPct / 100;
                ws.Cells[row, 14].Value = (long)r.TotalBalance;

                ws.Cells[row, 3, row, 5].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 7, row, 12].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 14].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 6].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 13].Style.Numberformat.Format = "0.0%";
                for (int c = 3; c <= 14; c++) ws.Cells[row, c].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;

                if (alt)
                {
                    for (int c = 1; c <= 14; c++)
                    {
                        ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                        ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(altFill);
                    }
                }
                alt = !alt;

                totAl += (long)r.TotalAllocatedBudget;
                totGr += (long)r.TotalBudgetGrant;
                totRl += (long)r.TotalReleases;
                totSupp += (long)r.TotalSupplementaryGrant;
                totReappPlus += (long)r.TotalReappropriationPlus;
                totReappMinus += (long)r.TotalReappropriationMinus;
                totSurr += (long)r.TotalSurrender;
                totFinalGrant += (long)r.TotalFinalGrant;
                totEx += (long)r.TotalActualExpenditure;
                totBl += (long)r.TotalBalance;
                row++;
            }

            // Totals row
            var tRelPct  = totAl > 0 ? Math.Round((double)totRl / totAl * 100, 1) : 0.0;
            var tUtilPct = totGr > 0 ? Math.Round((double)totEx / totGr * 100, 1) : 0.0;
            ws.Cells[row, 1].Value = "Total";
            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 3].Value = totAl;
            ws.Cells[row, 4].Value = totGr;
            ws.Cells[row, 5].Value = totRl;
            ws.Cells[row, 6].Value = tRelPct / 100;
            ws.Cells[row, 7].Value = totSupp;
            ws.Cells[row, 8].Value = totReappPlus;
            ws.Cells[row, 9].Value = totReappMinus;
            ws.Cells[row, 10].Value = totSurr;
            ws.Cells[row, 11].Value = totFinalGrant;
            ws.Cells[row, 12].Value = totEx;
            ws.Cells[row, 13].Value = tUtilPct / 100;
            ws.Cells[row, 14].Value = totBl;
            ws.Cells[row, 3, row, 5].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 7, row, 12].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 14].Style.Numberformat.Format = "#,##0";
            ws.Cells[row, 6].Style.Numberformat.Format = "0.0%";
            ws.Cells[row, 13].Style.Numberformat.Format = "0.0%";
            for (int c = 1; c <= 14; c++)
            {
                ws.Cells[row, c].Style.Font.Bold = true;
                ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(totalFill);
                ws.Cells[row, c].Style.HorizontalAlignment = (c >= 3) ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Right : OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                ws.Cells[row, c].Style.Border.BorderAround(OfficeOpenXml.Style.ExcelBorderStyle.Thin);
            }

            ws.Cells.AutoFitColumns();
            ws.Column(2).Width = 40;
            ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            return await Task.FromResult(package.GetAsByteArray());
        }

        public async Task<byte[]> ExportCostCenterReportAsync(int? financialYear, string? fundCode, string? costCenterCode)
        {
            var data = await _budgetRepository.GetCostCenterReportAsync(financialYear, fundCode, costCenterCode);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Cost Center Report");

            var title = $"Cost Center Report{(financialYear.HasValue ? " - FY " + financialYear : "")}";
            ws.Cells[1, 1].Value = "Finance Department Punjab";
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[2, 1].Value = title;
            ws.Cells[2, 1].Style.Font.Size = 11;
            ws.Cells[3, 1].Value = $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            ws.Cells[3, 1].Style.Font.Size = 9;
            ws.Cells[3, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            ws.Cells["A1:I1"].Merge = true;
            ws.Cells["A2:I2"].Merge = true;
            ws.Cells["A3:I3"].Merge = true;

            // Filter summary row
            int filterRow = 4;
            if (fundCode != null) { ws.Cells[filterRow, 1].Value = $"Fund: {fundCode}"; filterRow++; }
            if (costCenterCode != null) { ws.Cells[filterRow, 1].Value = $"Cost Center: {costCenterCode}"; filterRow++; }

            int headerRow = filterRow + 1;
            var headers = new[] { "Financial Year", "Department", "Fund Code", "Cost Center", "Commitment Item", "Posts", "Original Budget (PKR)", "Revised Budget (PKR)", "Variance (PKR)" };
            for (int i = 0; i < headers.Length; i++)
            {
                var cell = ws.Cells[headerRow, i + 1];
                cell.Value = headers[i];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.FromArgb(13, 110, 253));
                cell.Style.Font.Color.SetColor(System.Drawing.Color.White);
                cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            }

            int dataRow = headerRow + 1;
            foreach (var row in data)
            {
                ws.Cells[dataRow, 1].Value = row.FinancialYear;
                ws.Cells[dataRow, 2].Value = row.DepartmentCode;
                ws.Cells[dataRow, 3].Value = row.FundCode;
                ws.Cells[dataRow, 4].Value = row.CostCenterCode;
                ws.Cells[dataRow, 5].Value = row.CommitmentItem;
                ws.Cells[dataRow, 6].Value = row.Posts;
                ws.Cells[dataRow, 7].Value = row.OriginalBudgetAmount;
                ws.Cells[dataRow, 8].Value = row.RevisedBudgetAmount;
                ws.Cells[dataRow, 9].Value = row.Variance;
                ws.Cells[dataRow, 7, dataRow, 9].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[dataRow, 7, dataRow, 9].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                if (row.Variance < 0)
                    ws.Cells[dataRow, 9].Style.Font.Color.SetColor(System.Drawing.Color.Red);
                else if (row.Variance > 0)
                    ws.Cells[dataRow, 9].Style.Font.Color.SetColor(System.Drawing.Color.Green);
                dataRow++;
            }

            // Grand Total row
            if (data.Count > 0)
            {
                ws.Cells[dataRow, 1, dataRow, 5].Merge = true;
                ws.Cells[dataRow, 1].Value = "GRAND TOTAL";
                ws.Cells[dataRow, 1].Style.Font.Bold = true;
                ws.Cells[dataRow, 6].Value = data.Sum(r => r.Posts);
                ws.Cells[dataRow, 7].Value = data.Sum(r => r.OriginalBudgetAmount);
                ws.Cells[dataRow, 8].Value = data.Sum(r => r.RevisedBudgetAmount);
                ws.Cells[dataRow, 9].Value = data.Sum(r => r.Variance);
                ws.Cells[dataRow, 6, dataRow, 9].Style.Font.Bold = true;
                ws.Cells[dataRow, 7, dataRow, 9].Style.Numberformat.Format = "#,##0.00";
                ws.Cells[dataRow, 1, dataRow, 9].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                ws.Cells[dataRow, 1, dataRow, 9].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
            }

            ws.Cells.AutoFitColumns();
            return await Task.FromResult(package.GetAsByteArray());
        }

        private static int ParseInt(string value)
        {
            return int.TryParse(value.Replace(",", "").Trim(), out int result) ? result : 0;
        }

        private static decimal ParseDecimal(string value)
        {
            return decimal.TryParse(value.Replace(",", "").Trim(), out decimal result) ? result : 0;
        }

        private static DateTime? ParseDate(string value)
        {
            if (DateTime.TryParse(value.Trim(), out DateTime result))
                return result;
            return null;
        }

        // ─── Helpers for reading from a bulk-loaded object[,] range (ws.Cells[...].Value) ───
        // Reading a whole used-range in one call (EPPlus returns object[,]) is orders of
        // magnitude faster than accessing ExcelRange.Text cell-by-cell, especially under the
        // constrained CPU shares typical of shared/hosted environments (e.g. MonsterASP).
        private static string CellStr(object[,] values, int row, int col)
        {
            var v = values[row, col];
            if (v == null) return string.Empty;
            return Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture)?.Trim() ?? string.Empty;
        }

        private static int CellInt(object[,] values, int row, int col)
        {
            var v = values[row, col];
            if (v == null) return 0;
            if (v is double d) return (int)d;
            if (v is int i) return i;
            return ParseInt(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static decimal CellDecimal(object[,] values, int row, int col)
        {
            var v = values[row, col];
            if (v == null) return 0;
            if (v is double d) return (decimal)d;
            if (v is decimal dec) return dec;
            if (v is int i) return i;
            return ParseDecimal(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        private static DateTime? CellDate(object[,] values, int row, int col)
        {
            var v = values[row, col];
            if (v == null) return null;
            if (v is DateTime dt) return dt;
            if (v is double d) return DateTime.FromOADate(d);
            return ParseDate(Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty);
        }

        // ─── PnD File Import ────────────────────────────────────────────────────────
        // Always reads the FIRST worksheet, regardless of its name, and maps columns
        // by header name (row 1) — not dependent on a specific sheet name or column order.
        public async Task<int> ImportPnDFileAsync(IFormFile file)
        {
            using var stream = new MemoryStream();
            await file.CopyToAsync(stream);
            using var package = new ExcelPackage(stream);

            if (package.Workbook.Worksheets.Count == 0)
                throw new InvalidOperationException("The uploaded workbook has no worksheets.");

            var ws = package.Workbook.Worksheets[0];
            int rowCount = ws.Dimension?.Rows ?? 0;
            int colCount = ws.Dimension?.Columns ?? 0;
            if (rowCount < 2) return 0;

            // Normalize a header/name by stripping anything that isn't a letter or digit and lower-casing it.
            // This makes matching resilient to spacing, underscores, punctuation, and casing differences
            // between the uploaded workbook's headers and the expected property names
            // (e.g. "SR No", "SR_No", "sr no" and "SRNo" all normalize to "srno").
            static string Normalize(string s)
            {
                var chars = s.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray();
                return new string(chars);
            }

            // Build header-name → 1-based column-index map (normalized, case/space/punctuation-insensitive)
            var headerMap = new Dictionary<string, int>();
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cells[1, c].Text.Trim();
                if (string.IsNullOrEmpty(h)) continue;
                var key = Normalize(h);
                if (!string.IsNullOrEmpty(key) && !headerMap.ContainsKey(key))
                    headerMap[key] = c;
            }

            string? Str(int row, string name) =>
                headerMap.TryGetValue(Normalize(name), out int c) ? NullIfEmpty(ws.Cells[row, c].Text.Trim()) : null;

            int? Int(int row, string name)
            {
                if (!headerMap.TryGetValue(Normalize(name), out int c)) return null;
                var t = ws.Cells[row, c].Text.Trim();
                return int.TryParse(t, out int i) ? i : null;
            }

            decimal? Dec(int row, string name)
            {
                if (!headerMap.TryGetValue(Normalize(name), out int c)) return null;
                var t = ws.Cells[row, c].Text.Trim();
                return decimal.TryParse(t, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal d) ? d : null;
            }

            var records = new List<PnDFile>(rowCount - 1);
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    // Skip fully blank rows
                    bool anyValue = false;
                    for (int c = 1; c <= colCount; c++)
                    {
                        if (!string.IsNullOrWhiteSpace(ws.Cells[row, c].Text)) { anyValue = true; break; }
                    }
                    if (!anyValue) continue;

                    var record = new PnDFile
                    {
                        SRNo = Str(row, "SRNo"),
                        GSNo = Int(row, "GSNo"),
                        SchemeNo = Str(row, "SchemeNo"),
                        FullSchemeNo = Str(row, "FullSchemeNo"),
                        LoNumberCapital = Str(row, "LoNumberCapital"),
                        LoNumberRevenue = Str(row, "LoNumberRevenue"),
                        Name = Str(row, "Name"),
                        District = Str(row, "District"),
                        DistrictName = Str(row, "DistrictName"),
                        Scope = Str(row, "Scope"),
                        Objectives = Str(row, "Objectives"),
                        IssueRemarks = Str(row, "IssueRemarks"),
                        GestationPeriod = Str(row, "GestationPeriod"),
                        ApprovalStatus = Str(row, "ApprovalStatus"),
                        InitialCost = Dec(row, "InitialCost"),
                        Cost = Dec(row, "Cost"),
                        ExpenseUptoJune = Dec(row, "ExpenseUptoJune"),
                        ActualExpenditureUptoJune2026 = Dec(row, "ActualExpenditureUptoJune2026"),
                        LocalSouthPunjab = Dec(row, "LocalSouthPunjab"),
                        ForeignSouthPunjab = Dec(row, "ForeignSouthPunjab"),
                        AllocationSouthPunjab = Dec(row, "AllocationSouthPunjab"),
                        LocalAllocation = Dec(row, "LocalAllocation"),
                        ForeignAllocation = Dec(row, "ForeignAllocation"),
                        Allocation = Dec(row, "Allocation"),
                        LocalRevisedAllocation = Dec(row, "LocalRevisedAllocation"),
                        ForeignRevisedAllocation = Dec(row, "ForeignRevisedAllocation"),
                        RevisedAllocation = Dec(row, "RevisedAllocation"),
                        LocalPDDRelease = Dec(row, "LocalPDDRelease"),
                        ForeignPDDRelease = Dec(row, "ForeignPDDRelease"),
                        ReleaseByPDD = Dec(row, "ReleaseByPDD"),
                        ReleaseReceivedByAD = Dec(row, "ReleaseReceivedByAD"),
                        FDRelease = Dec(row, "FDRelease"),
                        SpendingRelease = Dec(row, "SpendingRelease"),
                        UtilizationPifra = Dec(row, "UtilizationPifra"),
                        TotalThrowForward = Dec(row, "TotalThrowForward"),
                        Utilization = Dec(row, "Utilization"),
                        PctUtilCont = Dec(row, "PctUtilCont"),
                        PctUtilRevAlloc = Dec(row, "PctUtilRevAlloc"),
                        SectorID = Int(row, "SectorID"),
                        SectorOrder = Int(row, "SectorOrder"),
                        SectorName = Str(row, "SectorName"),
                        SchemeType = Str(row, "SchemeType"),
                        SchemeSubType = Str(row, "SchemeSubType"),
                        AcToBeCredited = Str(row, "AcToBeCredited"),
                        ProjectStatusType = Str(row, "ProjectStatusType"),
                        ApprovalForum = Str(row, "ApprovalForum"),
                        AA = Str(row, "AA"),
                        TS = Str(row, "TS"),
                        ExecutingAgency = Str(row, "ExecutingAgency"),
                        SponsoringAgency = Str(row, "SponsoringAgency"),
                        IsSNERequired = Str(row, "IsSNERequired"),
                        IsPCI = Str(row, "IsPCI"),
                        AttachDepartment = Str(row, "AttachDepartment"),
                        AdminDepartment = Str(row, "AdminDepartment"),
                        IsMOM = Str(row, "IsMOM"),
                        IsAA = Str(row, "IsAA"),
                        ProjectID = Int(row, "ProjectID"),
                        CreatedAt = DateTime.Now
                    };

                    records.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing PnD file row {Row}: {Message}", row, ex.Message);
                }
            }

            _logger.LogInformation("PnD import: matched {Matched} of {Expected} expected columns from uploaded headers.",
                headerMap.Count, colCount);

            await _budgetRepository.TruncatePnDFileAsync();

            if (records.Count > 0)
                await _budgetRepository.InsertPnDFileAsync(records);

            return records.Count;
        }

        private static string? NullIfEmpty(string? s) => string.IsNullOrWhiteSpace(s) ? null : s;

        // ─── Dev Budget Report Excel export (Parent Project → GL Description hierarchy) ──
        public async Task<byte[]> ExportDevBudgetExcelAsync(int? financialYear, List<string>? parentProjects, List<string>? gsNos = null, List<string>? sectors = null)
        {
            var data = await _budgetRepository.GetDevBudgetReportAsync(financialYear, parentProjects, gsNos, sectors);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Dev Budget Report");

            var navy = System.Drawing.Color.FromArgb(11, 43, 82);
            var blue = System.Drawing.Color.FromArgb(30, 100, 160);
            var lightBlue = System.Drawing.Color.FromArgb(230, 240, 250);
            var altRow = System.Drawing.Color.FromArgb(244, 247, 251);
            var white = System.Drawing.Color.White;
            var darkText = System.Drawing.Color.FromArgb(30, 50, 80);
            var borderColor = System.Drawing.Color.FromArgb(185, 212, 245);

            const int COLS = 9;

            ws.Cells[1, 1, 1, COLS].Merge = true;
            ws.Cells[1, 1].Value = $"Development Expenditure Report FY {financialYear?.ToString() ?? "All"}";
            ws.Cells[1, 1].Style.Font.Size = 14;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(navy);
            ws.Cells[1, 1].Style.Font.Color.SetColor(white);
            ws.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1, 2, COLS].Merge = true;
            ws.Cells[2, 1].Value = $"Dated: {DateTime.Now:dd-MMM-yyyy}";
            ws.Cells[2, 1].Style.Font.Italic = true;
            ws.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;
            ws.Cells[2, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(80, 80, 80));

            int r = 4;
            var headers = new[] { "Project", "GL Description", "Allocation", "Original Budget", "Supplementary", "Reapp (+)", "Reapp (-)", "Surrender", "Expenditure" };
            int headerRow = r;
            for (int c = 0; c < COLS; c++)
            {
                var cell = ws.Cells[headerRow, c + 1];
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(navy);
                cell.Style.Font.Color.SetColor(white);
                cell.Style.HorizontalAlignment = c <= 1
                    ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Left
                    : OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                cell.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                cell.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                cell.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                cell.Style.Border.Top.Color.SetColor(navy);
                cell.Style.Border.Bottom.Color.SetColor(navy);
                cell.Style.Border.Left.Color.SetColor(navy);
                cell.Style.Border.Right.Color.SetColor(navy);
                cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
            }
            r = headerRow + 1;

            void WriteRow(string project, string gl, decimal al, decimal ob, decimal sb, decimal rp, decimal rm, decimal su, decimal ex,
                          System.Drawing.Color bg, System.Drawing.Color fg, bool bold = false)
            {
                ws.Cells[r, 1].Value = project;
                ws.Cells[r, 2].Value = gl;
                ws.Cells[r, 3].Value = Math.Round(al);
                ws.Cells[r, 4].Value = Math.Round(ob);
                ws.Cells[r, 5].Value = Math.Round(sb);
                ws.Cells[r, 6].Value = Math.Round(rp);
                ws.Cells[r, 7].Value = Math.Round(rm);
                ws.Cells[r, 8].Value = Math.Round(su);
                ws.Cells[r, 9].Value = Math.Round(ex);

                for (int c = 1; c <= COLS; c++)
                {
                    var cell = ws.Cells[r, c];
                    cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    cell.Style.Fill.BackgroundColor.SetColor(bg);
                    cell.Style.Font.Color.SetColor(fg);
                    if (c >= 3) { cell.Style.Numberformat.Format = "#,##0"; cell.Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right; }
                    if (bold) cell.Style.Font.Bold = true;
                    cell.Style.Border.Top.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    cell.Style.Border.Left.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    cell.Style.Border.Right.Style = OfficeOpenXml.Style.ExcelBorderStyle.Thin;
                    cell.Style.Border.Top.Color.SetColor(borderColor);
                    cell.Style.Border.Bottom.Color.SetColor(borderColor);
                    cell.Style.Border.Left.Color.SetColor(borderColor);
                    cell.Style.Border.Right.Color.SetColor(borderColor);
                    cell.Style.VerticalAlignment = OfficeOpenXml.Style.ExcelVerticalAlignment.Center;
                }
            }

            var grouped = data.GroupBy(x => new { x.ParentProject, x.ProjectID, x.ProjectDescription, x.GsNo });
            decimal gAl = 0, gOb = 0, gSb = 0, gRp = 0, gRm = 0, gSu = 0, gEx = 0;

            foreach (var proj in grouped)
            {
                var gsNoLabel = string.IsNullOrWhiteSpace(proj.Key.GsNo) ? "-" : proj.Key.GsNo;
                var projLabel = $"{gsNoLabel} - {proj.Key.ProjectID} — {proj.Key.ProjectDescription}";
                var pAl = proj.Sum(x => x.Allocation); var pOb = proj.Sum(x => x.OriginalBudget);
                var pSb = proj.Sum(x => x.SupplementaryBudget); var pRp = proj.Sum(x => x.ReappPlus);
                var pRm = proj.Sum(x => x.ReappMinus); var pSu = proj.Sum(x => x.Surrender);
                var pEx = proj.Sum(x => x.ProgressiveExp);

                WriteRow(projLabel, "", pAl, pOb, pSb, pRp, pRm, pSu, pEx, blue, white, bold: true);
                r++;
                gAl += pAl; gOb += pOb; gSb += pSb; gRp += pRp; gRm += pRm; gSu += pSu; gEx += pEx;

                int glIndex = 0;
                foreach (var gl in proj)
                {
                    var rowBg = glIndex % 2 == 0 ? lightBlue : altRow;
                    WriteRow("", string.IsNullOrWhiteSpace(gl.Gldesc) ? "(Unclassified)" : gl.Gldesc,
                        gl.Allocation, gl.OriginalBudget, gl.SupplementaryBudget, gl.ReappPlus, gl.ReappMinus, gl.Surrender, gl.ProgressiveExp,
                        rowBg, darkText);
                    r++;
                    glIndex++;
                }
            }

            WriteRow("GRAND TOTAL", "", gAl, gOb, gSb, gRp, gRm, gSu, gEx, navy, white, bold: true);

            ws.Column(1).Width = 42;
            ws.Column(2).Width = 32;
            for (int c = 3; c <= COLS; c++) ws.Column(c).Width = 16;

            ws.View.FreezePanes(headerRow + 1, 1);
            ws.PrinterSettings.Orientation = OfficeOpenXml.eOrientation.Landscape;
            ws.PrinterSettings.FitToPage = true;
            ws.PrinterSettings.FitToWidth = 1;
            ws.PrinterSettings.FitToHeight = 0;
            ws.PrinterSettings.PaperSize = OfficeOpenXml.ePaperSize.A4;
            ws.PrinterSettings.RepeatRows = ws.Cells[$"{headerRow}:{headerRow}"];
            ws.View.ShowGridLines = false;

            return package.GetAsByteArray();
        }

        // ─── Department Wise full drill-down Excel export ───────────────────────────
        public async Task<byte[]> ExportDepartmentWiseFullAsync(FilterViewModel filters)
        {
            // Colours matching the PDF scheme
            var darkNavy    = System.Drawing.Color.FromArgb(11, 43, 82);
            var deptColor   = System.Drawing.Color.FromArgb(21, 67, 120);
            var fundColor   = System.Drawing.Color.FromArgb(30, 100, 160);
            var ccColor     = System.Drawing.Color.FromArgb(200, 220, 245);
            var glColor     = System.Drawing.Color.FromArgb(240, 246, 255);
            var totalColor  = System.Drawing.Color.FromArgb(11, 43, 82);
            var white       = System.Drawing.Color.White;
            var darkText    = System.Drawing.Color.FromArgb(30, 50, 80);

            const int COLS = 9;
            string lastCol = ((char)('A' + COLS - 1)).ToString();

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Department Wise Report");

            // ── Title block ──────────────────────────────────────────────────────────
            ws.Cells[1, 1, 1, COLS].Merge = true;
            ws.Cells[1, 1].Value = "Finance Department Punjab";
            ws.Cells[1, 1].Style.Font.Size = 15;
            ws.Cells[1, 1].Style.Font.Bold = true;
            ws.Cells[1, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[1, 1].Style.Fill.BackgroundColor.SetColor(darkNavy);
            ws.Cells[1, 1].Style.Font.Color.SetColor(white);
            ws.Cells[1, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            ws.Cells[2, 1, 2, COLS].Merge = true;
            ws.Cells[2, 1].Value = "Department Wise Budget Report";
            ws.Cells[2, 1].Style.Font.Size = 12;
            ws.Cells[2, 1].Style.Font.Bold = true;
            ws.Cells[2, 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            ws.Cells[2, 1].Style.Fill.BackgroundColor.SetColor(darkNavy);
            ws.Cells[2, 1].Style.Font.Color.SetColor(white);
            ws.Cells[2, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Center;

            // ── Filter subtitle ──────────────────────────────────────────────────────
            int r = 3;
            void AddFilter(string label, string? val)
            {
                if (string.IsNullOrWhiteSpace(val)) return;
                ws.Cells[r, 1, r, COLS].Merge = true;
                ws.Cells[r, 1].Value = $"{label}: {val}";
                ws.Cells[r, 1].Style.Font.Italic = true;
                ws.Cells[r, 1].Style.Font.Color.SetColor(System.Drawing.Color.FromArgb(80, 80, 80));
                r++;
            }
            AddFilter("Financial Year", filters.FinancialYear?.ToString());
            AddFilter("Sector",         filters.Sector);
            AddFilter("Department",     filters.DepartmentCode);
            AddFilter("Fund",           filters.FundCode);

            ws.Cells[r, 1, r, COLS].Merge = true;
            ws.Cells[r, 1].Value = $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}";
            ws.Cells[r, 1].Style.Font.Size = 9;
            ws.Cells[r, 1].Style.Font.Color.SetColor(System.Drawing.Color.Gray);
            r++;

            // ── Column headers ───────────────────────────────────────────────────────
            int headerRow = r;
            var headers = new[] { "Department / Fund / Cost Center / GL", "Alloc Budget", "Budget Grant",
                                   "Releases", "% Release", "Expenditure", "% Util", "Balance", "Final Grant" };
            for (int c = 0; c < COLS; c++)
            {
                var cell = ws.Cells[headerRow, c + 1];
                cell.Value = headers[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(darkNavy);
                cell.Style.Font.Color.SetColor(white);
                cell.Style.HorizontalAlignment = c == 0
                    ? OfficeOpenXml.Style.ExcelHorizontalAlignment.Left
                    : OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                cell.Style.Border.Bottom.Style = OfficeOpenXml.Style.ExcelBorderStyle.Medium;
                cell.Style.Border.Bottom.Color.SetColor(white);
            }
            r = headerRow + 1;

            // ── Helper: write a data row ─────────────────────────────────────────────
            void WriteDataRow(int row, string label, decimal al, decimal bg, decimal rl, decimal ex, decimal bl, decimal fg,
                              System.Drawing.Color bgFill, System.Drawing.Color fgFill, bool bold = false)
            {
                ws.Cells[row, 1].Value = label;
                ws.Cells[row, 2].Value = Math.Round(al);
                ws.Cells[row, 3].Value = Math.Round(bg);
                ws.Cells[row, 4].Value = Math.Round(rl);
                ws.Cells[row, 5].Value = bg > 0 ? rl / bg : 0;
                ws.Cells[row, 6].Value = Math.Round(ex);
                ws.Cells[row, 7].Value = bg > 0 ? ex / bg : 0;
                ws.Cells[row, 8].Value = Math.Round(bl);
                ws.Cells[row, 9].Value = Math.Round(fg);

                ws.Cells[row, 5].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 7].Style.Numberformat.Format = "0.0%";
                for (int c = 2; c <= COLS; c++)
                {
                    if (c != 5 && c != 7) ws.Cells[row, c].Style.Numberformat.Format = "#,##0";
                    ws.Cells[row, c].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                }
                ws.Cells[row, 1].Style.Font.Bold = bold;
                for (int c = 1; c <= COLS; c++)
                {
                    ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(bgFill);
                    ws.Cells[row, c].Style.Font.Color.SetColor(fgFill);
                    if (bold) ws.Cells[row, c].Style.Font.Bold = true;
                }
                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            }

            // ── Fetch data and write rows ────────────────────────────────────────────
            var depts = await _budgetRepository.GetDepartmentWiseReportAsync(
                filters.FinancialYear, filters.Sector, filters.DepartmentCode, filters.FundCode);

            decimal gAl = 0, gBg = 0, gRl = 0, gEx = 0, gBl = 0, gFg = 0;

            foreach (var dept in depts)
            {
                WriteDataRow(r, dept.DepartmentName ?? "(No Department)",
                    dept.TotalAllocatedBudget, dept.TotalBudgetGrant, dept.TotalReleases,
                    dept.TotalActualExpenditure, dept.TotalBalance, dept.TotalFinalGrant,
                    deptColor, white, bold: true);
                r++;

                gAl += dept.TotalAllocatedBudget; gBg += dept.TotalBudgetGrant;
                gRl += dept.TotalReleases; gEx += dept.TotalActualExpenditure;
                gBl += dept.TotalBalance; gFg += dept.TotalFinalGrant;

                var funds = await _budgetRepository.GetDrillFundsAsync(
                    dept.DepartmentName ?? "", filters.FinancialYear, filters.Sector);

                foreach (var fund in funds)
                {
                    WriteDataRow(r, $"  {fund.FundCode} — {fund.FundDes}",
                        fund.TotalAllocatedBudget, fund.TotalBudgetGrant, fund.TotalReleases,
                        fund.TotalActualExpenditure, fund.TotalBalance, fund.TotalFinalGrant,
                        fundColor, white, bold: true);
                    r++;

                    var ccs = await _budgetRepository.GetDrillCostCentersAsync(
                        dept.DepartmentName ?? "", fund.FundCode, filters.FinancialYear, filters.Sector);

                    foreach (var cc in ccs)
                    {
                        WriteDataRow(r, $"    {cc.FundCenterCode} — {cc.CostCenterDescription}",
                            cc.TotalAllocatedBudget, cc.TotalBudgetGrant, cc.TotalReleases,
                            cc.TotalActualExpenditure, cc.TotalBalance, cc.TotalFinalGrant,
                            ccColor, darkText);
                        r++;

                        var gls = await _budgetRepository.GetCostCenterGLDetailAsync(
                            cc.FundCenterCode, filters.FinancialYear, fund.FundCode);

                        foreach (var gl in gls)
                        {
                            WriteDataRow(r, $"      {gl.GLCode} — {gl.GLDescription}",
                                gl.TotalAllocatedBudget, gl.TotalBudgetGrant, gl.TotalReleases,
                                gl.TotalActualExpenditure, gl.TotalBalance, gl.TotalFinalGrant,
                                glColor, darkText);
                            r++;
                        }
                    }
                }
            }

            // ── Grand Total row ──────────────────────────────────────────────────────
            WriteDataRow(r, "GRAND TOTAL",
                gAl, gBg, gRl, gEx, gBl, gFg,
                totalColor, white, bold: true);

            // ── Column widths ────────────────────────────────────────────────────────
            ws.Column(1).Width = 52;
            for (int c = 2; c <= COLS; c++) ws.Column(c).Width = 16;

            ws.View.FreezePanes(headerRow + 1, 2);

            return package.GetAsByteArray();
        }
    }
}
