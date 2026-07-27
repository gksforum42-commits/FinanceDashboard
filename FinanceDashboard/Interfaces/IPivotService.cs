using FinanceDashboard.ViewModels.Pivot;

namespace FinanceDashboard.Interfaces
{
    public interface IPivotService
    {
        /// <summary>
        /// Transforms raw grouped rows into an Excel-style pivot matrix with subtotals and grand totals.
        /// </summary>
        PivotResultViewModel BuildPivotResult(
            List<Dictionary<string, object?>> rawRows,
            PivotRequestViewModel request,
            int totalGroups);
    }
}
