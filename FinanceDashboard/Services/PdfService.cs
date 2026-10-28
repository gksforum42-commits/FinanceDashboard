using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FinanceDashboard.Services
{
    public class PdfService : IPdfService
    {
        private readonly IBudgetRepository _budgetRepository;

        public PdfService(IBudgetRepository budgetRepository)
        {
            _budgetRepository = budgetRepository;
            QuestPDF.Settings.License = LicenseType.Community;
        }

        public async Task<byte[]> GenerateReportPdfAsync(string reportType, FilterViewModel filters, List<string> columns, string title)
        {
            var data = reportType.ToLower() switch
            {
                "fund" => (await _budgetRepository.GetFundWiseReportAsync(filters.FinancialYear, filters.Keyword)).Cast<object>().ToList(),
                "department" => (await _budgetRepository.GetDepartmentWiseReportAsync(filters.FinancialYear, filters.DepartmentCode, filters.FundCode)).Cast<object>().ToList(),
                "costcenter" => (await _budgetRepository.GetCostCenterWiseReportAsync(filters.FinancialYear, filters.FundCode, filters.Keyword)).Cast<object>().ToList(),
                "function" => (await _budgetRepository.GetFunctionWiseReportAsync(filters.FinancialYear, filters.Keyword)).Cast<object>().ToList(),
                "object" => (await _budgetRepository.GetObjectWiseReportAsync(filters.FinancialYear, filters.Keyword)).Cast<object>().ToList(),
                _ => new List<object>()
            };

            return GeneratePdf(data, columns, title);
        }

        public async Task<byte[]> GenerateOriginalBudgetPdfAsync(FilterViewModel filters)
        {
            var pagination = new PaginationViewModel { PageNumber = 1, PageSize = 1000 };
            var result = await _budgetRepository.GetOriginalBudgetAsync(filters, pagination);
            var columns = new List<string> { "DocumentNumber", "DepartmentCode", "FundCode", "CostCenterCode", "CommitmentItem", "Posts", "OriginalBudgetAmount", "RevisedBudgetAmount" };
            return GeneratePdf(result.Data.Cast<object>().ToList(), columns, "Original Budget Report");
        }

        public async Task<byte[]> GenerateBERPdfAsync(FilterViewModel filters)
        {
            var pagination = new PaginationViewModel { PageNumber = 1, PageSize = 1000 };
            var result = await _budgetRepository.GetBERDataAsync(filters, pagination);
            var columns = new List<string> { "FundCode", "FundCenterCode", "BudgetGrant", "FinalGrant", "Releases", "ActualExpenditure", "FinalGrantBalance" };
            return GeneratePdf(result.Data.Cast<object>().ToList(), columns, "BER Report");
        }

        public async Task<byte[]> GenerateCostCenterReportPdfAsync(int? financialYear, string? fundCode, string? costCenterCode)
        {
            var data = await _budgetRepository.GetCostCenterReportAsync(financialYear, fundCode, costCenterCode);
            var title = $"Cost Center Report{(financialYear.HasValue ? " - FY " + financialYear : "")}";

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Element(header =>
                    {
                        header.Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Finance Department Punjab").FontSize(14).Bold();
                                col.Item().Text(title).FontSize(11).SemiBold();
                                if (fundCode != null) col.Item().Text($"Fund: {fundCode}").FontSize(8).FontColor(Colors.Grey.Darken2);
                                if (costCenterCode != null) col.Item().Text($"Cost Center: {costCenterCode}").FontSize(8).FontColor(Colors.Grey.Darken2);
                                col.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });
                    });

                    page.Content().Element(content =>
                    {
                        if (data.Count == 0)
                        {
                            content.AlignCenter().AlignMiddle().Text("No data available").FontSize(12);
                            return;
                        }

                        content.Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(40);   // Year
                                cols.RelativeColumn(2);    // Department
                                cols.RelativeColumn(2);    // Fund Code
                                cols.RelativeColumn(2);    // Cost Center
                                cols.RelativeColumn(3);    // Commitment Item
                                cols.ConstantColumn(35);   // Posts
                                cols.RelativeColumn(3);    // Original Budget
                                cols.RelativeColumn(3);    // Revised Budget
                                cols.RelativeColumn(3);    // Variance
                            });

                            table.Header(header =>
                            {
                                foreach (var col in new[] { "Year", "Department", "Fund Code", "Cost Center", "Commitment Item", "Posts", "Original Budget (PKR)", "Revised Budget (PKR)", "Variance (PKR)" })
                                {
                                    header.Cell().Element(cell =>
                                    {
                                        cell.Background(Colors.Blue.Darken2).Padding(3)
                                            .Text(col).FontColor(Colors.White).Bold().FontSize(7);
                                    });
                                }
                            });

                            int rowIndex = 0;
                            foreach (var item in data.Take(500))
                            {
                                var bg = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                void DataCell(string val, bool rightAlign = false)
                                {
                                    table.Cell().Element(c =>
                                    {
                                        var container = c.Background(bg).Padding(3);
                                        if (rightAlign)
                                            container.AlignRight().Text(val).FontSize(7);
                                        else
                                            container.Text(val).FontSize(7);
                                    });
                                }
                                DataCell(item.FinancialYear.ToString());
                                DataCell(item.DepartmentCode);
                                DataCell(item.FundCode);
                                DataCell(item.CostCenterCode);
                                DataCell(item.CommitmentItem);
                                DataCell(item.Posts.ToString("N0"), true);
                                DataCell(item.OriginalBudgetAmount.ToString("N2"), true);
                                DataCell(item.RevisedBudgetAmount.ToString("N2"), true);

                                var varStr = item.Variance.ToString("N2");
                                table.Cell().Element(c =>
                                {
                                    var col = item.Variance < 0 ? Colors.Red.Darken2 : Colors.Green.Darken2;
                                    c.Background(bg).Padding(3).AlignRight().Text(varStr).FontSize(7).FontColor(col);
                                });
                                rowIndex++;
                            }

                            // Grand Total row
                            var totalOrig = data.Sum(r => r.OriginalBudgetAmount);
                            var totalRev = data.Sum(r => r.RevisedBudgetAmount);
                            var totalVar = data.Sum(r => r.Variance);
                            foreach (var v in new[] { "TOTAL", "", "", "", "", data.Sum(r => r.Posts).ToString("N0"), totalOrig.ToString("N2"), totalRev.ToString("N2"), totalVar.ToString("N2") })
                            {
                                table.Cell().Element(c =>
                                {
                                    c.Background(Colors.Blue.Lighten3).Padding(3).Text(v).Bold().FontSize(7);
                                });
                            }
                        });

                        if (data.Count > 500)
                        {
                            content.AlignCenter().PaddingTop(10)
                                .Text($"... and {data.Count - 500} more records. Export to Excel for complete data.")
                                .FontSize(8).FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().Element(footer =>
                    {
                        footer.Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Page ").FontSize(7);
                                text.CurrentPageNumber().FontSize(7);
                                text.Span(" of ").FontSize(7);
                                text.TotalPages().FontSize(7);
                            });
                            row.RelativeItem().AlignRight()
                                .Text("Finance Department Punjab - Cost Center Report")
                                .FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        public async Task<byte[]> GenerateDevBudgetPdfAsync(int? financialYear, List<string>? parentProjects, List<string>? gsNos = null, List<string>? sectors = null)
        {
            var data = await _budgetRepository.GetDevBudgetReportAsync(financialYear, parentProjects, gsNos, sectors);
            var title = $"Development Expenditure Report FY {(financialYear?.ToString() ?? "All")}";

            var navy = "#1A3A5C";
            var blue = "#2C5282";
            var lightBlue = "#EAF2FF";
            var altRow = "#F4F7FB";
            var borderColor = "#B9D4F5";

            var grouped = data.GroupBy(x => new { x.ParentProject, x.ProjectID, x.ProjectDescription, x.GsNo }).ToList();

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.2f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8).FontColor(Colors.Black));

                    page.Header().Element(header =>
                    {
                        header.Column(col =>
                        {
                            col.Item().Background(navy).Padding(10).Row(row =>
                            {
                                row.RelativeItem().Column(inner =>
                                {
                                    inner.Item().Text("Finance Department Punjab").FontSize(14).Bold().FontColor(Colors.White);
                                    inner.Item().Text(title).FontSize(11).SemiBold().FontColor(Colors.White);
                                });
                                row.ConstantItem(160).AlignRight().AlignMiddle()
                                    .Text($"Dated: {DateTime.Now:dd-MMM-yyyy}").FontSize(8).FontColor(Colors.White);
                            });
                            col.Item().PaddingTop(6);
                        });
                    });

                    page.Content().Element(content =>
                    {
                        if (data.Count == 0)
                        {
                            content.AlignCenter().AlignMiddle().Text("No data available").FontSize(12);
                            return;
                        }

                        content.Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn(3);   // Project
                                cols.RelativeColumn(3);   // GL Description
                                cols.RelativeColumn(2);   // Allocation
                                cols.RelativeColumn(2);   // Original Budget
                                cols.RelativeColumn(2);   // Supplementary
                                cols.RelativeColumn(2);   // Reapp +
                                cols.RelativeColumn(2);   // Reapp -
                                cols.RelativeColumn(2);   // Surrender
                                cols.RelativeColumn(2);   // Expenditure
                            });

                            table.Header(header =>
                            {
                                foreach (var col in new[] { "Project", "GL Description", "Allocation", "Original Budget", "Supplementary", "Reapp (+)", "Reapp (-)", "Surrender", "Expenditure" })
                                {
                                    header.Cell().Element(cell =>
                                    {
                                        cell.Background(navy)
                                            .Border(0.75f).BorderColor(navy)
                                            .Padding(4)
                                            .Text(col).FontColor(Colors.White).Bold().FontSize(7.5f);
                                    });
                                }
                            });

                            decimal gAl = 0, gOb = 0, gSb = 0, gRp = 0, gRm = 0, gSu = 0, gEx = 0;
                            int rowIndex = 0;

                            void DataRow(string project, string gl, decimal al, decimal ob, decimal sb, decimal rp, decimal rm, decimal su, decimal ex,
                                         string bg, string fg, bool bold = false)
                            {
                                void Cell(string val, bool rightAlign)
                                {
                                    table.Cell().Element(c =>
                                    {
                                        var cc = c.Background(bg).Border(0.5f).BorderColor(borderColor).Padding(4);
                                        var txt = rightAlign ? cc.AlignRight().Text(val) : cc.Text(val);
                                        txt.FontSize(7.5f).FontColor(fg);
                                        if (bold) txt.Bold();
                                    });
                                }
                                Cell(project, false);
                                Cell(gl, false);
                                Cell(al.ToString("N0"), true);
                                Cell(ob.ToString("N0"), true);
                                Cell(sb.ToString("N0"), true);
                                Cell(rp.ToString("N0"), true);
                                Cell(rm.ToString("N0"), true);
                                Cell(su.ToString("N0"), true);
                                Cell(ex.ToString("N0"), true);
                            }

                            foreach (var proj in grouped)
                            {
                                var pAl = proj.Sum(x => x.Allocation); var pOb = proj.Sum(x => x.OriginalBudget);
                                var pSb = proj.Sum(x => x.SupplementaryBudget); var pRp = proj.Sum(x => x.ReappPlus);
                                var pRm = proj.Sum(x => x.ReappMinus); var pSu = proj.Sum(x => x.Surrender);
                                var pEx = proj.Sum(x => x.ProgressiveExp);
                                var gsNoLabel = string.IsNullOrWhiteSpace(proj.Key.GsNo) ? "-" : proj.Key.GsNo;

                                DataRow($"{gsNoLabel} - {proj.Key.ProjectID} — {proj.Key.ProjectDescription}", "", pAl, pOb, pSb, pRp, pRm, pSu, pEx,
                                    blue, Colors.White, bold: true);

                                gAl += pAl; gOb += pOb; gSb += pSb; gRp += pRp; gRm += pRm; gSu += pSu; gEx += pEx;

                                foreach (var gl in proj)
                                {
                                    var rowBg = rowIndex % 2 == 0 ? lightBlue : altRow;
                                    DataRow("", string.IsNullOrWhiteSpace(gl.Gldesc) ? "(Unclassified)" : gl.Gldesc,
                                        gl.Allocation, gl.OriginalBudget, gl.SupplementaryBudget, gl.ReappPlus, gl.ReappMinus, gl.Surrender, gl.ProgressiveExp,
                                        rowBg, Colors.Black.ToString());
                                    rowIndex++;
                                }
                            }

                            DataRow("GRAND TOTAL", "", gAl, gOb, gSb, gRp, gRm, gSu, gEx, navy, Colors.White, bold: true);
                        });
                    });

                    page.Footer().Element(footer =>
                    {
                        footer.BorderTop(0.75f).BorderColor(borderColor).PaddingTop(4).Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Page ").FontSize(7).FontColor(Colors.Grey.Darken1);
                                text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Darken1);
                                text.Span(" of ").FontSize(7).FontColor(Colors.Grey.Darken1);
                                text.TotalPages().FontSize(7).FontColor(Colors.Grey.Darken1);
                            });
                            row.RelativeItem().AlignRight()
                                .Text("Finance Department Punjab - Development Expenditure Report")
                                .FontSize(7).FontColor(Colors.Grey.Darken1);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }

        private static byte[] GeneratePdf(List<object> data, List<string> columns, string title)
        {
            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4.Landscape());
                    page.Margin(1.5f, Unit.Centimetre);
                    page.PageColor(Colors.White);
                    page.DefaultTextStyle(x => x.FontSize(8));

                    page.Header().Element(header =>
                    {
                        header.Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Finance Department Punjab").FontSize(14).Bold();
                                col.Item().Text(title).FontSize(11).SemiBold();
                                col.Item().Text($"Generated: {DateTime.Now:dd-MMM-yyyy HH:mm}").FontSize(8).FontColor(Colors.Grey.Medium);
                            });
                        });
                    });

                    page.Content().Element(content =>
                    {
                        if (data.Count == 0)
                        {
                            content.AlignCenter().AlignMiddle().Text("No data available").FontSize(12);
                            return;
                        }

                        content.Table(table =>
                        {
                            // Define columns
                            table.ColumnsDefinition(columnDefs =>
                            {
                                foreach (var _ in columns)
                                {
                                    columnDefs.RelativeColumn();
                                }
                            });

                            // Header row
                            table.Header(header =>
                            {
                                foreach (var col in columns)
                                {
                                    header.Cell().Element(cell =>
                                    {
                                        cell.Background(Colors.Blue.Darken2)
                                            .Padding(3)
                                            .Text(col)
                                            .FontColor(Colors.White)
                                            .Bold();
                                    });
                                }
                            });

                            // Data rows
                            int rowIndex = 0;
                            foreach (var item in data.Take(500)) // Limit to 500 rows for PDF
                            {
                                var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;
                                var props = item.GetType().GetProperties();

                                foreach (var col in columns)
                                {
                                    var prop = props.FirstOrDefault(p => p.Name == col);
                                    var value = prop?.GetValue(item)?.ToString() ?? "";

                                    table.Cell().Element(cell =>
                                    {
                                        cell.Background(bgColor)
                                            .Padding(3)
                                            .Text(value)
                                            .FontSize(7);
                                    });
                                }
                                rowIndex++;
                            }
                        });

                        if (data.Count > 500)
                        {
                            content.AlignCenter().PaddingTop(10).Text($"... and {data.Count - 500} more records. Export to Excel for complete data.")
                                .FontSize(8).FontColor(Colors.Grey.Medium);
                        }
                    });

                    page.Footer().Element(footer =>
                    {
                        footer.Row(row =>
                        {
                            row.RelativeItem().Text(text =>
                            {
                                text.Span("Page ").FontSize(7);
                                text.CurrentPageNumber().FontSize(7);
                                text.Span(" of ").FontSize(7);
                                text.TotalPages().FontSize(7);
                            });
                            row.RelativeItem().AlignRight().Text("Finance Department Punjab - Budget Analysis System")
                                .FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                });
            });

            return document.GeneratePdf();
        }
    }
}
