using FinanceDashboard.ViewModels.Pivot;

namespace FinanceDashboard.Interfaces
{
    public interface IPivotRepository
    {
        /// <summary>
        /// Executes dynamic GROUP BY query and returns grouped rows ready for pivot transformation.
        /// </summary>
        Task<(List<Dictionary<string, object?>> Rows, int TotalGroups)> GetPivotRawDataAsync(
            PivotRequestViewModel request, CancellationToken ct = default);

        /// <summary>
        /// Returns distinct non-null values for a dimension column, filtered by parent dimension values.
        /// </summary>
        Task<List<PivotFilterLookupViewModel>> GetFilterLookupAsync(
            string column, Dictionary<string, List<string>> parentFilters, string? search, CancellationToken ct = default);

        /// <summary>
        /// Returns raw detail rows for drill-down popup.
        /// </summary>
        Task<PivotDrillResultViewModel> GetDrillDownDataAsync(
            Dictionary<string, string> dimensionFilters,
            List<string> valueFields,
            Dictionary<string, List<string>> globalFilters,
            int page, int pageSize, CancellationToken ct = default);
    }
}
