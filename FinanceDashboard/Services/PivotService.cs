using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels.Pivot;

namespace FinanceDashboard.Services
{
    /// <summary>
    /// Transforms flat GROUP BY rows from the database into a full Excel-style pivot matrix
    /// with sub-totals at each hierarchy level and a grand total row.
    /// </summary>
    public class PivotService : IPivotService
    {
        public PivotResultViewModel BuildPivotResult(
            List<Dictionary<string, object?>> rawRows,
            PivotRequestViewModel request,
            int totalGroups)
        {
            var result = new PivotResultViewModel
            {
                ValueFields = request.ValueFields,
                TotalRows = totalGroups,
                Page = request.Page,
                PageSize = request.PageSize
            };

            if (!rawRows.Any())
                return result;

            var rowDims = request.RowDimensions;
            var colDim = request.ColumnDimension;
            var values = request.ValueFields;

            // ── Collect distinct column-dimension values ──────────────────────
            if (!string.IsNullOrEmpty(colDim))
            {
                result.ColumnHeaders = rawRows
                    .Select(r => r.TryGetValue(colDim, out var v) ? v?.ToString() ?? "(blank)" : "(blank)")
                    .Distinct()
                    .OrderBy(x => x)
                    .ToList();
            }

            // ── Group raw rows into hierarchy ─────────────────────────────────
            var pivotRows = new List<PivotRowViewModel>();

            if (string.IsNullOrEmpty(colDim))
            {
                // Simple pivot — no column dimension
                BuildHierarchyRows(rawRows, rowDims, values, 0, pivotRows, new List<string?>());
            }
            else
            {
                // Pivot with column dimension — merge rows by row-dimensions, spread across columns
                var merged = MergeByRowDimensions(rawRows, rowDims, colDim, values, result.ColumnHeaders);
                BuildHierarchyRowsWithColumns(merged, rowDims, values, result.ColumnHeaders, 0, pivotRows, new List<string?>());
            }

            result.Rows = pivotRows;

            // ── Grand Total ───────────────────────────────────────────────────
            result.GrandTotal = BuildGrandTotal(pivotRows, values, result.ColumnHeaders, rowDims.Count);

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  No Column Dimension
        // ════════════════════════════════════════════════════════════════════
        private static void BuildHierarchyRows(
            List<Dictionary<string, object?>> rows,
            List<string> dims,
            List<string> values,
            int depth,
            List<PivotRowViewModel> output,
            List<string?> parentValues)
        {
            if (depth >= dims.Count) return;

            var dim = dims[depth];
            var grouped = rows
                .GroupBy(r => r.TryGetValue(dim, out var v) ? v?.ToString() ?? "(blank)" : "(blank)")
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var dimValues = new List<string?>(parentValues) { group.Key };

                // ── Data row ──────────────────────────────────────────────────
                if (depth == dims.Count - 1)
                {
                    // Leaf row — sum values
                    var row = new PivotRowViewModel
                    {
                        DimensionValues = PadDims(dimValues, dims.Count),
                        Depth = depth
                    };
                    foreach (var v in values)
                    {
                        var sum = group.Sum(r => r.TryGetValue(v, out var val) ? ToDecimal(val) : 0m);
                        row.Cells[v] = sum;
                        row.RowTotals[v] = sum;
                    }
                    output.Add(row);
                }
                else
                {
                    // Sub-group: recurse first, then add subtotal after children
                    BuildHierarchyRows(group.ToList(), dims, values, depth + 1, output, dimValues);

                    // Subtotal row for this group
                    var subTotal = new PivotRowViewModel
                    {
                        DimensionValues = PadDims(dimValues, dims.Count),
                        Depth = depth,
                        IsSubTotal = true
                    };
                    foreach (var v in values)
                    {
                        var sum = group.Sum(r => r.TryGetValue(v, out var val) ? ToDecimal(val) : 0m);
                        subTotal.Cells[v] = sum;
                        subTotal.RowTotals[v] = sum;
                    }
                    output.Add(subTotal);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  With Column Dimension — merge then hierarchy
        // ════════════════════════════════════════════════════════════════════
        private static List<Dictionary<string, object?>> MergeByRowDimensions(
            List<Dictionary<string, object?>> rows,
            List<string> rowDims,
            string colDim,
            List<string> values,
            List<string> colHeaders)
        {
            // Key = tuple of row-dimension values
            var dict = new Dictionary<string, Dictionary<string, object?>>();

            foreach (var row in rows)
            {
                var key = string.Join("||", rowDims.Select(d => row.TryGetValue(d, out var v) ? v?.ToString() ?? "" : ""));
                if (!dict.TryGetValue(key, out var merged))
                {
                    merged = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var d in rowDims)
                        merged[d] = row.TryGetValue(d, out var dv) ? dv : null;
                    dict[key] = merged;
                }

                var colVal = row.TryGetValue(colDim, out var cv) ? cv?.ToString() ?? "(blank)" : "(blank)";
                foreach (var v in values)
                {
                    var cellKey = $"{colVal}|{v}";
                    var existing = merged.TryGetValue(cellKey, out var ev) ? ToDecimal(ev) : 0m;
                    merged[cellKey] = existing + (row.TryGetValue(v, out var rv) ? ToDecimal(rv) : 0m);
                }
            }

            return dict.Values.ToList();
        }

        private static void BuildHierarchyRowsWithColumns(
            List<Dictionary<string, object?>> rows,
            List<string> dims,
            List<string> values,
            List<string> colHeaders,
            int depth,
            List<PivotRowViewModel> output,
            List<string?> parentValues)
        {
            if (depth >= dims.Count) return;

            var dim = dims[depth];
            var grouped = rows
                .GroupBy(r => r.TryGetValue(dim, out var v) ? v?.ToString() ?? "(blank)" : "(blank)")
                .OrderBy(g => g.Key);

            foreach (var group in grouped)
            {
                var dimValues = new List<string?>(parentValues) { group.Key };

                if (depth == dims.Count - 1)
                {
                    var row = new PivotRowViewModel
                    {
                        DimensionValues = PadDims(dimValues, dims.Count),
                        Depth = depth
                    };
                    foreach (var col in colHeaders)
                    {
                        foreach (var v in values)
                        {
                            var ck = $"{col}|{v}";
                            row.Cells[ck] = group.Sum(r => r.TryGetValue(ck, out var val) ? ToDecimal(val) : 0m);
                        }
                    }
                    foreach (var v in values)
                        row.RowTotals[v] = group.Sum(r => colHeaders.Sum(c => r.TryGetValue($"{c}|{v}", out var val) ? ToDecimal(val) : 0m));
                    output.Add(row);
                }
                else
                {
                    BuildHierarchyRowsWithColumns(group.ToList(), dims, values, colHeaders, depth + 1, output, dimValues);

                    var subTotal = new PivotRowViewModel
                    {
                        DimensionValues = PadDims(dimValues, dims.Count),
                        Depth = depth,
                        IsSubTotal = true
                    };
                    foreach (var col in colHeaders)
                    {
                        foreach (var v in values)
                        {
                            var ck = $"{col}|{v}";
                            subTotal.Cells[ck] = group.Sum(r => r.TryGetValue(ck, out var val) ? ToDecimal(val) : 0m);
                        }
                    }
                    foreach (var v in values)
                        subTotal.RowTotals[v] = group.Sum(r => colHeaders.Sum(c => r.TryGetValue($"{c}|{v}", out var val) ? ToDecimal(val) : 0m));
                    output.Add(subTotal);
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════
        //  Grand Total
        // ════════════════════════════════════════════════════════════════════
        private static PivotRowViewModel BuildGrandTotal(
            List<PivotRowViewModel> rows,
            List<string> values,
            List<string> colHeaders,
            int dimCount)
        {
            var gt = new PivotRowViewModel
            {
                DimensionValues = Enumerable.Repeat<string?>("Grand Total", 1)
                    .Concat(Enumerable.Repeat<string?>(null, dimCount - 1)).ToList(),
                IsGrandTotal = true,
                Depth = 0
            };

            // Only sum leaf rows (not subtotals) to avoid double-counting
            var leafRows = rows.Where(r => !r.IsSubTotal && !r.IsGrandTotal).ToList();

            if (colHeaders.Any())
            {
                foreach (var col in colHeaders)
                    foreach (var v in values)
                    {
                        var ck = $"{col}|{v}";
                        gt.Cells[ck] = leafRows.Sum(r => r.Cells.TryGetValue(ck, out var cv) ? cv : 0m);
                    }
            }

            foreach (var v in values)
                gt.RowTotals[v] = leafRows.Sum(r => r.RowTotals.TryGetValue(v, out var tv) ? tv : 0m);

            return gt;
        }

        // ── Utilities ─────────────────────────────────────────────────────────
        private static List<string?> PadDims(List<string?> dims, int count)
        {
            var result = new List<string?>(dims);
            while (result.Count < count) result.Add(null);
            return result;
        }

        private static decimal ToDecimal(object? val)
        {
            if (val is null) return 0m;
            if (val is decimal d) return d;
            if (val is double dbl) return (decimal)dbl;
            if (val is float f) return (decimal)f;
            if (val is int i) return i;
            if (val is long l) return l;
            if (decimal.TryParse(val.ToString(), out var parsed)) return parsed;
            return 0m;
        }
    }
}
