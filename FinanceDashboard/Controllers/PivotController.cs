using FinanceDashboard.Interfaces;
using FinanceDashboard.Services;
using FinanceDashboard.ViewModels.Pivot;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace FinanceDashboard.Controllers
{
    [Authorize(Policy = "NotSneDepartmentUser")]
    public class PivotController : Controller
    {
        private readonly IPivotRepository _repo;
        private readonly IPivotService _service;
        private readonly IPivotExportService _exportService;
        private readonly IMemoryCache _cache;
        private readonly ILogger<PivotController> _logger;

        private static readonly TimeSpan FilterCacheDuration = TimeSpan.FromMinutes(15);

        public PivotController(
            IPivotRepository repo,
            IPivotService service,
            IPivotExportService exportService,
            IMemoryCache cache,
            ILogger<PivotController> logger)
        {
            _repo = repo;
            _service = service;
            _exportService = exportService;
            _cache = cache;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Execute Pivot
        // ════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Execute(
            [FromBody] PivotRequestViewModel? request,
            CancellationToken ct)
        {
            try
            {
                if (request == null)
                    return Json(new { success = false, message = "Invalid request payload." });

                if (request.RowDimensions == null || !request.RowDimensions.Any())
                    return Json(new { success = false, message = "At least one row dimension is required." });

                if (request.ValueFields == null || !request.ValueFields.Any())
                    return Json(new { success = false, message = "At least one value field is required." });

                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    request.Filters ??= new Dictionary<string, List<string>>();
                    request.Filters["MinistryDepartment"] = restrictedDepartments;
                    if (request.DrillFilters != null && request.DrillFilters.ContainsKey("MinistryDepartment"))
                        request.DrillFilters["MinistryDepartment"] = restrictedDepartments[0];
                }

                var (rawRows, totalGroups) = await _repo.GetPivotRawDataAsync(request, ct);
                var pivotResult = _service.BuildPivotResult(rawRows, request, totalGroups);

                return Json(new { success = true, data = pivotResult });
            }
            catch (OperationCanceledException)
            {
                return Json(new { success = false, message = "Request cancelled." });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error executing pivot report");
#if DEBUG
                return Json(new { success = false, message = ex.Message });
#else
                return Json(new { success = false, message = "Failed to generate pivot report. Please try again." });
#endif
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Filter Lookup (cached)
        // ════════════════════════════════════════════════════════════════════
        [HttpGet]
        public async Task<IActionResult> GetFilterLookup(
            string column,
            string? search,
            [FromQuery] Dictionary<string, string>? parentFilters,
            CancellationToken ct)
        {
            try
            {
                var parentDict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                if (parentFilters != null)
                    foreach (var kv in parentFilters)
                        if (!string.IsNullOrWhiteSpace(kv.Value))
                            parentDict[kv.Key] = kv.Value.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();

                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    if (string.Equals(column, "MinistryDepartment", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(column, "DepartmentName", StringComparison.OrdinalIgnoreCase))
                        return Json(new { success = true, data = new List<PivotFilterLookupViewModel>() });
                    parentDict["MinistryDepartment"] = restrictedDepartments;
                }

                // Cache key includes column + sorted parent filters + search
                var cacheKey = $"pivot_filter_{column}_{string.Join("_", parentDict.OrderBy(k => k.Key).Select(k => k.Key + "=" + string.Join(",", k.Value)))}_{search}";

                if (!_cache.TryGetValue(cacheKey, out List<PivotFilterLookupViewModel>? items))
                {
                    items = await _repo.GetFilterLookupAsync(column, parentDict, search, ct);
                    _cache.Set(cacheKey, items, FilterCacheDuration);
                }

                return Json(new { success = true, data = items });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting filter lookup for {Column}", column);
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Drill Down
        // ════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DrillDown(
            [FromBody] PivotDrillRequestViewModel request,
            CancellationToken ct)
        {
            try
            {
                var globalFilters = request.GlobalFilters ?? new Dictionary<string, List<string>>();
                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                    globalFilters["MinistryDepartment"] = restrictedDepartments;

                var result = await _repo.GetDrillDownDataAsync(
                    request.DimensionFilters ?? new Dictionary<string, string>(),
                    request.ValueFields ?? new List<string>(),
                    globalFilters,
                    request.Page, request.PageSize, ct);

                return Json(new { success = true, data = result });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in pivot drill-down");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Export Excel
        // ════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportExcel(
            [FromBody] PivotRequestViewModel request,
            CancellationToken ct)
        {
            try
            {
                request.PageSize = 100_000; // Export all
                request.Page = 1;
                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    request.Filters ??= new Dictionary<string, List<string>>();
                    request.Filters["MinistryDepartment"] = restrictedDepartments;
                }
                var (rawRows, totalGroups) = await _repo.GetPivotRawDataAsync(request, ct);
                var pivotResult = _service.BuildPivotResult(rawRows, request, totalGroups);
                var bytes = await _exportService.ExportExcelAsync(pivotResult, request);
                return File(bytes,
                    "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                    $"PivotReport_{DateTime.Now:yyyyMMddHHmmss}.xlsx");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pivot to Excel");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Export CSV
        // ════════════════════════════════════════════════════════════════════
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExportCsv(
            [FromBody] PivotRequestViewModel request,
            CancellationToken ct)
        {
            try
            {
                request.PageSize = 100_000;
                request.Page = 1;
                var restrictedDepartments = User.GetRestrictedDepartments();
                if (restrictedDepartments != null && restrictedDepartments.Count > 0)
                {
                    request.Filters ??= new Dictionary<string, List<string>>();
                    request.Filters["MinistryDepartment"] = restrictedDepartments;
                }
                var (rawRows, totalGroups) = await _repo.GetPivotRawDataAsync(request, ct);
                var pivotResult = _service.BuildPivotResult(rawRows, request, totalGroups);
                var bytes = await _exportService.ExportCsvAsync(pivotResult, request);
                return File(bytes, "text/csv", $"PivotReport_{DateTime.Now:yyyyMMddHHmmss}.csv");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error exporting pivot to CSV");
                return Json(new { success = false, message = ex.Message });
            }
        }
    }

    /// <summary>Request body for drill-down calls.</summary>
    public class PivotDrillRequestViewModel
    {
        public Dictionary<string, string>? DimensionFilters { get; set; }
        public List<string>? ValueFields { get; set; }
        public Dictionary<string, List<string>>? GlobalFilters { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 100;
    }
}
