using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceDashboard.Controllers
{
    [Authorize(Policy = "NotDepartmentUser")]
    [Authorize(Policy = "NotSneDepartmentUser")]
    public class SummaryReportsController : Controller
    {
        private readonly IBudgetRepository _budgetRepository;
        private readonly ILogger<SummaryReportsController> _logger;

        public SummaryReportsController(IBudgetRepository budgetRepository, ILogger<SummaryReportsController> logger)
        {
            _budgetRepository = budgetRepository;
            _logger = logger;
        }

        public IActionResult NineItemsReport()
        {
            return View();
        }

        public IActionResult DeptWiseReport()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetNineItemsData(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetNineItemsReportAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting 9-items report data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDeptWiseData(int? financialYear, List<string>? sectors, List<string>? departmentCodes)
        {
            try
            {
                var restrictedDepartments = User.GetRestrictedDepartments();
                departmentCodes = User.ResolveDepartmentCodes(departmentCodes);
                var data = await _budgetRepository.GetDeptWiseReportAsync(financialYear, sectors, departmentCodes, restrictedDepartments);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department-wise report data");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetSectorLookup(int? financialYear)
        {
            try
            {
                var data = await _budgetRepository.GetSectorLookupAsync(financialYear);
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting sector lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetDepartmentLookup(int? financialYear, string? sector, List<string>? sectors)
        {
            try
            {
                var data = await _budgetRepository.GetDepartmentLookupAsync(financialYear, sector, sectors);
                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    data = data.Where(i => restrictedDepartments.Contains(i.Value)).ToList();
                }
                return Json(new { success = true, data });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting department lookup");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}
