namespace FinanceDashboard.ViewModels.Pivot
{
    /// <summary>
    /// Top-level result returned to the client.
    /// </summary>
    public class PivotResultViewModel
    {
        /// <summary>Ordered list of column headers (when a column dimension is used).</summary>
        public List<string> ColumnHeaders { get; set; } = new();

        /// <summary>All value field names included in this result.</summary>
        public List<string> ValueFields { get; set; } = new();

        /// <summary>Flat list of pivot rows (each may carry subtotal/grandtotal flag).</summary>
        public List<PivotRowViewModel> Rows { get; set; } = new();

        /// <summary>Grand total row.</summary>
        public PivotRowViewModel? GrandTotal { get; set; }

        public int TotalRows { get; set; }
        public int Page { get; set; }
        public int PageSize { get; set; }
        public int TotalPages => PageSize > 0 ? (int)Math.Ceiling((double)TotalRows / PageSize) : 1;
    }

    /// <summary>
    /// A single row in the pivot grid.
    /// </summary>
    public class PivotRowViewModel
    {
        /// <summary>Dimension label at each hierarchy level.</summary>
        public List<string?> DimensionValues { get; set; } = new();

        /// <summary>
        /// Cell values keyed as "{columnHeader}|{valueField}" (or just "{valueField}" when no column dimension).
        /// </summary>
        public Dictionary<string, decimal> Cells { get; set; } = new();

        /// <summary>Row-level totals keyed by value field name.</summary>
        public Dictionary<string, decimal> RowTotals { get; set; } = new();

        public int Depth { get; set; }
        public bool IsSubTotal { get; set; }
        public bool IsGrandTotal { get; set; }
    }

    /// <summary>
    /// A single raw record returned for drill-down popups.
    /// </summary>
    public class PivotDrillRowViewModel
    {
        public Dictionary<string, object?> Fields { get; set; } = new();
    }

    /// <summary>
    /// Drill-down result envelope.
    /// </summary>
    public class PivotDrillResultViewModel
    {
        public List<string> Columns { get; set; } = new();
        public List<PivotDrillRowViewModel> Rows { get; set; } = new();
        public int TotalRows { get; set; }
    }

    /// <summary>
    /// A lookup item for filter dropdowns.
    /// </summary>
    public class PivotFilterLookupViewModel
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
