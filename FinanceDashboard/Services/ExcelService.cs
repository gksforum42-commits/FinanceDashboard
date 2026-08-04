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
            var records = new List<OriginalBudget>();
            int rowCount = ws.Dimension?.Rows ?? 0;
            int batchSize = 10000;

            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var record = new OriginalBudget
                    {
                        FinancialYear = ParseInt(ws.Cells[row, 1].Text) is int fy && fy > 0 ? fy : financialYear,
                        DocumentNumber = ws.Cells[row, 2].Text.Trim(),
                        DepartmentCode = ws.Cells[row, 3].Text.Trim(),
                        BudgetType = ws.Cells[row, 4].Text.Trim(),
                        FundCode = ws.Cells[row, 5].Text.Trim(),
                        FundDescription = ws.Cells[row, 6].Text.Trim(),
                        CostCenterCode = ws.Cells[row, 7].Text.Trim(),
                        SNECode = ws.Cells[row, 8].Text.Trim(),
                        CommitmentItem = ws.Cells[row, 9].Text.Trim(),
                        SubHead = ws.Cells[row, 10].Text.Trim(),
                        BPS = ws.Cells[row, 11].Text.Trim(),
                        DesignationCode = ws.Cells[row, 12].Text.Trim(),
                        Posts = ParseInt(ws.Cells[row, 13].Text),
                        OriginalBudgetAmount = ParseDecimal(ws.Cells[row, 14].Text),
                        RevisedBudgetAmount = ParseDecimal(ws.Cells[row, 15].Text),
                        DesignationDescription = ws.Cells[row, 16].Text.Trim(),
                        Description = ws.Cells[row, 17].Text.Trim(),
                        PostingDate = ParseDate(ws.Cells[row, 18].Text),
                        UserName = ws.Cells[row, 19].Text.Trim(),
                        FunctionCode = ws.Cells[row, 20].Text.Trim(),
                        Type = ws.Cells[row, 21].Text.Trim(),
                        ImportId = importId
                    };

                    records.Add(record);

                    if (records.Count >= batchSize)
                    {
                        await _budgetRepository.InsertOriginalBudgetAsync(records);
                        records.Clear();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("Error parsing row {Row}: {Message}", row, ex.Message);
                }
            }

            if (records.Count > 0)
            {
                await _budgetRepository.InsertOriginalBudgetAsync(records);
            }

            return rowCount > 0 ? rowCount - 1 : 0;
        }

        private async Task<int> ImportBERSheetAsync(ExcelWorksheet ws, int financialYear, long importId)
        {
            int rowCount = ws.Dimension?.Rows ?? 0;
            if (rowCount < 2) return 0;

            // Wipe all existing staging data first so we always start clean
            await _budgetRepository.TruncateBerSapDataAsync();

            // Build a header-name → column-index map (case-insensitive, trimmed)
            int colCount = ws.Dimension.Columns;
            var headerMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int c = 1; c <= colCount; c++)
            {
                var h = ws.Cells[1, c].Text.Trim();
                if (!string.IsNullOrEmpty(h) && !headerMap.ContainsKey(h))
                    headerMap[h] = c;
            }

            // Helper: get text by header name, fall back to positional index if header not found
            string Col(int row, string name, int fallbackCol)
            {
                int idx = headerMap.TryGetValue(name, out int ci) ? ci : fallbackCol;
                return ws.Cells[row, idx].Text.Trim();
            }
            decimal ColD(int row, string name, int fallbackCol)
                => ParseDecimal(Col(row, name, fallbackCol));
            int ColI(int row, string name, int fallbackCol)
                => ParseInt(Col(row, name, fallbackCol));

            var records = new List<BERData>(rowCount - 1);
            for (int row = 2; row <= rowCount; row++)
            {
                try
                {
                    var fyVal = ColI(row, "FinancialYear", 1);
                    var record = new BERData
                    {
                        ImportId              = importId,
                        FinancialYear         = fyVal > 0 ? fyVal : financialYear,
                        FundCode              = Col(row, "FundCode",              2),
                        FundCenterCode        = Col(row, "FundCenterCode",        3),
                        FundCenterDescription = Col(row, "FundCenterDescription", 4),
                        MinistryDepartment    = Col(row, "MinistryDepartment",    5),
                        AttachedDepartment    = Col(row, "AttachedDepartment",    6),
                        ProfitCenter          = Col(row, "ProfitCenter",          7),
                        SubDetailFunction     = Col(row, "SubDetailFunction",     8),
                        DetailObject          = Col(row, "DetailObject",          9),
                        BudgetGrant           = ColD(row, "BudgetGrant",          10),
                        SupplementaryGrant    = ColD(row, "SupplementaryGrant",   11),
                        ReappropriationPlus   = ColD(row, "ReappropriationPlus",  12),
                        ReappropriationMinus  = ColD(row, "ReappropriationMinus", 13),
                        TSSSurrender          = ColD(row, "TSSSurrender",         14),
                        FinalGrant            = ColD(row, "FinalGrant",           15),
                        Releases              = ColD(row, "Releases",             16),
                        JulExp                = ColD(row, "JulExp",               17),
                        AugExp                = ColD(row, "AugExp",               18),
                        SepExp                = ColD(row, "SepExp",               19),
                        OctExp                = ColD(row, "OctExp",               20),
                        NovExp                = ColD(row, "NovExp",               21),
                        DecExp                = ColD(row, "DecExp",               22),
                        JanExp                = ColD(row, "JanExp",               23),
                        FebExp                = ColD(row, "FebExp",               24),
                        MarExp                = ColD(row, "MarExp",               25),
                        AprExp                = ColD(row, "AprExp",               26),
                        MayExp                = ColD(row, "MayExp",               27),
                        JunExp                = ColD(row, "JunExp",               28),
                        ActualExpenditure     = ColD(row, "ActualExpenditure",    29),
                        FinalGrantBalance     = ColD(row, "FinalGrantBalance",    30)
                    };
                    records.Add(record);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning("BER parse error row {Row}: {Msg}", row, ex.Message);
                }
            }

            // Insert all rows in a single bulk call (50,000-row batches inside InsertBERDataAsync)
            if (records.Count > 0)
                await _budgetRepository.InsertBERDataAsync(records);

            return records.Count;
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
                "fund" => await ExportToExcelAsync(await _budgetRepository.GetFundWiseReportAsync(filters.FinancialYear, filters.Keyword), "Fund Wise Report"),
                "department" => await ExportToExcelAsync(await _budgetRepository.GetDepartmentWiseReportAsync(filters.FinancialYear, filters.DepartmentCode, filters.FundCode), "Department Wise Report"),
                "costcenter" => await ExportToExcelAsync(await _budgetRepository.GetCostCenterWiseReportAsync(filters.FinancialYear, filters.FundCode, filters.Keyword), "Cost Center Wise Report"),
                "function" => await ExportToExcelAsync(await _budgetRepository.GetFunctionWiseReportAsync(filters.FinancialYear, filters.Keyword), "Function Wise Report"),
                "object" => await ExportObjectWiseReportAsync(filters),
                _ => await ExportToExcelAsync(new List<object>(), "Report")
            };
        }

        public async Task<byte[]> ExportObjectWiseReportAsync(FilterViewModel filters)
        {
            var data = await _budgetRepository.GetObjectWiseGroupedAsync(
                filters.FinancialYear, filters.Sector, filters.DepartmentCode, filters.FundCode, filters.CostCenterCode, filters.Keyword);

            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Object Wise Report");

            const int totalCols = 9;
            var navy  = System.Drawing.Color.FromArgb(11, 43, 82);
            var white = System.Drawing.Color.White;
            var sectorBg = System.Drawing.Color.FromArgb(0, 70, 127);
            var deptBg   = System.Drawing.Color.FromArgb(14, 99, 160);
            var fundBg   = System.Drawing.Color.FromArgb(41, 128, 185);
            var subtotBg = System.Drawing.Color.FromArgb(214, 229, 247);
            var grandBg  = System.Drawing.Color.FromArgb(11, 43, 82);

            string MergeRange(int r, int c1, int c2) => $"{ExcelCellAddress.GetColumnLetter(c1)}{r}:{ExcelCellAddress.GetColumnLetter(c2)}{r}";
            void StyleBand(int row, System.Drawing.Color bg, System.Drawing.Color fg, float sz, bool bold)
            {
                ws.Cells[MergeRange(row, 1, totalCols)].Merge = true;
                for (int c = 1; c <= totalCols; c++)
                {
                    ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(bg);
                    ws.Cells[row, c].Style.Font.Color.SetColor(fg);
                    ws.Cells[row, c].Style.Font.Bold = bold;
                    ws.Cells[row, c].Style.Font.Size = sz;
                }
            }
            void WriteNumRow(int row, decimal al, decimal bg2, decimal rl, decimal fg2, decimal ex, decimal bl, System.Drawing.Color fillColor, bool bold)
            {
                decimal relPct  = al > 0 ? rl / al : 0;
                decimal utilPct = fg2 > 0 ? ex / fg2 : 0;
                ws.Cells[row, 3].Value = Math.Round(al); ws.Cells[row, 4].Value = Math.Round(bg2);
                ws.Cells[row, 5].Value = Math.Round(rl); ws.Cells[row, 6].Value = relPct;
                ws.Cells[row, 7].Value = Math.Round(ex); ws.Cells[row, 8].Value = utilPct;
                ws.Cells[row, 9].Value = Math.Round(bl);
                for (int c = 3; c <= 5; c++) ws.Cells[row, c].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 6].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 7].Style.Numberformat.Format = "#,##0";
                ws.Cells[row, 8].Style.Numberformat.Format = "0.0%";
                ws.Cells[row, 9].Style.Numberformat.Format = "#,##0";
                for (int c = 1; c <= totalCols; c++)
                {
                    ws.Cells[row, c].Style.Font.Bold = bold;
                    ws.Cells[row, c].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
                    ws.Cells[row, c].Style.Fill.BackgroundColor.SetColor(fillColor);
                    if (c >= 3) ws.Cells[row, c].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Right;
                }
            }

            int row = 1;
            // ── Title ──────────────────────────────────────────────────────────────
            StyleBand(row, navy, white, 14, true);
            ws.Cells[row, 1].Value = "Finance Department Punjab";
            row++;
            StyleBand(row, navy, white, 11, false);
            ws.Cells[row, 1].Value = "Object Wise Budget Report" + (filters.FinancialYear.HasValue ? $"  —  FY {filters.FinancialYear}" : "");
            row++;
            StyleBand(row, System.Drawing.Color.FromArgb(40, 60, 100), System.Drawing.Color.FromArgb(200, 215, 240), 9, false);
            ws.Cells[row, 1].Value = $"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}   |   Sector: {filters.Sector ?? "All"}   |   Dept: {filters.DepartmentCode ?? "All"}   |   Fund: {filters.FundCode ?? "All"}   |   CC: {filters.CostCenterCode ?? "All"}   |   Object: {filters.Keyword ?? "All"}";
            row++;

            // ── Column headers ────────────────────────────────────────────────────
            var headers = new[] { "Object Code", "Object Description", "Alloc Budget", "Budget Grant", "Releases", "% Release", "Expenditure", "% Util", "Balance" };
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

            // ── Hierarchical data ─────────────────────────────────────────────────
            decimal gAl = 0, gBg = 0, gRl = 0, gFg = 0, gEx = 0, gBl = 0;
            var altGrey = System.Drawing.Color.FromArgb(248, 250, 255);

            var bySector = data.GroupBy(d => d.Sector).OrderBy(g => g.Key);
            foreach (var sGrp in bySector)
            {
                // Sector heading
                StyleBand(row, sectorBg, white, 10, true);
                ws.Cells[row, 1].Value = $"  SECTOR:  {sGrp.Key}";
                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                row++;

                decimal sAl=0, sBg=0, sRl=0, sFg=0, sEx=0, sBl=0;

                var byDept = sGrp.GroupBy(d => d.Department).OrderBy(g => g.Key);
                foreach (var dGrp in byDept)
                {
                    StyleBand(row, deptBg, white, 9.5f, true);
                    ws.Cells[row, 1].Value = $"    Department:  {dGrp.Key}";
                    ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    row++;

                    decimal dAl=0, dBg=0, dRl=0, dFg=0, dEx=0, dBl=0;

                    var byFund = dGrp.GroupBy(d => new { d.FundCode, d.FundDescription }).OrderBy(g => g.Key.FundCode);
                    foreach (var fGrp in byFund)
                    {
                        StyleBand(row, fundBg, white, 9, false);
                        ws.Cells[row, 1].Value = $"      Fund:  {fGrp.Key.FundCode}  {fGrp.Key.FundDescription}";
                        ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                        row++;

                        decimal fAl=0, fBg=0, fRl=0, fFg=0, fEx=0, fBl=0;
                        bool alt = false;
                        foreach (var item in fGrp.OrderByDescending(x => x.TotalAllocatedBudget))
                        {
                            var rowFill = alt ? altGrey : white;
                            ws.Cells[row, 1].Value = item.DetailObject;
                            ws.Cells[row, 2].Value = item.ObjectDescription ?? string.Empty;
                            WriteNumRow(row, item.TotalAllocatedBudget, item.TotalBudgetGrant, item.TotalReleases, item.TotalFinalGrant, item.TotalActualExpenditure, item.TotalBalance, rowFill, false);
                            ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                            ws.Cells[row, 2].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                            fAl += item.TotalAllocatedBudget; fBg += item.TotalBudgetGrant;
                            fRl += item.TotalReleases; fFg += item.TotalFinalGrant;
                            fEx += item.TotalActualExpenditure; fBl += item.TotalBalance;
                            alt = !alt; row++;
                        }
                        // Fund subtotal
                        ws.Cells[row, 1, row, 2].Merge = true;
                        ws.Cells[row, 1].Value = $"      Subtotal — Fund: {fGrp.Key.FundCode}";
                        ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                        WriteNumRow(row, fAl, fBg, fRl, fFg, fEx, fBl, subtotBg, true);
                        row++;
                        dAl += fAl; dBg += fBg; dRl += fRl; dFg += fFg; dEx += fEx; dBl += fBl;
                    }
                    // Dept subtotal
                    ws.Cells[row, 1, row, 2].Merge = true;
                    ws.Cells[row, 1].Value = $"    Subtotal — Dept: {dGrp.Key}";
                    ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                    WriteNumRow(row, dAl, dBg, dRl, dFg, dEx, dBl, System.Drawing.Color.FromArgb(185, 210, 240), true);
                    row++;
                    sAl += dAl; sBg += dBg; sRl += dRl; sFg += dFg; sEx += dEx; sBl += dBl;
                }
                // Sector subtotal
                ws.Cells[row, 1, row, 2].Merge = true;
                ws.Cells[row, 1].Value = $"  Subtotal — Sector: {sGrp.Key}";
                ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
                WriteNumRow(row, sAl, sBg, sRl, sFg, sEx, sBl, System.Drawing.Color.FromArgb(155, 190, 230), true);
                row++;
                gAl += sAl; gBg += sBg; gRl += sRl; gFg += sFg; gEx += sEx; gBl += sBl;
            }

            // ── Grand total ───────────────────────────────────────────────────────
            ws.Cells[row, 1, row, 2].Merge = true;
            ws.Cells[row, 1].Value = "GRAND TOTAL";
            ws.Cells[row, 1].Style.HorizontalAlignment = OfficeOpenXml.Style.ExcelHorizontalAlignment.Left;
            WriteNumRow(row, gAl, gBg, gRl, gFg, gEx, gBl, grandBg, true);
            for (int c = 1; c <= totalCols; c++) ws.Cells[row, c].Style.Font.Color.SetColor(white);

            ws.Cells.AutoFitColumns();
            ws.Column(2).Width = 40; // description can be wide
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
