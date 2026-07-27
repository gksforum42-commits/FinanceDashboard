using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels.Pivot;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using System.Text;

namespace FinanceDashboard.Services
{
    /// <summary>
    /// Produces Excel (.xlsx) and CSV exports of the pivot result.
    /// </summary>
    public class PivotExportService : IPivotExportService
    {
        // ════════════════════════════════════════════════════════════════════
        //  Excel Export
        // ════════════════════════════════════════════════════════════════════
        public Task<byte[]> ExportExcelAsync(PivotResultViewModel result, PivotRequestViewModel request)
        {
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("Pivot Report");

            var rowDims = request.RowDimensions;
            var values = result.ValueFields;
            var colHeaders = result.ColumnHeaders;
            bool hasColDim = colHeaders.Any();

            // ── Header row 1 (column dimension groups) ────────────────────────
            int headerRow = 1;
            int startCol = rowDims.Count + 1;

            // Row dimension headers
            for (int d = 0; d < rowDims.Count; d++)
            {
                ws.Cells[headerRow, d + 1].Value = rowDims[d];
                StyleHeaderCell(ws.Cells[headerRow, d + 1], isGroupHeader: true);
            }

            if (hasColDim)
            {
                foreach (var colHeader in colHeaders)
                {
                    int colStart = startCol;
                    foreach (var v in values)
                    {
                        ws.Cells[headerRow, startCol].Value = $"{colHeader} / {v}";
                        StyleHeaderCell(ws.Cells[headerRow, startCol]);
                        startCol++;
                    }
                }
                // Row totals
                foreach (var v in values)
                {
                    ws.Cells[headerRow, startCol].Value = $"Total {v}";
                    StyleHeaderCell(ws.Cells[headerRow, startCol], isTotal: true);
                    startCol++;
                }
            }
            else
            {
                foreach (var v in values)
                {
                    ws.Cells[headerRow, startCol].Value = v;
                    StyleHeaderCell(ws.Cells[headerRow, startCol]);
                    startCol++;
                }
            }

            // ── Data rows ─────────────────────────────────────────────────────
            int dataRow = headerRow + 1;
            foreach (var row in result.Rows)
            {
                int col = 1;

                // Dimension cells
                for (int d = 0; d < rowDims.Count; d++)
                {
                    var cell = ws.Cells[dataRow, col++];
                    cell.Value = d < row.DimensionValues.Count ? row.DimensionValues[d] : null;
                    if (row.IsSubTotal) StyleSubTotal(cell, d == row.Depth);
                    else if (d == row.Depth) cell.Style.Font.Bold = false;
                }

                // Value cells
                if (hasColDim)
                {
                    foreach (var ch in colHeaders)
                        foreach (var v in values)
                        {
                            var ck = $"{ch}|{v}";
                            var numCell = ws.Cells[dataRow, col++];
                            numCell.Value = row.Cells.TryGetValue(ck, out var cv) ? (object)cv : 0m;
                            StyleNumberCell(numCell, row.IsSubTotal, (decimal)(numCell.Value ?? 0m));
                        }
                    foreach (var v in values)
                    {
                        var totCell = ws.Cells[dataRow, col++];
                        totCell.Value = row.RowTotals.TryGetValue(v, out var rt) ? (object)rt : 0m;
                        StyleNumberCell(totCell, true, (decimal)(totCell.Value ?? 0m));
                    }
                }
                else
                {
                    foreach (var v in values)
                    {
                        var numCell = ws.Cells[dataRow, col++];
                        numCell.Value = row.Cells.TryGetValue(v, out var cv) ? (object)cv : 0m;
                        StyleNumberCell(numCell, row.IsSubTotal, (decimal)(numCell.Value ?? 0m));
                    }
                }

                if (row.IsSubTotal)
                {
                    ws.Cells[dataRow, 1, dataRow, col - 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                    ws.Cells[dataRow, 1, dataRow, col - 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(217, 225, 242));
                }

                dataRow++;
            }

            // ── Grand Total ───────────────────────────────────────────────────
            if (result.GrandTotal != null)
            {
                int col = 1;
                var gt = result.GrandTotal;

                ws.Cells[dataRow, col].Value = "Grand Total";
                ws.Cells[dataRow, col].Style.Font.Bold = true;
                ws.Cells[dataRow, col++].Style.Font.Size = 11;
                for (int d = 1; d < rowDims.Count; d++) col++;

                if (hasColDim)
                {
                    foreach (var ch in colHeaders)
                        foreach (var v in values)
                        {
                            var ck = $"{ch}|{v}";
                            var numCell = ws.Cells[dataRow, col++];
                            numCell.Value = gt.Cells.TryGetValue(ck, out var cv) ? (object)cv : 0m;
                            StyleGrandTotalCell(numCell, (decimal)(numCell.Value ?? 0m));
                        }
                    foreach (var v in values)
                    {
                        var totCell = ws.Cells[dataRow, col++];
                        totCell.Value = gt.RowTotals.TryGetValue(v, out var rt) ? (object)rt : 0m;
                        StyleGrandTotalCell(totCell, (decimal)(totCell.Value ?? 0m));
                    }
                }
                else
                {
                    foreach (var v in values)
                    {
                        var numCell = ws.Cells[dataRow, col++];
                        numCell.Value = gt.RowTotals.TryGetValue(v, out var rt) ? (object)rt : 0m;
                        StyleGrandTotalCell(numCell, (decimal)(numCell.Value ?? 0m));
                    }
                }

                ws.Cells[dataRow, 1, dataRow, col - 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[dataRow, 1, dataRow, col - 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(31, 73, 125));
                ws.Cells[dataRow, 1, dataRow, col - 1].Style.Font.Color.SetColor(Color.White);
            }

            ws.Cells[ws.Dimension.Address].AutoFitColumns(8, 60);

            return Task.FromResult(package.GetAsByteArray());
        }

        // ════════════════════════════════════════════════════════════════════
        //  CSV Export
        // ════════════════════════════════════════════════════════════════════
        public Task<byte[]> ExportCsvAsync(PivotResultViewModel result, PivotRequestViewModel request)
        {
            var rowDims = request.RowDimensions;
            var values = result.ValueFields;
            var colHeaders = result.ColumnHeaders;
            bool hasColDim = colHeaders.Any();

            var sb = new StringBuilder();

            // Header
            var headerCols = new List<string>(rowDims);
            if (hasColDim)
            {
                foreach (var ch in colHeaders)
                    foreach (var v in values)
                        headerCols.Add($"{ch} / {v}");
                foreach (var v in values)
                    headerCols.Add($"Total {v}");
            }
            else
            {
                headerCols.AddRange(values);
            }
            sb.AppendLine(string.Join(",", headerCols.Select(CsvEscape)));

            // Rows
            foreach (var row in result.Rows)
            {
                var cells = new List<string>();
                for (int d = 0; d < rowDims.Count; d++)
                    cells.Add(CsvEscape(d < row.DimensionValues.Count ? row.DimensionValues[d] ?? "" : ""));

                if (hasColDim)
                {
                    foreach (var ch in colHeaders)
                        foreach (var v in values)
                        {
                            var ck = $"{ch}|{v}";
                            cells.Add(row.Cells.TryGetValue(ck, out var cv) ? cv.ToString("F2") : "0.00");
                        }
                    foreach (var v in values)
                        cells.Add(row.RowTotals.TryGetValue(v, out var rt) ? rt.ToString("F2") : "0.00");
                }
                else
                {
                    foreach (var v in values)
                        cells.Add(row.Cells.TryGetValue(v, out var cv) ? cv.ToString("F2") : "0.00");
                }
                sb.AppendLine(string.Join(",", cells));
            }

            // Grand Total
            if (result.GrandTotal != null)
            {
                var gtCells = new List<string> { "Grand Total" };
                for (int d = 1; d < rowDims.Count; d++) gtCells.Add("");

                var gt = result.GrandTotal;
                if (hasColDim)
                {
                    foreach (var ch in colHeaders)
                        foreach (var v in values)
                        {
                            var ck = $"{ch}|{v}";
                            gtCells.Add(gt.Cells.TryGetValue(ck, out var cv) ? cv.ToString("F2") : "0.00");
                        }
                    foreach (var v in values)
                        gtCells.Add(gt.RowTotals.TryGetValue(v, out var rt) ? rt.ToString("F2") : "0.00");
                }
                else
                {
                    foreach (var v in values)
                        gtCells.Add(gt.RowTotals.TryGetValue(v, out var rt) ? rt.ToString("F2") : "0.00");
                }
                sb.AppendLine(string.Join(",", gtCells));
            }

            return Task.FromResult(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray());
        }

        // ════════════════════════════════════════════════════════════════════
        //  Style Helpers
        // ════════════════════════════════════════════════════════════════════
        private static void StyleHeaderCell(ExcelRange cell, bool isGroupHeader = false, bool isTotal = false)
        {
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(isGroupHeader
                ? Color.FromArgb(31, 73, 125)
                : isTotal ? Color.FromArgb(68, 114, 196)
                : Color.FromArgb(68, 114, 196));
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            cell.Style.Border.BorderAround(ExcelBorderStyle.Thin, Color.White);
            cell.Style.WrapText = true;
        }

        private static void StyleSubTotal(ExcelRange cell, bool isCurrent)
        {
            if (isCurrent) cell.Style.Font.Bold = true;
            cell.Style.Font.Italic = true;
        }

        private static void StyleNumberCell(ExcelRange cell, bool bold, decimal value)
        {
            cell.Style.Numberformat.Format = "#,##0.00";
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            if (bold) cell.Style.Font.Bold = true;
            if (value < 0) cell.Style.Font.Color.SetColor(Color.Red);
        }

        private static void StyleGrandTotalCell(ExcelRange cell, decimal value)
        {
            cell.Style.Numberformat.Format = "#,##0.00";
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Right;
            cell.Style.Font.Bold = true;
        }

        private static string CsvEscape(string? s)
        {
            if (string.IsNullOrEmpty(s)) return "";
            if (s.Contains(',') || s.Contains('"') || s.Contains('\n'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }
}
