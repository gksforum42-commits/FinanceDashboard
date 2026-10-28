using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using FinanceDashboard.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace FinanceDashboard.Controllers
{
    [Authorize(Policy = "NotSneDepartmentUser")]
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
                var restrictedDepartments = User.GetRestrictedDepartments();
                var summary = await _budgetRepository.GetDashboardSummaryAsync(financialYear, restrictedDepartments);
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
                var restrictedDepartments = User.GetRestrictedDepartments();
                var chartData = await _budgetRepository.GetBudgetChartDataAsync(financialYear, restrictedDepartments);
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
                var restrictedDepartments = User.GetRestrictedDepartments();
                var data = await _budgetRepository.GetMonthlyExpenditureAsync(financialYear, fundCode, restrictedDepartments);
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
                var restrictedDepartments = User.GetRestrictedDepartments();
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var items = await _budgetRepository.GetFundLookupAsync(financialYear, departmentCode, sector, restrictedDepartments);
                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting fund lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterLookup(int? financialYear, string? fundCode, string? sector, string? departmentCode)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                departmentCode = User.ResolveDepartmentCode(departmentCode);
                var items = await _budgetRepository.GetCostCenterLookupAsync(financialYear, fundCode, sector, departmentCode, restrictedDepartments);
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
                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    items = items.Where(i => restrictedDepartments.Contains(i.Value)).ToList();
                }
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
                var restrictedDepartments = User.GetRestrictedDepartments();

                var summary = await _budgetRepository.GetDashboardSummaryAsync(financialYear, restrictedDepartments);
                var charts  = await _budgetRepository.GetBudgetChartDataAsync(financialYear, restrictedDepartments);
                var monthly = await _budgetRepository.GetMonthlyExpenditureAsync(year, null, restrictedDepartments);

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