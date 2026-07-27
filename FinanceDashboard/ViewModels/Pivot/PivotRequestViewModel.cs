namespace FinanceDashboard.ViewModels.Pivot
{
    /// <summary>
    /// Sent by the client to define how to build the pivot report.
    /// </summary>
    public class PivotRequestViewModel
    {
        /// <summary>Row dimensions in hierarchy order (e.g. ["Sector","DepartmentName","FundCode"]).</summary>
        public List<string> RowDimensions { get; set; } = new();

        /// <summary>Optional single column dimension (e.g. "FinancialYear").</summary>
        public string? ColumnDimension { get; set; }

        /// <summary>Value fields to aggregate (e.g. ["FinalGrant","ActualExpenditure"]).</summary>
        public List<string> ValueFields { get; set; } = new();

        /// <summary>Active filter values keyed by column name.</summary>
        public Dictionary<string, List<string>> Filters { get; set; } = new();

        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 500;

        /// <summary>For drill-down — additional fixed dimension values to filter raw rows.</summary>
        public Dictionary<string, string>? DrillFilters { get; set; }
    }
}
