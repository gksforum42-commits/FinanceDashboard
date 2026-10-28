using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceDashboard.Controllers
{
    [Authorize(Policy = "NotSneDepartmentUser")]
    public class BudgetAnalysisController : Controller
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly IExcelService _excelService;
        private readonly IPdfService _pdfService;
        private readonly ILogger<BudgetAnalysisController> _logger;

        public BudgetAnalysisController(
            IBudgetRepository budgetRepository,
            IExcelService excelService,
            IPdfService pdfService,
            ILogger<BudgetAnalysisController> logger)
        {
            _budgetRepository = budgetRepository;
            _excelService = excelService;
            _pdfService = pdfService;
            _logger = logger;
        }

        // Original Budget
        public IActionResult OriginalBudget()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> GetOriginalBudgetData([FromBody] DataTableRequestViewModel request)
        {
            try
            {
                var departmentCode = User.ResolveDepartmentCode(request.DepartmentCode);
                var filters = new FilterViewModel
                {
                    FinancialYear = request.FinancialYear,
                    FundCode = request.FundCode,
                    CostCenterCode = null,
                    DepartmentCode = departmentCode,
                    Keyword = request.SearchValue
                };

                var pagination = new PaginationViewModel
                {
                    PageNumber = (request.Start / request.Length) + 1,
                    PageSize = request.Length,
                    SortColumn = request.SortColumn ?? "Id",
                    SortDirection = request.SortDirection ?? "asc"
                };

                var result = await _budgetRepository.GetOriginalBudgetAsync(filters, pagination);

                return Json(new DataTableResponseViewModel<dynamic>
                {
                    Draw = request.Draw,
                    RecordsTotal = result.Pagination.TotalRecords,
                    RecordsFiltered = result.Pagination.TotalRecords,
                    Data = result.Data.Cast<dynamic>().ToList()
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting original budget data");
                return Json(new { error = ex.Message });
            }
        }

        // Cost Center Report
        public IActionResult CostCenterReport()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterReportData(int? financialYear, string? fundCode, string? costCenterCode)
        {
            // Require at least a financial year to prevent full-table scans
            if (!financialYear.HasValue)
                return Json(new { success = true, data = Array.Empty<object>() });

            try
            {
                var data = await _budgetRepository.GetCostCenterReportAsync(financialYear, fundCode, costCenterCode);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost center report data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenters(int? financialYear, string? fundCode)
        {
            try
            {
                var data = await _budgetRepository.GetCostCenterLookupAsync(financialYear, fundCode);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost centers");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCostCenterReportExcel(int? financialYear, string? fundCode, string? costCenterCode)
        {
            try
            {
                var excelBytes = await _excelService.ExportCostCenterReportAsync(financialYear, fundCode, costCenterCode);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"CostCenterReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting cost center report to Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(nameof(CostCenterReport));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportCostCenterReportPdf(int? financialYear, string? fundCode, string? costCenterCode)
        {
            try
            {
                var pdfBytes = await _pdfService.GenerateCostCenterReportPdfAsync(financialYear, fundCode, costCenterCode);
                return File(pdfBytes, "application/pdf", $"CostCenterReport_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting cost center report to PDF");
                TempData["Error"] = "Failed to export PDF file.";
                return RedirectToAction(nameof(CostCenterReport));
            }
        }

        // Budget Comparison
        public IActionResult Comparison()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetComparisonData(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetBudgetComparisonAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting comparison data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Export Actions
        [HttpGet]
        public async Task<IActionResult> ExportOriginalBudgetExcel(int? financialYear, string? fundCode, string? departmentCode, string? keyword)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel { FinancialYear = financialYear, FundCode = fundCode, DepartmentCode = departmentCode, Keyword = keyword };
                var excelBytes = await _excelService.ExportOriginalBudgetAsync(filters);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"OriginalBudget_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting original budget to Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(nameof(OriginalBudget));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportOriginalBudgetPdf(int? financialYear, string? fundCode, string? departmentCode, string? keyword)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel { FinancialYear = financialYear, FundCode = fundCode, DepartmentCode = departmentCode, Keyword = keyword };
                var pdfBytes = await _pdfService.GenerateOriginalBudgetPdfAsync(filters);
                return File(pdfBytes, "application/pdf", $"OriginalBudget_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting original budget to PDF");
                TempData["Error"] = "Failed to export PDF file.";
                return RedirectToAction(nameof(OriginalBudget));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportBERExcel(int? financialYear, string? fundCode, string? departmentCode, string? keyword)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel { FinancialYear = financialYear, FundCode = fundCode, DepartmentCode = departmentCode, Keyword = keyword };
                var excelBytes = await _excelService.ExportBERDataAsync(filters);
                return File(excelBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"BER_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting BER to Excel");
                TempData["Error"] = "Failed to export Excel file.";
                return RedirectToAction(nameof(OriginalBudget));
            }
        }

        [HttpGet]
        public async Task<IActionResult> ExportBERPdf(int? financialYear, string? fundCode, string? departmentCode, string? keyword)
        {
            try
            {
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var filters = new FilterViewModel { FinancialYear = financialYear, FundCode = fundCode, DepartmentCode = departmentCode, Keyword = keyword };
                var pdfBytes = await _pdfService.GenerateBERPdfAsync(filters);
                return File(pdfBytes, "application/pdf", $"BER_{DateTime.Now:yyyyMMddHHmmss}.pdf");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting BER to PDF");
                TempData["Error"] = "Failed to export PDF file.";
                return RedirectToAction(nameof(OriginalBudget));
            }
        }
    }
}
