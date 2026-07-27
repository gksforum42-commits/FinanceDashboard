using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels.Pivot;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text;

namespace FinanceDashboard.Repositories
{
    /// <summary>
    /// Raw ADO.NET repository for the pivot system. Uses dynamic but fully parameterised SQL
    /// and a strict column whitelist to prevent SQL-injection while supporting arbitrary groupings.
    /// </summary>
    public class PivotRepository : IPivotRepository
    {
        private readonly IConfiguration _config;
        private readonly ILogger<PivotRepository> _logger;

        // ── Whitelist of allowed dimension columns — must match actual BERData columns ──
        private static readonly HashSet<string> AllowedDimensions = new(StringComparer.OrdinalIgnoreCase)
        {
            "FinancialYear",
            "Sector",
            "FundCode",
            "Fund_Des",
            "FundCenterCode",
            "FundCenterDescription",
            "MinistryDepartment",
            "DepartmentName",
            "AttachedDepartment",
            "SubDetailFunction",
            "DetailObject",
            "GL_Desc",
            "ProfitCenter"
        };

        // ── Whitelist of allowed value (financial) columns ───────────────────
        private static readonly HashSet<string> AllowedValues = new(StringComparer.OrdinalIgnoreCase)
        {
            "AllocatedBudget",
            "BudgetGrant", "SupplementaryGrant",
            "ReappropriationPlus", "ReappropriationMinus", "TSSSurrender",
            "FinalGrant", "Releases", "ActualExpenditure", "FinalGrantBalance"
        };

        // ── SQL column name to actual DB column (handles aliases) ────────────
        private static readonly Dictionary<string, string> ColumnMap = new(StringComparer.OrdinalIgnoreCase)
        {
            ["AllocatedBudget"] = "allocated_budget",
            ["DetailObject"]    = "DetailObject",
            ["GL_Desc"]         = "GL_Desc",
        };

        // ── For filter dropdowns: column → companion description column ──────
        private static readonly Dictionary<string, string> FilterDescColumn = new(StringComparer.OrdinalIgnoreCase)
        {
            ["FundCode"]            = "Fund_Des",
            ["FundCenterCode"]      = "FundCenterDescription",
            ["MinistryDepartment"]  = "DepartmentName",
            ["DetailObject"]        = "GL_Desc",
        };

        private string GetDbColumn(string col)
            => ColumnMap.TryGetValue(col, out var mapped) ? mapped : col;

        public PivotRepository(IConfiguration config, ILogger<PivotRepository> logger)
        {
            _config = config;
            _logger = logger;
        }

        // ════════════════════════════════════════════════════════════════════
        //  GetPivotRawDataAsync
        // ════════════════════════════════════════════════════════════════════
        public async Task<(List<Dictionary<string, object?>> Rows, int TotalGroups)> GetPivotRawDataAsync(
            PivotRequestViewModel req, CancellationToken ct = default)
        {
            // --- Validate dimensions & values ---
            var rowDims = req.RowDimensions
                .Where(d => AllowedDimensions.Contains(d))
                .Select(MapToDbCol).ToList();

            var colDim = req.ColumnDimension != null && AllowedDimensions.Contains(req.ColumnDimension)
                ? MapToDbCol(req.ColumnDimension) : null;

            // Keep (originalName, dbColName) pairs so the SELECT alias matches the
            // original field name that PivotService will look up in the result dictionary.
            var values = req.ValueFields
                .Where(v => AllowedValues.Contains(v))
                .Select(v => (Original: v, DbCol: GetDbColumn(v)))
                .ToList();

            if (!rowDims.Any() || !values.Any())
                return (new List<Dictionary<string, object?>>(), 0);

            // --- Build GROUP BY columns ---
            var groupCols = new List<string>(rowDims);
            if (colDim != null) groupCols.Add(colDim);

            var selectParts = groupCols.Select(c => $"[{c}]").ToList();
            // Alias each aggregate back to the original field name so the result keys
            // match what PivotService expects (e.g. "AllocatedBudget", not "allocated_budget").
            foreach (var (orig, db) in values)
                selectParts.Add($"SUM([{db}]) AS [{orig}]");

            var groupByClause = string.Join(", ", groupCols.Select(c => $"[{c}]"));
            var selectClause  = string.Join(", ", selectParts);
            // The DB column list used purely for GROUP BY / ORDER BY (no aliases)
            var valueDbCols = values.Select(v => v.DbCol).ToList();

            // --- WHERE clause with parameters ---
            var (whereClause, sqlParams) = BuildWhereClause(req.Filters);

            // --- Count query ---
            var countSql = $@"
                SELECT COUNT(*) FROM (
                    SELECT {groupByClause}
                    FROM [budget].[BERData]
                    {whereClause}
                    GROUP BY {groupByClause}
                ) _cnt";

            // --- Data query with pagination ---
            var orderClause = string.Join(", ", groupCols.Select(c => $"[{c}]"));
            var offset = (req.Page - 1) * req.PageSize;

            var dataSql = $@"
                SELECT {selectClause}
                FROM [budget].[BERData]
                {whereClause}
                GROUP BY {groupByClause}
                ORDER BY {orderClause}
                OFFSET {offset} ROWS FETCH NEXT {req.PageSize} ROWS ONLY";

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync(ct);

            // Count
            int totalGroups = 0;
            using (var countCmd = new SqlCommand(countSql, conn) { CommandTimeout = 120 })
            {
                AddParameters(countCmd, sqlParams);
                var countResult = await countCmd.ExecuteScalarAsync(ct);
                totalGroups = Convert.ToInt32(countResult);
            }

            // Data
            var rows = new List<Dictionary<string, object?>>();
            using var cmd = new SqlCommand(dataSql, conn) { CommandTimeout = 120 };
            AddParameters(cmd, sqlParams);

            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < reader.FieldCount; i++)
                    dict[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                rows.Add(dict);
            }

            return (rows, totalGroups);
        }

        // ════════════════════════════════════════════════════════════════════
        //  GetFilterLookupAsync
        // ════════════════════════════════════════════════════════════════════
        public async Task<List<PivotFilterLookupViewModel>> GetFilterLookupAsync(
            string column, Dictionary<string, List<string>> parentFilters, string? search, CancellationToken ct = default)
        {
            if (!AllowedDimensions.Contains(column))
                return new List<PivotFilterLookupViewModel>();

            var dbCol = MapToDbCol(column);
            var (whereClause, sqlParams) = BuildWhereClause(parentFilters);

            var searchParam = new List<(string, object?)>();
            var searchSql = "";

            // Check if this column has a companion description column
            var hasDesc = FilterDescColumn.TryGetValue(column, out var descCol);

            if (!string.IsNullOrWhiteSpace(search))
            {
                if (hasDesc)
                    searchSql = $" AND (CAST([{dbCol}] AS NVARCHAR(500)) LIKE @search OR CAST([{descCol}] AS NVARCHAR(500)) LIKE @search)";
                else
                    searchSql = $" AND CAST([{dbCol}] AS NVARCHAR(500)) LIKE @search";
                searchParam.Add(("@search", $"%{search.Trim()}%"));
            }

            string sql;
            // Use a larger cap for the initial full load; narrow it when searching
            int topN = string.IsNullOrWhiteSpace(search) ? 5000 : 500;
            if (hasDesc)
            {
                sql = $@"
                    SELECT TOP {topN}
                        CAST([{dbCol}] AS NVARCHAR(500))  AS Val,
                        CAST([{descCol}] AS NVARCHAR(500)) AS Desc
                    FROM [budget].[BERData]
                    {whereClause}
                    {searchSql}
                    GROUP BY [{dbCol}], [{descCol}]
                    ORDER BY [{dbCol}]";
            }
            else
            {
                sql = $@"
                    SELECT TOP {topN} CAST([{dbCol}] AS NVARCHAR(500)) AS Val
                    FROM [budget].[BERData]
                    {whereClause}
                    {searchSql}
                    GROUP BY [{dbCol}]
                    ORDER BY [{dbCol}]";
            }

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync(ct);
            using var cmd = new SqlCommand(sql, conn) { CommandTimeout = 60 };
            AddParameters(cmd, sqlParams);
            foreach (var (pName, pVal) in searchParam)
                cmd.Parameters.AddWithValue(pName, pVal ?? DBNull.Value);

            var result = new List<PivotFilterLookupViewModel>();
            using var reader = await cmd.ExecuteReaderAsync(ct);
            while (await reader.ReadAsync(ct))
            {
                var val  = reader.IsDBNull(0) ? "" : reader.GetString(0);
                if (string.IsNullOrWhiteSpace(val)) continue;

                string text;
                if (hasDesc && reader.FieldCount > 1)
                {
                    var desc = reader.IsDBNull(1) ? "" : reader.GetString(1);
                    text = string.IsNullOrWhiteSpace(desc) ? val : $"{val} – {desc}";
                }
                else
                {
                    text = val;
                }
                result.Add(new PivotFilterLookupViewModel { Value = val, Text = text });
            }
            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  GetDrillDownDataAsync
        // ════════════════════════════════════════════════════════════════════
        public async Task<PivotDrillResultViewModel> GetDrillDownDataAsync(
            Dictionary<string, string> dimensionFilters,
            List<string> valueFields,
            Dictionary<string, List<string>> globalFilters,
            int page, int pageSize, CancellationToken ct = default)
        {
            var safeValues = valueFields
                .Where(v => AllowedValues.Contains(v))
                .Select(v => (Original: v, DbCol: MapToDbCol(v)))
                .ToList();

            // Only include meaningful display dimensions in the drill-down detail table
            var displayDimensions = new[]
            {
                "FinancialYear", "Sector", "DepartmentName", "FundCode", "Fund_Des",
                "FundCenterCode", "FundCenterDescription", "SubDetailFunction",
                "DetailObject", "GL_Desc", "ProfitCenter"
            }.Where(AllowedDimensions.Contains).ToList();

            // Dim columns — straightforward, no alias needed
            var selectCols = displayDimensions.Select(d => $"[{MapToDbCol(d)}] AS [{d}]").ToList();
            // Value columns — alias back to logical name so drill result headers are clean
            selectCols.AddRange(safeValues.Select(v => $"[{v.DbCol}] AS [{v.Original}]"));

            var (globalWhere, globalParams) = BuildWhereClause(globalFilters);

            // Add drill dimension filters
            var drillSb = new StringBuilder();
            var drillParams = new List<(string, object?)>(globalParams);
            int pi = globalParams.Count;
            foreach (var kv in dimensionFilters)
            {
                if (!AllowedDimensions.Contains(kv.Key)) continue;
                var pName = $"@dp{pi++}";
                drillSb.Append($" AND [{MapToDbCol(kv.Key)}] = {pName}");
                drillParams.Add((pName, kv.Value));
            }

            var whereAll = string.IsNullOrEmpty(globalWhere)
                ? (drillSb.Length > 0 ? "WHERE 1=1" + drillSb : "")
                : globalWhere + drillSb;

            var offset = (page - 1) * pageSize;
            var selectClause = string.Join(", ", selectCols);

            var countSql = $"SELECT COUNT(*) FROM [budget].[BERData] {whereAll}";
            var dataSql = $@"
                SELECT {selectClause}
                FROM [budget].[BERData]
                {whereAll}
                ORDER BY (SELECT NULL)
                OFFSET {offset} ROWS FETCH NEXT {pageSize} ROWS ONLY";

            using var conn = new SqlConnection(_config.GetConnectionString("DefaultConnection"));
            await conn.OpenAsync(ct);

            int total = 0;
            using (var cntCmd = new SqlCommand(countSql, conn) { CommandTimeout = 60 })
            {
                AddParameters(cntCmd, drillParams);
                total = Convert.ToInt32(await cntCmd.ExecuteScalarAsync(ct));
            }

            var result = new PivotDrillResultViewModel { TotalRows = total };
            using var dataCmd = new SqlCommand(dataSql, conn) { CommandTimeout = 60 };
            AddParameters(dataCmd, drillParams);

            using var reader = await dataCmd.ExecuteReaderAsync(ct);
            for (int i = 0; i < reader.FieldCount; i++)
                result.Columns.Add(reader.GetName(i));

            while (await reader.ReadAsync(ct))
            {
                var row = new PivotDrillRowViewModel();
                for (int i = 0; i < reader.FieldCount; i++)
                    row.Fields[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                result.Rows.Add(row);
            }

            return result;
        }

        // ════════════════════════════════════════════════════════════════════
        //  Helpers
        // ════════════════════════════════════════════════════════════════════

        /// <summary>Maps a logical column name to the physical DB column name.</summary>
        private static string MapToDbCol(string col)
            => ColumnMap.TryGetValue(col, out var mapped) ? mapped : col;

        private static (string Clause, List<(string, object?)> Params) BuildWhereClause(
            Dictionary<string, List<string>>? filters)
        {
            if (filters == null || !filters.Any())
                return ("", new List<(string, object?)>());

            var sb    = new StringBuilder("WHERE 1=1");
            var parms = new List<(string, object?)>();
            int idx   = 0;

            foreach (var kv in filters)
            {
                if (!AllowedDimensions.Contains(kv.Key)) continue;
                var activeVals = kv.Value.Where(v => !string.IsNullOrWhiteSpace(v)).ToList();
                if (!activeVals.Any()) continue;

                var dbCol = MapToDbCol(kv.Key);

                // FinancialYear is an INT — use exact equality, no LIKE/CAST needed
                bool isInt = string.Equals(kv.Key, "FinancialYear", StringComparison.OrdinalIgnoreCase);

                if (activeVals.Count == 1)
                {
                    var p = $"@f{idx++}";
                    if (isInt)
                    {
                        sb.Append($" AND [{dbCol}] = {p}");
                        parms.Add((p, int.TryParse(activeVals[0], out var iv) ? iv : (object)activeVals[0]));
                    }
                    else
                    {
                        sb.Append($" AND CAST([{dbCol}] AS NVARCHAR(500)) LIKE {p}");
                        parms.Add((p, $"%{activeVals[0]}%"));
                    }
                }
                else
                {
                    var inParms = activeVals.Select(_ => $"@f{idx++}").ToList();
                    sb.Append($" AND [{dbCol}] IN ({string.Join(",", inParms)})");
                    for (int i = 0; i < activeVals.Count; i++)
                    {
                        object val = isInt && int.TryParse(activeVals[i], out var iv2)
                            ? iv2 : (object)activeVals[i];
                        parms.Add((inParms[i], val));
                    }
                }
            }

            return (sb.ToString(), parms);
        }

        private static void AddParameters(SqlCommand cmd, List<(string Name, object? Val)> parms)
        {
            foreach (var (name, val) in parms)
                cmd.Parameters.AddWithValue(name, val ?? DBNull.Value);
        }
    }
}
