using FinanceDashboard.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FinanceDashboard.Controllers
{
    [Authorize]
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
    }
}
