using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinanceDashboard.Controllers
{
    [Authorize]
    public class DashboardController : Controller
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(IBudgetRepository budgetRepository, ILogger<DashboardController> logger)
        {
            _budgetRepository = budgetRepository;
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetSummary(int? financialYear)
        {
            try
            {
                var summary = await _budgetRepository.GetDashboardSummaryAsync(financialYear);
                return Json(new { success = true, data = summary });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard summary");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetChartData(int? financialYear)
        {
            try
            {
                var chartData = await _budgetRepository.GetBudgetChartDataAsync(financialYear);
                return Json(new { success = true, data = chartData });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting chart data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetMonthlyExpenditure(int financialYear, string? fundCode)
        {
            try
            {
                var data = await _budgetRepository.GetMonthlyExpenditureAsync(financialYear, fundCode);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting monthly expenditure");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFinancialYears()
        {
            try
            {
                var years = await _budgetRepository.GetFinancialYearsAsync();
                var items = years.Select(y => new SelectListItem { Value = y.ToString(), Text = y.ToString() }).ToList();
                items.Insert(0, new SelectListItem { Value = "", Text = "-- All Years --" });
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting financial years");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetLookups(string type, string? search)
        {
            try
            {
                var items = await _budgetRepository.GetLookupValuesAsync(type, search);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting lookups");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetFundLookup(int? financialYear, string? departmentCode, string? sector)
        {
            try
            {
                var items = await _budgetRepository.GetFundLookupAsync(financialYear, departmentCode, sector);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fund lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterLookup(int? financialYear, string? fundCode)
        {
            try
            {
                var items = await _budgetRepository.GetCostCenterLookupAsync(financialYear, fundCode);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cost center lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentLookup(int? financialYear, string? sector)
        {
            try
            {
                var items = await _budgetRepository.GetDepartmentLookupAsync(financialYear, sector);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSectorLookup(int? financialYear)
        {
            try
            {
                var items = await _budgetRepository.GetSectorLookupAsync(financialYear);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }


        /// <summary>
        /// Single endpoint: fetches summary + chart + monthly expenditure in parallel.
        /// One round-trip from the browser instead of three.
        /// </summary>
        [HttpGet]
        public async Task<IActionResult> GetAll(int? financialYear)
        {
            try
            {
                int year = financialYear ?? DateTime.Now.Year;

                var summary = await _budgetRepository.GetDashboardSummaryAsync(financialYear);
                var charts  = await _budgetRepository.GetBudgetChartDataAsync(financialYear);
                var monthly = await _budgetRepository.GetMonthlyExpenditureAsync(year);

                return Json(new
                {
                    success = true,
                    summary,
                    charts,
                    monthly
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting dashboard data");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}