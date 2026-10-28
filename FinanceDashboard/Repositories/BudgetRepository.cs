using Dapper;
using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Data;

namespace FinanceDashboard.Repositories
{
    public class BudgetRepository : IBudgetRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;
        private readonly IMemoryCache _cache;
        private static readonly TimeSpan LookupCacheDuration = TimeSpan.FromMinutes(10);
        private static readonly TimeSpan ReportCacheDuration = TimeSpan.FromMinutes(3);

        public BudgetRepository(ApplicationDbContext context, IConfiguration configuration, IMemoryCache cache)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")!;
            _cache = cache;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        // Dashboard
        public async Task<DashboardSummaryViewModel> GetDashboardSummaryAsync(int? financialYear = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    -- Allocated Budget per sector
                    ISNULL(SUM(CASE WHEN sector = 'Non Development' THEN allocated_budget   ELSE 0 END), 0) AS NonDevelopmentBudget,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-I'   THEN allocated_budget   ELSE 0 END), 0) AS CapitalAc1Budget,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-II'  THEN allocated_budget   ELSE 0 END), 0) AS CapitalAc2Budget,
                    ISNULL(SUM(CASE WHEN sector = 'Development'      THEN allocated_budget   ELSE 0 END), 0) AS DevelopmentBudget,
                    -- Releases per sector
                    ISNULL(SUM(CASE WHEN sector = 'Non Development' THEN Releases           ELSE 0 END), 0) AS NonDevelopmentRelease,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-I'   THEN Releases           ELSE 0 END), 0) AS CapitalAc1Release,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-II'  THEN Releases           ELSE 0 END), 0) AS CapitalAc2Release,
                    ISNULL(SUM(CASE WHEN sector = 'Development'      THEN Releases           ELSE 0 END), 0) AS DevelopmentRelease,
                    -- Actual Expenditure per sector
                    ISNULL(SUM(CASE WHEN sector = 'Non Development' THEN ActualExpenditure  ELSE 0 END), 0) AS NonDevelopmentExpenditure,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-I'   THEN ActualExpenditure  ELSE 0 END), 0) AS CapitalAc1Expenditure,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-II'  THEN ActualExpenditure  ELSE 0 END), 0) AS CapitalAc2Expenditure,
                    ISNULL(SUM(CASE WHEN sector = 'Development'      THEN ActualExpenditure  ELSE 0 END), 0) AS DevelopmentExpenditure,
                    -- Grand totals from BERData columns
                    ISNULL(SUM(allocated_budget),    0) AS TotalAllocatedBudget,
                    ISNULL(SUM(FinalGrant),          0) AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),   0) AS TotalExpenditure,
                    ISNULL(SUM(FinalGrantBalance),   0) AS TotalBalance,
                    -- BER record count
                    COUNT(1) AS TotalBERRecords,
                    -- Last BER import date
                    (SELECT TOP 1 ISNULL(CompletedDate, ImportedDate)
                     FROM budget.ImportHistory WITH (NOLOCK)
                     WHERE ImportType = 'BER' AND Status = 'Completed' AND IsDeleted = 0
                     ORDER BY ISNULL(CompletedDate, ImportedDate) DESC) AS BerLastUpdated
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                OPTION (RECOMPILE);";

            var summary = await connection.QueryFirstOrDefaultAsync<DashboardSummaryViewModel>(
                sql,
                new
                {
                    FinancialYear = financialYear,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 120);

            return summary ?? new DashboardSummaryViewModel();
        }

        public async Task<List<ChartDataViewModel>> GetBudgetChartDataAsync(int? financialYear = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var charts = new List<ChartDataViewModel>();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;

            // Fund-wise Allocated Budget distribution from BERData
            var fundData = await connection.QueryAsync(
                @"SELECT TOP 10 FundCode AS Label, ISNULL(SUM(allocated_budget), 0) AS Value
                  FROM budget.BERData WITH (NOLOCK)
                  WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                    AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                  GROUP BY FundCode ORDER BY Value DESC",
                new
                {
                    FinancialYear = financialYear,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                });

            charts.Add(new ChartDataViewModel
            {
                Labels = fundData.Select(f => (string)f.Label).ToArray(),
                Data = fundData.Select(f => (decimal)f.Value).ToArray(),
                BackgroundColors = new[] { "#0d6efd", "#6610f2", "#6f42c1", "#d63384", "#dc3545", "#fd7e14", "#ffc107", "#198754", "#20c997", "#0dcaf0" }
            });

            return charts;
        }

        public async Task<List<MonthlyExpenditureViewModel>> GetMonthlyExpenditureAsync(int financialYear, string? fundCode = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    v.MonthOrder,
                    v.Month,
                    ISNULL(SUM(
                        CASE v.MonthOrder
                            WHEN 1  THEN b.JulExp
                            WHEN 2  THEN b.AugExp
                            WHEN 3  THEN b.SepExp
                            WHEN 4  THEN b.OctExp
                            WHEN 5  THEN b.NovExp
                            WHEN 6  THEN b.DecExp
                            WHEN 7  THEN b.JanExp
                            WHEN 8  THEN b.FebExp
                            WHEN 9  THEN b.MarExp
                            WHEN 10 THEN b.AprExp
                            WHEN 11 THEN b.MayExp
                            WHEN 12 THEN b.JunExp
                        END
                    ), 0) AS Amount
                FROM budget.BERData b WITH (NOLOCK)
                CROSS JOIN (VALUES
                    (1,'Jul'),(2,'Aug'),(3,'Sep'),(4,'Oct'),
                    (5,'Nov'),(6,'Dec'),(7,'Jan'),(8,'Feb'),
                    (9,'Mar'),(10,'Apr'),(11,'May'),(12,'Jun')
                ) AS v(MonthOrder, Month)
                WHERE b.FinancialYear = @FinancialYear
                  AND (@FundCode IS NULL OR b.FundCode = @FundCode)
                  AND (@HasRestrictedDepartments = 0 OR b.MinistryDepartment IN @RestrictedDepartments)
                GROUP BY v.MonthOrder, v.Month
                ORDER BY v.MonthOrder;";

            var result = await connection.QueryAsync<MonthlyExpenditureViewModel>(
                sql,
                new
                {
                    FinancialYear = financialYear,
                    FundCode = fundCode,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 120);
            return result.ToList();
        }

        // Original Budget
        public async Task<PagedResultViewModel<OriginalBudget>> GetOriginalBudgetAsync(FilterViewModel filters, PaginationViewModel pagination)
        {
            using var connection = CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@FinancialYear", filters.FinancialYear, DbType.Int32);
            parameters.Add("@FundCode", filters.FundCode, DbType.String);
            parameters.Add("@CostCenterCode", filters.CostCenterCode, DbType.String);
            parameters.Add("@DepartmentCode", filters.DepartmentCode, DbType.String);
            parameters.Add("@CommitmentItem", null, DbType.String);
            parameters.Add("@FunctionCode", filters.FunctionCode, DbType.String);
            parameters.Add("@Keyword", filters.Keyword, DbType.String);
            parameters.Add("@PageNumber", pagination.PageNumber, DbType.Int32);
            parameters.Add("@PageSize", pagination.PageSize, DbType.Int32);
            parameters.Add("@SortColumn", pagination.SortColumn ?? "Id", DbType.String);
            parameters.Add("@SortDirection", pagination.SortDirection ?? "ASC", DbType.String);

            var result = await connection.QueryAsync<OriginalBudget>(
                "budget.sp_GetOriginalBudgetFiltered",
                parameters,
                commandType: CommandType.StoredProcedure);

            var list = result.ToList();
            var totalRecords = list.FirstOrDefault()?.Id ?? 0;

            // Get total count separately
            var countParams = new DynamicParameters();
            countParams.Add("@FinancialYear", filters.FinancialYear, DbType.Int32);
            countParams.Add("@FundCode", filters.FundCode, DbType.String);
            countParams.Add("@CostCenterCode", filters.CostCenterCode, DbType.String);
            countParams.Add("@DepartmentCode", filters.DepartmentCode, DbType.String);
            countParams.Add("@CommitmentItem", null, DbType.String);
            countParams.Add("@FunctionCode", filters.FunctionCode, DbType.String);
            countParams.Add("@Keyword", filters.Keyword, DbType.String);
            countParams.Add("@PageNumber", 1, DbType.Int32);
            countParams.Add("@PageSize", int.MaxValue, DbType.Int32);
            countParams.Add("@SortColumn", "Id", DbType.String);
            countParams.Add("@SortDirection", "ASC", DbType.String);

            var allRecords = await connection.QueryAsync<OriginalBudget>(
                "budget.sp_GetOriginalBudgetFiltered",
                countParams,
                commandType: CommandType.StoredProcedure);

            return new PagedResultViewModel<OriginalBudget>
            {
                Data = list,
                Pagination = new PaginationViewModel
                {
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalRecords = allRecords.Count()
                },
                Filters = filters
            };
        }

        public async Task<OriginalBudget?> GetOriginalBudgetByIdAsync(long id)
        {
            return await _context.OriginalBudgets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<int> InsertOriginalBudgetAsync(List<OriginalBudget> records)
        {
            // Use SqlBulkCopy for large datasets
            using var connection = CreateConnection();
            await connection.OpenAsync();

            // TableLock takes a bulk-update table lock instead of row/page locks, which avoids
            // the per-batch lock-escalation overhead that stalls badly on memory-constrained
            // shared hosts (e.g. MonsterASP) when 6 nonclustered indexes must be maintained.
            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = "budget.OriginalBudget",
                BatchSize = 5000,
                BulkCopyTimeout = 600
            };

            var dataTable = new DataTable();
            dataTable.Columns.Add("FinancialYear", typeof(int));
            dataTable.Columns.Add("DocumentNumber", typeof(string));
            dataTable.Columns.Add("DepartmentCode", typeof(string));
            dataTable.Columns.Add("BudgetType", typeof(string));
            dataTable.Columns.Add("FundCode", typeof(string));
            dataTable.Columns.Add("FundDescription", typeof(string));
            dataTable.Columns.Add("CostCenterCode", typeof(string));
            dataTable.Columns.Add("SNECode", typeof(string));
            dataTable.Columns.Add("CommitmentItem", typeof(string));
            dataTable.Columns.Add("SubHead", typeof(string));
            dataTable.Columns.Add("BPS", typeof(string));
            dataTable.Columns.Add("DesignationCode", typeof(string));
            dataTable.Columns.Add("Posts", typeof(int));
            dataTable.Columns.Add("OriginalBudgetAmount", typeof(decimal));
            dataTable.Columns.Add("RevisedBudgetAmount", typeof(decimal));
            dataTable.Columns.Add("DesignationDescription", typeof(string));
            dataTable.Columns.Add("Description", typeof(string));
            dataTable.Columns.Add("PostingDate", typeof(DateTime));
            dataTable.Columns.Add("UserName", typeof(string));
            dataTable.Columns.Add("FunctionCode", typeof(string));
            dataTable.Columns.Add("Type", typeof(string));
            dataTable.Columns.Add("ImportId", typeof(long));
            dataTable.Columns.Add("IsDeleted", typeof(bool));
            dataTable.Columns.Add("CreatedDate", typeof(DateTime));

            foreach (var record in records)
            {
                dataTable.Rows.Add(
                    record.FinancialYear,
                    record.DocumentNumber,
                    record.DepartmentCode,
                    record.BudgetType,
                    record.FundCode,
                    record.FundDescription,
                    record.CostCenterCode,
                    record.SNECode,
                    record.CommitmentItem,
                    record.SubHead,
                    record.BPS,
                    record.DesignationCode,
                    record.Posts,
                    record.OriginalBudgetAmount,
                    record.RevisedBudgetAmount,
                    record.DesignationDescription,
                    record.Description,
                    record.PostingDate,
                    record.UserName,
                    record.FunctionCode,
                    record.Type,
                    record.ImportId,
                    false,
                    DateTime.UtcNow
                );
            }

            // Map DataTable columns to SQL table columns by name to avoid ordinal shift from identity Id
            foreach (DataColumn col in dataTable.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            await bulkCopy.WriteToServerAsync(dataTable);
            return records.Count;
        }

        public async Task<int> DeleteOriginalBudgetByImportIdAsync(long importId)
        {
            using var connection = CreateConnection();
            return await connection.ExecuteAsync(
                "UPDATE budget.OriginalBudget SET IsDeleted = 1 WHERE ImportId = @ImportId",
                new { ImportId = importId });
        }

        // BER Data
        public async Task<PagedResultViewModel<BERData>> GetBERDataAsync(FilterViewModel filters, PaginationViewModel pagination)
        {
            using var connection = CreateConnection();

            var sortColumn = (pagination.SortColumn ?? "Id").ToLower() switch
            {
                "id" => "Id",
                "financialyear" => "FinancialYear",
                "fundcode" => "FundCode",
                "fundcentercode" => "FundCenterCode",
                "ministrydepartment" => "MinistryDepartment",
                "detailobject" => "DetailObject",
                "budgetgrant" => "BudgetGrant",
                "finalgrant" => "FinalGrant",
                "releases" => "Releases",
                "actualexpenditure" => "ActualExpenditure",
                "finalgrantbalance" => "FinalGrantBalance",
                _ => "Id"
            };

            var sortDirection = string.Equals(pagination.SortDirection, "desc", StringComparison.OrdinalIgnoreCase)
                ? "DESC"
                : "ASC";

            var parameters = new DynamicParameters();
            parameters.Add("@FinancialYear", filters.FinancialYear, DbType.Int32);
            parameters.Add("@FundCode", filters.FundCode, DbType.String);
            parameters.Add("@FundCenterCode", filters.CostCenterCode, DbType.String);
            parameters.Add("@MinistryDepartment", filters.DepartmentCode, DbType.String);
            parameters.Add("@DetailObject", filters.ObjectCode, DbType.String);
            parameters.Add("@Keyword", filters.Keyword, DbType.String);
            parameters.Add("@Offset", (pagination.PageNumber - 1) * pagination.PageSize, DbType.Int32);
            parameters.Add("@PageSize", pagination.PageSize, DbType.Int32);

            var whereClause = @"
                FROM budget.BERData
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@FundCode IS NULL OR FundCode = @FundCode)
                  AND (@FundCenterCode IS NULL OR FundCenterCode = @FundCenterCode)
                  AND (@MinistryDepartment IS NULL OR MinistryDepartment = @MinistryDepartment)
                  AND (@DetailObject IS NULL OR DetailObject = @DetailObject)
                  AND (
                        @Keyword IS NULL
                        OR FundCode LIKE '%' + @Keyword + '%'
                        OR FundCenterCode LIKE '%' + @Keyword + '%'
                        OR MinistryDepartment LIKE '%' + @Keyword + '%'
                        OR DetailObject LIKE '%' + @Keyword + '%'
                     )";

            var sql = $@"
                SELECT *
                {whereClause}
                ORDER BY {sortColumn} {sortDirection}
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;

                SELECT COUNT(1)
                {whereClause};";

            using var multi = await connection.QueryMultipleAsync(sql, parameters);
            var list = (await multi.ReadAsync<BERData>()).ToList();
            var totalRecords = await multi.ReadFirstAsync<int>();

            return new PagedResultViewModel<BERData>
            {
                Data = list,
                Pagination = new PaginationViewModel
                {
                    PageNumber = pagination.PageNumber,
                    PageSize = pagination.PageSize,
                    TotalRecords = totalRecords
                },
                Filters = filters
            };
        }

        public async Task<BERData?> GetBERDataByIdAsync(long id)
        {
            return await _context.BERData
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task TruncateBerSapDataAsync()
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "TRUNCATE TABLE budget.BerSapData;",
                commandTimeout: 120);
        }

        public async Task<int> InsertBERDataAsync(List<BERData> records)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = "budget.BerSapData",
                BatchSize = 10000,
                BulkCopyTimeout = 600
            };

            var dataTable = new DataTable();
            dataTable.Columns.Add("FinancialYear", typeof(int));
            // Column mappings ensure name-based mapping even when the table has an identity Id column
            dataTable.Columns.Add("FundCode", typeof(string));
            dataTable.Columns.Add("FundCenterCode", typeof(string));
            dataTable.Columns.Add("FundCenterDescription", typeof(string));
            dataTable.Columns.Add("MinistryDepartment", typeof(string));
            dataTable.Columns.Add("AttachedDepartment", typeof(string));
            dataTable.Columns.Add("ProfitCenter", typeof(string));
            dataTable.Columns.Add("SubDetailFunction", typeof(string));
            dataTable.Columns.Add("DetailObject", typeof(string));
            dataTable.Columns.Add("BudgetGrant", typeof(decimal));
            dataTable.Columns.Add("SupplementaryGrant", typeof(decimal));
            dataTable.Columns.Add("ReappropriationPlus", typeof(decimal));
            dataTable.Columns.Add("ReappropriationMinus", typeof(decimal));
            dataTable.Columns.Add("TSSSurrender", typeof(decimal));
            dataTable.Columns.Add("FinalGrant", typeof(decimal));
            dataTable.Columns.Add("Releases", typeof(decimal));
            dataTable.Columns.Add("JulExp", typeof(decimal));
            dataTable.Columns.Add("AugExp", typeof(decimal));
            dataTable.Columns.Add("SepExp", typeof(decimal));
            dataTable.Columns.Add("OctExp", typeof(decimal));
            dataTable.Columns.Add("NovExp", typeof(decimal));
            dataTable.Columns.Add("DecExp", typeof(decimal));
            dataTable.Columns.Add("JanExp", typeof(decimal));
            dataTable.Columns.Add("FebExp", typeof(decimal));
            dataTable.Columns.Add("MarExp", typeof(decimal));
            dataTable.Columns.Add("AprExp", typeof(decimal));
            dataTable.Columns.Add("MayExp", typeof(decimal));
            dataTable.Columns.Add("JunExp", typeof(decimal));
            dataTable.Columns.Add("ActualExpenditure", typeof(decimal));
            dataTable.Columns.Add("FinalGrantBalance", typeof(decimal));
            dataTable.Columns.Add("ImportId", typeof(long));

            foreach (var record in records)
            {
                dataTable.Rows.Add(
                    record.FinancialYear, record.FundCode, record.FundCenterCode,
                    record.FundCenterDescription, record.MinistryDepartment, record.AttachedDepartment,
                    record.ProfitCenter, record.SubDetailFunction, record.DetailObject,
                    record.BudgetGrant, record.SupplementaryGrant, record.ReappropriationPlus,
                    record.ReappropriationMinus, record.TSSSurrender, record.FinalGrant,
                    record.Releases, record.JulExp, record.AugExp, record.SepExp,
                    record.OctExp, record.NovExp, record.DecExp, record.JanExp,
                    record.FebExp, record.MarExp, record.AprExp, record.MayExp,
                    record.JunExp, record.ActualExpenditure, record.FinalGrantBalance,
                    record.ImportId
                );
            }

            // Map DataTable columns to SQL table columns by name to avoid ordinal shift from identity Id
            foreach (DataColumn col in dataTable.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            await bulkCopy.WriteToServerAsync(dataTable);
            return records.Count;
        }

        public async Task TruncatePnDFileAsync()
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "TRUNCATE TABLE budget.pndFile;",
                commandTimeout: 120);
        }

        public async Task<int> InsertPnDFileAsync(List<PnDFile> records)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = "budget.pndFile",
                BatchSize = 5000,
                BulkCopyTimeout = 600
            };

            var dataTable = new DataTable();
            dataTable.Columns.Add("SRNo", typeof(string));
            dataTable.Columns.Add("GSNo", typeof(int));
            dataTable.Columns.Add("SchemeNo", typeof(string));
            dataTable.Columns.Add("FullSchemeNo", typeof(string));
            dataTable.Columns.Add("LoNumberCapital", typeof(string));
            dataTable.Columns.Add("LoNumberRevenue", typeof(string));
            dataTable.Columns.Add("Name", typeof(string));
            dataTable.Columns.Add("District", typeof(string));
            dataTable.Columns.Add("DistrictName", typeof(string));
            dataTable.Columns.Add("Scope", typeof(string));
            dataTable.Columns.Add("Objectives", typeof(string));
            dataTable.Columns.Add("IssueRemarks", typeof(string));
            dataTable.Columns.Add("GestationPeriod", typeof(string));
            dataTable.Columns.Add("ApprovalStatus", typeof(string));
            dataTable.Columns.Add("InitialCost", typeof(decimal));
            dataTable.Columns.Add("Cost", typeof(decimal));
            dataTable.Columns.Add("ExpenseUptoJune", typeof(decimal));
            dataTable.Columns.Add("ActualExpenditureUptoJune2026", typeof(decimal));
            dataTable.Columns.Add("LocalSouthPunjab", typeof(decimal));
            dataTable.Columns.Add("ForeignSouthPunjab", typeof(decimal));
            dataTable.Columns.Add("AllocationSouthPunjab", typeof(decimal));
            dataTable.Columns.Add("LocalAllocation", typeof(decimal));
            dataTable.Columns.Add("ForeignAllocation", typeof(decimal));
            dataTable.Columns.Add("Allocation", typeof(decimal));
            dataTable.Columns.Add("LocalRevisedAllocation", typeof(decimal));
            dataTable.Columns.Add("ForeignRevisedAllocation", typeof(decimal));
            dataTable.Columns.Add("RevisedAllocation", typeof(decimal));
            dataTable.Columns.Add("LocalPDDRelease", typeof(decimal));
            dataTable.Columns.Add("ForeignPDDRelease", typeof(decimal));
            dataTable.Columns.Add("ReleaseByPDD", typeof(decimal));
            dataTable.Columns.Add("ReleaseReceivedByAD", typeof(decimal));
            dataTable.Columns.Add("FDRelease", typeof(decimal));
            dataTable.Columns.Add("SpendingRelease", typeof(decimal));
            dataTable.Columns.Add("UtilizationPifra", typeof(decimal));
            dataTable.Columns.Add("TotalThrowForward", typeof(decimal));
            dataTable.Columns.Add("Utilization", typeof(decimal));
            dataTable.Columns.Add("PctUtilCont", typeof(decimal));
            dataTable.Columns.Add("PctUtilRevAlloc", typeof(decimal));
            dataTable.Columns.Add("SectorID", typeof(int));
            dataTable.Columns.Add("SectorOrder", typeof(int));
            dataTable.Columns.Add("SectorName", typeof(string));
            dataTable.Columns.Add("SchemeType", typeof(string));
            dataTable.Columns.Add("SchemeSubType", typeof(string));
            dataTable.Columns.Add("AcToBeCredited", typeof(string));
            dataTable.Columns.Add("ProjectStatusType", typeof(string));
            dataTable.Columns.Add("ApprovalForum", typeof(string));
            dataTable.Columns.Add("AA", typeof(string));
            dataTable.Columns.Add("TS", typeof(string));
            dataTable.Columns.Add("ExecutingAgency", typeof(string));
            dataTable.Columns.Add("SponsoringAgency", typeof(string));
            dataTable.Columns.Add("IsSNERequired", typeof(string));
            dataTable.Columns.Add("IsPCI", typeof(string));
            dataTable.Columns.Add("AttachDepartment", typeof(string));
            dataTable.Columns.Add("AdminDepartment", typeof(string));
            dataTable.Columns.Add("IsMOM", typeof(string));
            dataTable.Columns.Add("IsAA", typeof(string));
            dataTable.Columns.Add("ProjectID", typeof(int));
            dataTable.Columns.Add("CreatedAt", typeof(DateTime));

            foreach (var record in records)
            {
                var row = dataTable.NewRow();
                row["SRNo"] = (object?)record.SRNo ?? DBNull.Value;
                row["GSNo"] = (object?)record.GSNo ?? DBNull.Value;
                row["SchemeNo"] = (object?)record.SchemeNo ?? DBNull.Value;
                row["FullSchemeNo"] = (object?)record.FullSchemeNo ?? DBNull.Value;
                row["LoNumberCapital"] = (object?)record.LoNumberCapital ?? DBNull.Value;
                row["LoNumberRevenue"] = (object?)record.LoNumberRevenue ?? DBNull.Value;
                row["Name"] = (object?)record.Name ?? DBNull.Value;
                row["District"] = (object?)record.District ?? DBNull.Value;
                row["DistrictName"] = (object?)record.DistrictName ?? DBNull.Value;
                row["Scope"] = (object?)record.Scope ?? DBNull.Value;
                row["Objectives"] = (object?)record.Objectives ?? DBNull.Value;
                row["IssueRemarks"] = (object?)record.IssueRemarks ?? DBNull.Value;
                row["GestationPeriod"] = (object?)record.GestationPeriod ?? DBNull.Value;
                row["ApprovalStatus"] = (object?)record.ApprovalStatus ?? DBNull.Value;
                row["InitialCost"] = (object?)record.InitialCost ?? DBNull.Value;
                row["Cost"] = (object?)record.Cost ?? DBNull.Value;
                row["ExpenseUptoJune"] = (object?)record.ExpenseUptoJune ?? DBNull.Value;
                row["ActualExpenditureUptoJune2026"] = (object?)record.ActualExpenditureUptoJune2026 ?? DBNull.Value;
                row["LocalSouthPunjab"] = (object?)record.LocalSouthPunjab ?? DBNull.Value;
                row["ForeignSouthPunjab"] = (object?)record.ForeignSouthPunjab ?? DBNull.Value;
                row["AllocationSouthPunjab"] = (object?)record.AllocationSouthPunjab ?? DBNull.Value;
                row["LocalAllocation"] = (object?)record.LocalAllocation ?? DBNull.Value;
                row["ForeignAllocation"] = (object?)record.ForeignAllocation ?? DBNull.Value;
                row["Allocation"] = (object?)record.Allocation ?? DBNull.Value;
                row["LocalRevisedAllocation"] = (object?)record.LocalRevisedAllocation ?? DBNull.Value;
                row["ForeignRevisedAllocation"] = (object?)record.ForeignRevisedAllocation ?? DBNull.Value;
                row["RevisedAllocation"] = (object?)record.RevisedAllocation ?? DBNull.Value;
                row["LocalPDDRelease"] = (object?)record.LocalPDDRelease ?? DBNull.Value;
                row["ForeignPDDRelease"] = (object?)record.ForeignPDDRelease ?? DBNull.Value;
                row["ReleaseByPDD"] = (object?)record.ReleaseByPDD ?? DBNull.Value;
                row["ReleaseReceivedByAD"] = (object?)record.ReleaseReceivedByAD ?? DBNull.Value;
                row["FDRelease"] = (object?)record.FDRelease ?? DBNull.Value;
                row["SpendingRelease"] = (object?)record.SpendingRelease ?? DBNull.Value;
                row["UtilizationPifra"] = (object?)record.UtilizationPifra ?? DBNull.Value;
                row["TotalThrowForward"] = (object?)record.TotalThrowForward ?? DBNull.Value;
                row["Utilization"] = (object?)record.Utilization ?? DBNull.Value;
                row["PctUtilCont"] = (object?)record.PctUtilCont ?? DBNull.Value;
                row["PctUtilRevAlloc"] = (object?)record.PctUtilRevAlloc ?? DBNull.Value;
                row["SectorID"] = (object?)record.SectorID ?? DBNull.Value;
                row["SectorOrder"] = (object?)record.SectorOrder ?? DBNull.Value;
                row["SectorName"] = (object?)record.SectorName ?? DBNull.Value;
                row["SchemeType"] = (object?)record.SchemeType ?? DBNull.Value;
                row["SchemeSubType"] = (object?)record.SchemeSubType ?? DBNull.Value;
                row["AcToBeCredited"] = (object?)record.AcToBeCredited ?? DBNull.Value;
                row["ProjectStatusType"] = (object?)record.ProjectStatusType ?? DBNull.Value;
                row["ApprovalForum"] = (object?)record.ApprovalForum ?? DBNull.Value;
                row["AA"] = (object?)record.AA ?? DBNull.Value;
                row["TS"] = (object?)record.TS ?? DBNull.Value;
                row["ExecutingAgency"] = (object?)record.ExecutingAgency ?? DBNull.Value;
                row["SponsoringAgency"] = (object?)record.SponsoringAgency ?? DBNull.Value;
                row["IsSNERequired"] = (object?)record.IsSNERequired ?? DBNull.Value;
                row["IsPCI"] = (object?)record.IsPCI ?? DBNull.Value;
                row["AttachDepartment"] = (object?)record.AttachDepartment ?? DBNull.Value;
                row["AdminDepartment"] = (object?)record.AdminDepartment ?? DBNull.Value;
                row["IsMOM"] = (object?)record.IsMOM ?? DBNull.Value;
                row["IsAA"] = (object?)record.IsAA ?? DBNull.Value;
                row["ProjectID"] = (object?)record.ProjectID ?? DBNull.Value;
                row["CreatedAt"] = record.CreatedAt;
                dataTable.Rows.Add(row);
            }

            foreach (DataColumn col in dataTable.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            await bulkCopy.WriteToServerAsync(dataTable);
            return records.Count;
        }

        public async Task<List<PnDFile>> GetAllPnDFilesAsync()
        {
            using var connection = CreateConnection();
            var rows = await connection.QueryAsync<PnDFile>(
                "SELECT GSNo, LoNumberCapital, LoNumberRevenue, SectorID, SectorName FROM budget.pndFile;",
                commandTimeout: 120);
            return rows.ToList();
        }

        public async Task<int> UpsertPdSecAsync(List<PdSec> records)
        {
            if (records.Count == 0) return 0;

            using var connection = CreateConnection();
            await connection.OpenAsync();

            // Stage the incoming rows in a temp table, then MERGE into budget.pdSec
            // keyed on Project (upsert on the basis of Project).
            await connection.ExecuteAsync(@"
                IF OBJECT_ID('tempdb..#PdSecStaging') IS NOT NULL DROP TABLE #PdSecStaging;
                CREATE TABLE #PdSecStaging
                (
                    GSNo INT NULL,
                    SectorID INT NULL,
                    SectorName NVARCHAR(100) NULL,
                    Project NVARCHAR(20) NULL,
                    sectorDesc NVARCHAR(100) NULL
                );",
                commandTimeout: 120);

            using (var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = "#PdSecStaging",
                BatchSize = 5000,
                BulkCopyTimeout = 600
            })
            {
                var dataTable = new DataTable();
                dataTable.Columns.Add("GSNo", typeof(int));
                dataTable.Columns.Add("SectorID", typeof(int));
                dataTable.Columns.Add("SectorName", typeof(string));
                dataTable.Columns.Add("Project", typeof(string));
                dataTable.Columns.Add("sectorDesc", typeof(string));

                foreach (var record in records)
                {
                    var row = dataTable.NewRow();
                    row["GSNo"] = (object?)record.GSNo ?? DBNull.Value;
                    row["SectorID"] = (object?)record.SectorID ?? DBNull.Value;
                    row["SectorName"] = (object?)record.SectorName ?? DBNull.Value;
                    row["Project"] = (object?)record.Project ?? DBNull.Value;
                    row["sectorDesc"] = (object?)record.sectorDesc ?? DBNull.Value;
                    dataTable.Rows.Add(row);
                }

                foreach (DataColumn col in dataTable.Columns)
                    bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

                await bulkCopy.WriteToServerAsync(dataTable);
            }

            var affected = await connection.ExecuteAsync(@"
                MERGE budget.pdSec AS target
                USING (
                    SELECT Project, MAX(GSNo) AS GSNo, MAX(SectorID) AS SectorID,
                           MAX(SectorName) AS SectorName, MAX(sectorDesc) AS sectorDesc
                    FROM #PdSecStaging
                    GROUP BY Project
                ) AS source
                ON target.Project COLLATE DATABASE_DEFAULT = source.Project COLLATE DATABASE_DEFAULT
                WHEN MATCHED THEN
                    UPDATE SET
                        target.GSNo = source.GSNo,
                        target.SectorID = source.SectorID,
                        target.SectorName = source.SectorName,
                        target.sectorDesc = source.sectorDesc
                WHEN NOT MATCHED BY TARGET THEN
                    INSERT (GSNo, SectorID, SectorName, Project, sectorDesc)
                    VALUES (source.GSNo, source.SectorID, source.SectorName, source.Project, source.sectorDesc);

                DROP TABLE #PdSecStaging;",
                commandTimeout: 300);

            return affected;
        }

        public async Task<int> UpdatePdSecFromPnDFileAsync()
        {
            var pndRecords = await GetAllPnDFilesAsync();

            var pdSecRows = new List<PdSec>();
            var seenProjects = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            static IEnumerable<string> SplitCodes(string? value)
            {
                if (string.IsNullOrWhiteSpace(value)) yield break;
                foreach (var part in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    var trimmed = part.Trim();
                    if (!string.IsNullOrEmpty(trimmed)) yield return trimmed;
                }
            }

            foreach (var pnd in pndRecords)
            {
                if (string.IsNullOrWhiteSpace(pnd.LoNumberCapital) && string.IsNullOrWhiteSpace(pnd.LoNumberRevenue))
                    continue;

                var sectorDesc = pnd.SectorID.HasValue && !string.IsNullOrWhiteSpace(pnd.SectorName)
                    ? $"{pnd.SectorID}.{pnd.SectorName}"
                    : null;

                var codes = SplitCodes(pnd.LoNumberCapital).Concat(SplitCodes(pnd.LoNumberRevenue));

                foreach (var project in codes)
                {
                    // Last one wins per Project code when the same project appears more than once in this batch.
                    seenProjects.Add(project);
                    pdSecRows.RemoveAll(r => string.Equals(r.Project, project, StringComparison.OrdinalIgnoreCase));
                    pdSecRows.Add(new PdSec
                    {
                        GSNo = pnd.GSNo,
                        SectorID = pnd.SectorID,
                        SectorName = pnd.SectorName,
                        Project = project,
                        sectorDesc = sectorDesc
                    });
                }
            }

            return await UpsertPdSecAsync(pdSecRows);
        }

        public async Task DeleteYpProjectByYearAsync(int fiscalYear)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "DELETE FROM budget.ypProject WHERE FiscalYear = @FiscalYear;",
                new { FiscalYear = fiscalYear },
                commandTimeout: 120);
        }

        public async Task<int> InsertYpProjectAsync(List<YpProject> records)
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();

            using var bulkCopy = new SqlBulkCopy(connection, SqlBulkCopyOptions.TableLock, null)
            {
                DestinationTableName = "budget.ypProject",
                BatchSize = 10000,
                BulkCopyTimeout = 600
            };

            var dataTable = new DataTable();
            dataTable.Columns.Add("FiscalYear", typeof(int));
            dataTable.Columns.Add("ParentProject", typeof(string));
            dataTable.Columns.Add("ProjectID", typeof(string));
            dataTable.Columns.Add("WBSElement", typeof(string));
            dataTable.Columns.Add("ProfitCenter", typeof(string));
            dataTable.Columns.Add("CostCenter", typeof(string));
            dataTable.Columns.Add("MinistryDept", typeof(string));
            dataTable.Columns.Add("AttachedDept", typeof(string));
            dataTable.Columns.Add("DetailFunctionCode", typeof(int));
            dataTable.Columns.Add("OriginalBudget", typeof(decimal));
            dataTable.Columns.Add("SupplementaryBudget", typeof(decimal));
            dataTable.Columns.Add("Surrender", typeof(decimal));
            dataTable.Columns.Add("ReappPlus", typeof(decimal));
            dataTable.Columns.Add("ReappMinus", typeof(decimal));
            dataTable.Columns.Add("FinalBudget", typeof(decimal));
            dataTable.Columns.Add("ProgReleases", typeof(decimal));
            dataTable.Columns.Add("InterimDateExp", typeof(decimal));
            dataTable.Columns.Add("ProgressiveExp", typeof(decimal));
            dataTable.Columns.Add("Balance", typeof(decimal));
            dataTable.Columns.Add("SectorID", typeof(string));
            dataTable.Columns.Add("SubSect", typeof(string));
            dataTable.Columns.Add("Fund", typeof(string));
            dataTable.Columns.Add("JulyExp", typeof(decimal));
            dataTable.Columns.Add("AugustExp", typeof(decimal));
            dataTable.Columns.Add("SeptemberExp", typeof(decimal));
            dataTable.Columns.Add("OctoberExp", typeof(decimal));
            dataTable.Columns.Add("NovemberExp", typeof(decimal));
            dataTable.Columns.Add("DecemberExp", typeof(decimal));
            dataTable.Columns.Add("JanuaryExp", typeof(decimal));
            dataTable.Columns.Add("FebruaryExp", typeof(decimal));
            dataTable.Columns.Add("MarchExp", typeof(decimal));
            dataTable.Columns.Add("AprilExp", typeof(decimal));
            dataTable.Columns.Add("MayExp", typeof(decimal));
            dataTable.Columns.Add("JuneExp", typeof(decimal));
            dataTable.Columns.Add("ProjectDescription", typeof(string));

            foreach (var record in records)
            {
                dataTable.Rows.Add(
                    record.FiscalYear, record.ParentProject, record.ProjectID, record.WBSElement,
                    record.ProfitCenter, record.CostCenter, record.MinistryDept, record.AttachedDept,
                    record.DetailFunctionCode, record.OriginalBudget, record.SupplementaryBudget,
                    record.Surrender, record.ReappPlus, record.ReappMinus, record.FinalBudget,
                    record.ProgReleases, record.InterimDateExp, record.ProgressiveExp, record.Balance,
                    record.SectorID, record.SubSect, record.Fund,
                    record.JulyExp, record.AugustExp, record.SeptemberExp, record.OctoberExp,
                    record.NovemberExp, record.DecemberExp, record.JanuaryExp, record.FebruaryExp,
                    record.MarchExp, record.AprilExp, record.MayExp, record.JuneExp,
                    record.ProjectDescription
                );
            }

            foreach (DataColumn col in dataTable.Columns)
                bulkCopy.ColumnMappings.Add(col.ColumnName, col.ColumnName);

            await bulkCopy.WriteToServerAsync(dataTable);
            return records.Count;
        }

        public async Task DeleteDevExpByYearAsync(int fiscalYear)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "DELETE FROM budget.devExp WHERE FiscalYear = @FiscalYear;",
                new { FiscalYear = fiscalYear },
                commandTimeout: 120);
        }

        public async Task<int> MergeYpProjectIntoDevExpAsync(int fiscalYear)
        {
            using var connection = CreateConnection();
            var sql = @"
                INSERT INTO budget.devExp
                (
                    FiscalYear,
                    ParentProject,
                    ProjectID,
                    WBSElement,
                    ProfitCenter,
                    CostCenter,
                    MinistryDept,
                    AttachedDept,
                    DetailFunctionCode,
                    SectorID,
                    Fund,
                    ProjectDescription,
                    OriginalBudget,
                    SupplementaryBudget,
                    Surrender,
                    ReappPlus,
                    ReappMinus,
                    FinalBudget,
                    ProgReleases,
                    InterimDateExp,
                    ProgressiveExp,
                    Balance,
                    SubSect,
                    JulyExp,
                    AugustExp,
                    SeptemberExp,
                    OctoberExp,
                    NovemberExp,
                    DecemberExp,
                    JanuaryExp,
                    FebruaryExp,
                    MarchExp,
                    AprilExp,
                    MayExp,
                    JuneExp,
                    Allocation
                )
                SELECT
                    COALESCE(y.FiscalYear, d.FiscalYear),
                    COALESCE(y.ParentProject, d.ParentProject),
                    COALESCE(y.ProjectID, d.ProjectID),
                    COALESCE(y.WBSElement, d.WBSElement),
                    y.ProfitCenter,
                    COALESCE(y.CostCenter, d.CostCenter),
                    y.MinistryDept,
                    COALESCE(y.AttachedDept, d.AttachedDeptDesc),
                    COALESCE(y.DetailFunctionCode, d.DetailFunction),
                    COALESCE(y.SectorID, d.SectorID),
                    COALESCE(y.Fund, d.Fund),
                    y.ProjectDescription,
                    y.OriginalBudget,
                    y.SupplementaryBudget,
                    y.Surrender,
                    y.ReappPlus,
                    y.ReappMinus,
                    y.FinalBudget,
                    y.ProgReleases,
                    y.InterimDateExp,
                    y.ProgressiveExp,
                    y.Balance,
                    COALESCE(y.SubSect, d.SubSect),
                    y.JulyExp,
                    y.AugustExp,
                    y.SeptemberExp,
                    y.OctoberExp,
                    y.NovemberExp,
                    y.DecemberExp,
                    y.JanuaryExp,
                    y.FebruaryExp,
                    y.MarchExp,
                    y.AprilExp,
                    y.MayExp,
                    y.JuneExp,
                    d.OriginalBudget
                FROM budget.ypProject y
                FULL OUTER JOIN budget.devOb d
                    ON LTRIM(RTRIM(y.ProjectID))   = LTRIM(RTRIM(d.ProjectID))
                   AND LTRIM(RTRIM(y.WBSElement)) = LTRIM(RTRIM(d.WBSElement))
                WHERE COALESCE(y.FiscalYear, d.FiscalYear) = @FiscalYear;";

            return await connection.ExecuteAsync(sql, new { FiscalYear = fiscalYear }, commandTimeout: 300);
        }

        public async Task<int> UpdateDevExpGlCodeAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE budget.devExp
                SET glcode = SUBSTRING(WBSElement, CHARINDEX('-', WBSElement) + 1, 6)
                WHERE CHARINDEX('-', WBSElement) > 0;";

            return await connection.ExecuteAsync(sql, commandTimeout: 120);
        }

        public async Task<int> UpdateDevExpGlDescriptionAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE d
                SET d.gldesc = g.Gl_desc
                FROM budget.devExp d
                INNER JOIN budget.GL_Codes g
                    ON LTRIM(RTRIM(d.glcode)) = LTRIM(RTRIM(g.gl_code));";

            return await connection.ExecuteAsync(sql, commandTimeout: 120);
        }

        public async Task<int> UpdateDevExpPnDSectorAsync()
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE d
                SET d.pndSector = p.sectorDesc
                FROM budget.devExp d
                INNER JOIN budget.pdSec p
                    ON LTRIM(RTRIM(d.ParentProject)) COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(p.Project)) COLLATE DATABASE_DEFAULT;";

            return await connection.ExecuteAsync(sql, commandTimeout: 120);
        }

        public async Task<List<ImportHistory>> GetImportHistoryAsync(string? importType = null, int pageNumber = 1, int pageSize = 20)
        {
            using var connection = CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@PageNumber", pageNumber, DbType.Int32);
            parameters.Add("@PageSize", pageSize, DbType.Int32);
            parameters.Add("@ImportType", importType, DbType.String);

            var result = await connection.QueryAsync<ImportHistory>(
                "budget.sp_GetImportHistory",
                parameters,
                commandType: CommandType.StoredProcedure);

            return result.ToList();
        }

        public async Task<ImportHistory?> GetImportHistoryByIdAsync(long id)
        {
            return await _context.ImportHistories
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id);
        }

        public async Task<long> AddImportHistoryAsync(ImportHistory importHistory)
        {
            _context.ImportHistories.Add(importHistory);
            await _context.SaveChangesAsync();
            return importHistory.Id;
        }

        public async Task UpdateImportHistoryAsync(ImportHistory importHistory)
        {
            _context.ImportHistories.Update(importHistory);
            await _context.SaveChangesAsync();
        }

        /// <summary>
        /// Cheap progress heartbeat written after each insert batch during a large BER import.
        /// Uses a raw connection (not the EF context) so it never conflicts with EF change
        /// tracking on the long-lived background scope, and lets the poller show real progress
        /// (and prove the background job is still alive) instead of appearing frozen at 50%.
        /// </summary>
        public async Task UpdateImportHeartbeatAsync(long importId, int recordsSoFar)
        {
            using var connection = CreateConnection();
            await connection.ExecuteAsync(
                "UPDATE budget.ImportHistory SET RecordsImported = @RecordsSoFar WHERE Id = @ImportId",
                new { RecordsSoFar = recordsSoFar, ImportId = importId });
        }

        public async Task<bool> DeleteImportAsync(long importId, string deletedBy)
        {
            using var connection = CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@ImportId", importId, DbType.Int64);
            parameters.Add("@DeletedBy", deletedBy, DbType.String);

            await connection.ExecuteAsync(
                "budget.sp_DeleteImport",
                parameters,
                commandType: CommandType.StoredProcedure);

            return true;
        }

        // Lookups
        public async Task<List<LookupItem>> GetLookupValuesAsync(string type, string? searchTerm = null)
        {
            using var connection = CreateConnection();
            var parameters = new DynamicParameters();
            parameters.Add("@Type", type, DbType.String);
            parameters.Add("@SearchTerm", searchTerm, DbType.String);

            var result = await connection.QueryAsync<LookupItem>(
                "budget.sp_GetLookupValues",
                parameters,
                commandType: CommandType.StoredProcedure);

            return result.ToList();
        }

        public async Task<List<LookupItem>> GetFundLookupAsync(int? financialYear = null, string? departmentCode = null, string? sector = null, List<string>? restrictedDepartments = null)
        {
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var cacheKey = $"Lookup_Fund_{financialYear}_{departmentCode}_{sector}_{(hasRestrictedDepartments ? string.Join(",", restrictedDepartments!) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<LookupItem>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT
                    FundCode            AS Value,
                    FundCode + CASE WHEN MAX(ISNULL(Fund_Des,'')) <> ''
                               THEN ' - ' + MAX(Fund_Des) ELSE '' END AS Text
                FROM budget.BERData WITH (NOLOCK)
                WHERE FundCode IS NOT NULL AND FundCode <> ''
                  AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@Sector IS NULL OR Sector = @Sector)
                  AND (@DepartmentCode IS NULL OR MinistryDepartment = @DepartmentCode)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY FundCode
                ORDER BY FundCode;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new
                {
                    FinancialYear = financialYear,
                    DepartmentCode = departmentCode,
                    Sector = sector,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                });
            var list = result.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        public async Task<List<int>> GetFinancialYearsAsync()
        {
            const string cacheKey = "Lookup_FinancialYears";
            if (_cache.TryGetValue(cacheKey, out List<int>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();
            var years = await connection.QueryAsync<int>(
                @"SELECT DISTINCT FinancialYear FROM budget.OriginalBudget WHERE IsDeleted = 0
                  UNION
                  SELECT DISTINCT FinancialYear FROM budget.BERData
                  ORDER BY FinancialYear DESC");
            var list = years.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        // Dev Budget Report
        public async Task<List<DevBudgetReportViewModel>> GetDevBudgetReportAsync(int? financialYear = null, List<string>? parentProjects = null, List<string>? gsNos = null, List<string>? sectors = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasProjects = parentProjects != null && parentProjects.Count > 0;
            var hasGsNos = gsNos != null && gsNos.Count > 0;
            var hasSectors = sectors != null && sectors.Count > 0;
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    d.FiscalYear,
                    d.ParentProject,
                    d.ProjectID,
                    MAX(d.ProjectDescription)      AS ProjectDescription,
                    MAX(d.gldesc)                        AS Gldesc,
                    MAX(CAST(p.GSNo AS NVARCHAR(20)))    AS GsNo,
                    MAX(p.SectorName)                    AS SectorName,
                    ISNULL(SUM(d.Allocation),0)          AS Allocation,
                    ISNULL(SUM(d.OriginalBudget),0)      AS OriginalBudget,
                    ISNULL(SUM(d.SupplementaryBudget),0) AS SupplementaryBudget,
                    ISNULL(SUM(d.ReappPlus),0)           AS ReappPlus,
                    ISNULL(SUM(d.ReappMinus),0)          AS ReappMinus,
                    ISNULL(SUM(d.Surrender),0)           AS Surrender,
                    ISNULL(SUM(d.FinalBudget),0)         AS FinalBudget,
                    ISNULL(SUM(d.ProgReleases),0)        AS ProgReleases,
                    ISNULL(SUM(d.InterimDateExp),0)      AS ProgressiveExp,
                    ISNULL(MAX(pf.ReleaseByPDD),0)       AS ReleaseByPDD
                FROM budget.devExp d WITH (NOLOCK)
                LEFT JOIN budget.pdSec p
                    ON LTRIM(RTRIM(d.ParentProject)) COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(p.Project)) COLLATE DATABASE_DEFAULT
                LEFT JOIN (
                    SELECT GSNo, SUM(ISNULL(ReleaseByPDD,0)) AS ReleaseByPDD
                    FROM budget.pndFile
                    GROUP BY GSNo
                ) pf
                    ON p.GSNo = pf.GSNo
                WHERE d.ParentProject LIKE 'PB%'
                  AND (@FinancialYear IS NULL OR d.FiscalYear = @FinancialYear)
                  AND (@HasProjects = 0 OR d.ParentProject IN @ParentProjects)
                  AND (@HasGsNos = 0 OR CAST(p.GSNo AS NVARCHAR(20)) IN @GsNos)
                  AND (@HasSectors = 0 OR p.SectorName IN @Sectors)
                  AND (@HasRestrictedDepartments = 0 OR d.MinistryDept IN @RestrictedDepartments)
                GROUP BY d.FiscalYear, d.ParentProject, d.ProjectID, d.gldesc
                ORDER BY d.ProjectID;";

            var result = await connection.QueryAsync<DevBudgetReportViewModel>(sql,
                new
                {
                    FinancialYear = financialYear,
                    HasProjects = hasProjects,
                    ParentProjects = hasProjects ? parentProjects : new List<string> { string.Empty },
                    HasGsNos = hasGsNos,
                    GsNos = hasGsNos ? gsNos : new List<string> { string.Empty },
                    HasSectors = hasSectors,
                    Sectors = hasSectors ? sectors : new List<string> { string.Empty },
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<LookupItem>> GetParentProjectLookupAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT ParentProject AS Value, ParentProject AS Text
                FROM budget.devExp WITH (NOLOCK)
                WHERE ParentProject IS NOT NULL AND ParentProject <> ''
                  AND ParentProject LIKE 'PB%'
                  AND (@FinancialYear IS NULL OR FiscalYear = @FinancialYear)
                ORDER BY ParentProject;";

            var result = await connection.QueryAsync<LookupItem>(sql,
                new { FinancialYear = financialYear },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<LookupItem>> GetGsNoLookupAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT CAST(p.GSNo AS NVARCHAR(20)) AS Value, CAST(p.GSNo AS NVARCHAR(20)) AS Text
                FROM budget.devExp d WITH (NOLOCK)
                INNER JOIN budget.pdSec p
                    ON LTRIM(RTRIM(d.ParentProject)) COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(p.Project)) COLLATE DATABASE_DEFAULT
                WHERE p.GSNo IS NOT NULL
                  AND d.ParentProject LIKE 'PB%'
                  AND (@FinancialYear IS NULL OR d.FiscalYear = @FinancialYear)
                ORDER BY Value;";

            var result = await connection.QueryAsync<LookupItem>(sql,
                new { FinancialYear = financialYear },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<LookupItem>> GetDevBudgetSectorLookupAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT p.SectorName AS Value, p.SectorName AS Text
                FROM budget.devExp d WITH (NOLOCK)
                INNER JOIN budget.pdSec p
                    ON LTRIM(RTRIM(d.ParentProject)) COLLATE DATABASE_DEFAULT = LTRIM(RTRIM(p.Project)) COLLATE DATABASE_DEFAULT
                WHERE p.SectorName IS NOT NULL AND p.SectorName <> ''
                  AND d.ParentProject LIKE 'PB%'
                  AND (@FinancialYear IS NULL OR d.FiscalYear = @FinancialYear)
                ORDER BY Value;";

            var result = await connection.QueryAsync<LookupItem>(sql,
                new { FinancialYear = financialYear },
                commandTimeout: 60);
            return result.ToList();
        }

        // Reports
        public async Task<List<FundWiseReportViewModel>> GetFundWiseReportAsync(int? financialYear = null, string? fundCode = null, string? keyword = null, string? sector = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    FundCode,
                    MAX(ISNULL(Fund_Des, ''))            AS FundDes,
                    ISNULL(SUM(allocated_budget),0)      AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)           AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)              AS TotalReleases,
                    ISNULL(SUM(SupplementaryGrant),0)    AS TotalSupplementaryGrant,
                    ISNULL(SUM(ReappropriationPlus),0)   AS TotalReappropriationPlus,
                    ISNULL(SUM(ReappropriationMinus),0)  AS TotalReappropriationMinus,
                    ISNULL(SUM(TSSSurrender),0)          AS TotalSurrender,
                    ISNULL(SUM(FinalGrant),0)            AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)     AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)     AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@Sector   IS NULL OR Sector   = @Sector)
                  AND (@FundCode IS NULL OR FundCode = @FundCode)
                  AND (@Keyword   IS NULL OR FundCode LIKE '%' + @Keyword + '%'
                                         OR Fund_Des LIKE '%' + @Keyword + '%')
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY FundCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<FundWiseReportViewModel>(sql,
                new
                {
                    FinancialYear = financialYear,
                    Sector = sector,
                    FundCode = fundCode,
                    Keyword = keyword,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<DepartmentWiseReportViewModel>> GetDepartmentWiseReportAsync(
            int? financialYear = null, string? sector = null,
            string? departmentCode = null, string? fundCode = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    ISNULL(DepartmentName, MinistryDepartment) AS DepartmentName,
                    ISNULL(SUM(allocated_budget),0)            AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)                 AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)                    AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)                  AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)           AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)           AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear    IS NULL OR FinancialYear       = @FinancialYear)
                  AND (@Sector          IS NULL OR Sector               = @Sector)
                  AND (@DepartmentCode  IS NULL OR MinistryDepartment   = @DepartmentCode)
                  AND (@FundCode        IS NULL OR FundCode             = @FundCode)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY ISNULL(DepartmentName, MinistryDepartment)
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<DepartmentWiseReportViewModel>(sql,
                new
                {
                    FinancialYear = financialYear,
                    Sector = sector,
                    DepartmentCode = departmentCode,
                    FundCode = fundCode,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 60);
            return result.ToList();
        }

        // Drill-down: Funds for a specific department
        public async Task<List<FundWiseReportViewModel>> GetDrillFundsAsync(
            string departmentName, int? financialYear = null, string? sector = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    FundCode,
                    MAX(ISNULL(Fund_Des, ''))         AS FundDes,
                    MAX(ISNULL(Sector, ''))           AS Sector,
                    ISNULL(SUM(allocated_budget),0)   AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)        AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)           AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)         AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)  AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)  AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE ISNULL(DepartmentName, MinistryDepartment) = @DepartmentName
                  AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@Sector IS NULL OR Sector = @Sector)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY FundCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<FundWiseReportViewModel>(sql,
                new
                {
                    DepartmentName = departmentName,
                    FinancialYear = financialYear,
                    Sector = sector,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CostCenterWiseReportViewModel>> GetDrillCostCentersAsync(
            string departmentName, string fundCode, int? financialYear = null, string? sector = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    b.FundCenterCode,
                    MAX(ISNULL(b.FundCenterDescription,'')) AS CostCenterDescription,
                    MAX(ISNULL(b.Fund_Des,''))              AS FundDes,
                    ISNULL(SUM(b.allocated_budget),0)       AS TotalAllocatedBudget,
                    ISNULL(SUM(b.BudgetGrant),0)            AS TotalBudgetGrant,
                    ISNULL(SUM(b.Releases),0)               AS TotalReleases,
                    ISNULL(SUM(b.FinalGrant),0)             AS TotalFinalGrant,
                    ISNULL(SUM(b.ActualExpenditure),0)      AS TotalActualExpenditure,
                    ISNULL(SUM(b.FinalGrantBalance),0)      AS TotalBalance
                FROM budget.BERData b WITH (NOLOCK)
                WHERE ISNULL(b.DepartmentName, b.MinistryDepartment) = @DepartmentName
                  AND b.FundCode = @FundCode
                  AND (@FinancialYear IS NULL OR b.FinancialYear = @FinancialYear)
                  AND (@Sector IS NULL OR b.Sector = @Sector)
                  AND (@HasRestrictedDepartments = 0 OR b.MinistryDepartment IN @RestrictedDepartments)
                GROUP BY b.FundCenterCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<CostCenterWiseReportViewModel>(sql,
                new
                {
                    DepartmentName = departmentName,
                    FundCode = fundCode,
                    FinancialYear = financialYear,
                    Sector = sector,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CostCenterWiseReportViewModel>> GetCostCenterWiseReportAsync(
            int? financialYear = null, string? fundCode = null, string? costCenterCode = null,
            IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null,
            string? sector = null, string? departmentCode = null, List<string>? restrictedDepartments = null)
        {
            // Build dynamic WHERE clauses for multi-value IN filters
            var ccList  = costCenterCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var objList = objectCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;

            var cacheKey = $"Report_CostCenterWise_{financialYear}_{fundCode}_{costCenterCode}_{sector}_{departmentCode}_{(hasRestrictedDepartments ? string.Join(",", restrictedDepartments!) : "")}" +
                $"_{(ccList != null && ccList.Count > 0 ? string.Join(",", ccList) : "")}" +
                $"_{(objList != null && objList.Count > 0 ? string.Join(",", objList) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<CostCenterWiseReportViewModel>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    FundCenterCode,
                    MAX(ISNULL(FundCenterDescription,'')) AS CostCenterDescription,
                    MAX(ISNULL(Fund_Des,''))              AS FundDes,
                    ISNULL(SUM(allocated_budget),0)       AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)            AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)               AS TotalReleases,
                    ISNULL(SUM(SupplementaryGrant),0)     AS TotalSupplementaryGrant,
                    ISNULL(SUM(ReappropriationPlus),0)    AS TotalReappropriationPlus,
                    ISNULL(SUM(ReappropriationMinus),0)   AS TotalReappropriationMinus,
                    ISNULL(SUM(TSSSurrender),0)           AS TotalSurrender,
                    ISNULL(SUM(FinalGrant),0)             AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)      AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)      AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@FundCode      IS NULL OR FundCode      = @FundCode)
                  AND (@Sector         IS NULL OR Sector             = @Sector)
                  AND (@DepartmentCode IS NULL OR MinistryDepartment = @DepartmentCode)
                  AND (@CostCenterCode IS NULL OR FundCenterCode = @CostCenterCode)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)"
                + (ccList  != null && ccList.Count  > 0 ? " AND FundCenterCode IN @CcList"    : "")
                + (objList != null && objList.Count > 0 ? " AND DetailObject   IN @ObjList"   : "") + @"
                GROUP BY FundCenterCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<CostCenterWiseReportViewModel>(sql,
                new
                {
                    FinancialYear = financialYear,
                    FundCode = fundCode,
                    CostCenterCode = costCenterCode,
                    Sector = sector,
                    DepartmentCode = departmentCode,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty },
                    CcList = ccList ?? new List<string>(),
                    ObjList = objList ?? new List<string>()
                },
                commandTimeout: 60);
            var list = result.ToList();
            _cache.Set(cacheKey, list, ReportCacheDuration);
            return list;
        }

        public async Task<List<CostCenterGLDetailViewModel>> GetCostCenterGLDetailAsync(
            string costCenterCode, int? financialYear = null, string? fundCode = null,
            IEnumerable<string>? objectCodes = null)
        {
            var objList = objectCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var cacheKey = $"Report_CostCenterGLDetail_{costCenterCode}_{financialYear}_{fundCode}" +
                $"_{(objList != null && objList.Count > 0 ? string.Join(",", objList) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<CostCenterGLDetailViewModel>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    b.DetailObject                           AS GLCode,
                    MAX(ISNULL(b.GL_Desc, b.DetailObject))   AS GLDescription,
                    ISNULL(SUM(b.allocated_budget), 0)       AS TotalAllocatedBudget,
                    ISNULL(SUM(b.BudgetGrant),      0)       AS TotalBudgetGrant,
                    ISNULL(SUM(b.Releases),         0)       AS TotalReleases,
                    ISNULL(SUM(b.SupplementaryGrant),0)      AS TotalSupplementaryGrant,
                    ISNULL(SUM(b.ReappropriationPlus),0)     AS TotalReappropriationPlus,
                    ISNULL(SUM(b.ReappropriationMinus),0)    AS TotalReappropriationMinus,
                    ISNULL(SUM(b.TSSSurrender),0)            AS TotalSurrender,
                    ISNULL(SUM(b.FinalGrant),       0)       AS TotalFinalGrant,
                    ISNULL(SUM(b.ActualExpenditure),0)       AS TotalActualExpenditure,
                    ISNULL(SUM(b.FinalGrantBalance),0)       AS TotalBalance
                FROM budget.BERData b WITH (NOLOCK)
                WHERE b.FundCenterCode = @CostCenterCode
                  AND (@FinancialYear IS NULL OR b.FinancialYear = @FinancialYear)
                  AND (@FundCode      IS NULL OR b.FundCode      = @FundCode)"
                + (objList != null && objList.Count > 0 ? " AND b.DetailObject IN @ObjList" : "") + @"
                GROUP BY b.DetailObject
                ORDER BY b.DetailObject;";

            var result = await connection.QueryAsync<CostCenterGLDetailViewModel>(sql,
                new { CostCenterCode = costCenterCode, FinancialYear = financialYear, FundCode = fundCode,
                      ObjList = objList ?? new List<string>() },
                commandTimeout: 60);
            var list = result.ToList();
            _cache.Set(cacheKey, list, ReportCacheDuration);
            return list;
        }

        public async Task<List<FunctionWiseReportViewModel>> GetFunctionWiseReportAsync(int? financialYear = null, string? keyword = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var sql = @"
                SELECT
                    SubDetailFunction                   AS FunctionCode,
                    COUNT(1)                            AS TotalRecords,
                    ISNULL(SUM(BudgetGrant),0)          AS TotalOriginalBudget,
                    ISNULL(SUM(FinalGrant),0)           AS TotalRevisedBudget,
                    COUNT(DISTINCT MinistryDepartment)  AS DepartmentCount,
                    COUNT(DISTINCT FundCode)            AS FundCount
                FROM budget.BERData
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@Keyword IS NULL OR SubDetailFunction LIKE '%' + @Keyword + '%')
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY SubDetailFunction
                ORDER BY TotalRevisedBudget DESC;";

            var result = await connection.QueryAsync<FunctionWiseReportViewModel>(sql,
                new
                {
                    FinancialYear = financialYear,
                    Keyword = keyword,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                }, commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<ObjectWiseReportViewModel>> GetObjectWiseReportAsync(int? financialYear = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, string? sector = null, IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null)
        {
            var ccList  = costCenterCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var objList = objectCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            var cacheKey = $"Report_ObjectWise_{financialYear}_{departmentCode}_{fundCode}_{costCenterCode}_{keyword}_{sector}" +
                $"_{(ccList != null && ccList.Count > 0 ? string.Join(",", ccList) : "")}" +
                $"_{(objList != null && objList.Count > 0 ? string.Join(",", objList) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<ObjectWiseReportViewModel>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    DetailObject                             AS DetailObject,
                    MAX(GL_Desc)                             AS ObjectDescription,
                    ISNULL(SUM(allocated_budget),0)          AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)               AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)                  AS TotalReleases,
                    ISNULL(SUM(SupplementaryGrant),0)        AS TotalSupplementaryGrant,
                    ISNULL(SUM(ReappropriationPlus),0)       AS TotalReappropriationPlus,
                    ISNULL(SUM(ReappropriationMinus),0)      AS TotalReappropriationMinus,
                    ISNULL(SUM(TSSSurrender),0)              AS TotalSurrender,
                    ISNULL(SUM(FinalGrant),0)                AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)         AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)         AS TotalBalance
                FROM budget.BERData
                WHERE (@FinancialYear   IS NULL OR FinancialYear      = @FinancialYear)
                  AND (@Sector          IS NULL OR Sector             = @Sector)
                  AND (@Department      IS NULL OR MinistryDepartment = @Department)
                  AND (@FundCode        IS NULL OR FundCode           = @FundCode)
                  AND (@CostCenterCode  IS NULL OR FundCenterCode     = @CostCenterCode)
                  AND (@Keyword         IS NULL OR DetailObject LIKE '%' + @Keyword + '%')"
                + (ccList  != null && ccList.Count  > 0 ? " AND FundCenterCode IN @CcList"  : "")
                + (objList != null && objList.Count > 0 ? " AND DetailObject   IN @ObjList" : "") + @"
                GROUP BY DetailObject
                ORDER BY TotalAllocatedBudget DESC;";

            var result = await connection.QueryAsync<ObjectWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, Sector = sector, Department = departmentCode, FundCode = fundCode,
                      CostCenterCode = costCenterCode, Keyword = keyword,
                      CcList = ccList ?? new List<string>(), ObjList = objList ?? new List<string>() },
                commandTimeout: 60);
            var list = result.ToList();
            _cache.Set(cacheKey, list, ReportCacheDuration);
            return list;
        }

        public async Task<List<ObjectWiseGroupedViewModel>> GetObjectWiseGroupedAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null)
        {
            var ccList  = costCenterCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var objList = objectCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();

            var cacheKey = $"Report_ObjectWiseGrouped_{financialYear}_{sector}_{departmentCode}_{fundCode}_{costCenterCode}_{keyword}" +
                $"_{(ccList != null && ccList.Count > 0 ? string.Join(",", ccList) : "")}" +
                $"_{(objList != null && objList.Count > 0 ? string.Join(",", objList) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<ObjectWiseGroupedViewModel>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    ISNULL(Sector, '')                        AS Sector,
                    ISNULL(MinistryDepartment, '')            AS Department,
                    ISNULL(FundCode, '')                      AS FundCode,
                    MAX(ISNULL(Fund_Des, ''))                 AS FundDescription,
                    ISNULL(FundCenterCode, '')                AS CostCenterCode,
                    MAX(ISNULL(FundCenterDescription, ''))    AS CostCenterDescription,
                    DetailObject                              AS DetailObject,
                    MAX(GL_Desc)                              AS ObjectDescription,
                    ISNULL(SUM(allocated_budget),0)           AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)                AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)                   AS TotalReleases,
                    ISNULL(SUM(SupplementaryGrant),0)         AS TotalSupplementaryGrant,
                    ISNULL(SUM(ReappropriationPlus),0)        AS TotalReappropriationPlus,
                    ISNULL(SUM(ReappropriationMinus),0)       AS TotalReappropriationMinus,
                    ISNULL(SUM(TSSSurrender),0)               AS TotalSurrender,
                    ISNULL(SUM(FinalGrant),0)                 AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)          AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)          AS TotalBalance
                FROM budget.BERData
                WHERE (@FinancialYear   IS NULL OR FinancialYear      = @FinancialYear)
                  AND (@Sector          IS NULL OR Sector             = @Sector)
                  AND (@Department      IS NULL OR MinistryDepartment = @Department)
                  AND (@FundCode        IS NULL OR FundCode           = @FundCode)
                  AND (@CostCenterCode  IS NULL OR FundCenterCode     = @CostCenterCode)
                  AND (@Keyword         IS NULL OR DetailObject LIKE '%' + @Keyword + '%')"
                + (ccList  != null && ccList.Count  > 0 ? " AND FundCenterCode IN @CcList"  : "")
                + (objList != null && objList.Count > 0 ? " AND DetailObject   IN @ObjList" : "") + @"
                GROUP BY Sector, MinistryDepartment, FundCode, FundCenterCode, DetailObject
                ORDER BY Sector, MinistryDepartment, FundCode, FundCenterCode, TotalAllocatedBudget DESC;";

            var result = await connection.QueryAsync<ObjectWiseGroupedViewModel>(sql,
                new { FinancialYear = financialYear, Sector = sector, Department = departmentCode, FundCode = fundCode,
                      CostCenterCode = costCenterCode, Keyword = keyword,
                      CcList = ccList ?? new List<string>(), ObjList = objList ?? new List<string>() },
                commandTimeout: 60);
            var list = result.ToList();
            _cache.Set(cacheKey, list, ReportCacheDuration);
            return list;
        }

        public async Task<List<ObjectWiseGLDetailViewModel>> GetObjectWiseGLDetailAsync(string detailObject, int? financialYear = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    FundCenterCode                           AS GLCode,
                    MAX(FundCenterDescription)               AS GLDescription,
                    ISNULL(SUM(allocated_budget),0)          AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)               AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)                  AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)                AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)         AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)         AS TotalBalance
                FROM budget.BERData
                WHERE DetailObject = @DetailObject
                  AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                GROUP BY FundCenterCode
                ORDER BY TotalAllocatedBudget DESC;";

            var result = await connection.QueryAsync<ObjectWiseGLDetailViewModel>(sql,
                new { DetailObject = detailObject, FinancialYear = financialYear }, commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CompleteBudgetReportViewModel>> GetCompleteBudgetReportAsync(FilterViewModel filters, int pageNumber = 1, int pageSize = 50)
        {
            using var connection = CreateConnection();
            var offset = (pageNumber - 1) * pageSize;
            var sql = @"
                SELECT
                    FinancialYear,
                    MinistryDepartment                  AS DepartmentCode,
                    FundCode,
                    ISNULL(Fund_Des, '')                AS FundDescription,
                    FundCenterCode                      AS CostCenterCode,
                    DetailObject                        AS CommitmentItem,
                    SubDetailFunction                   AS FunctionCode,
                    ISNULL(BudgetGrant, 0)              AS OriginalBudgetAmount,
                    ISNULL(FinalGrant, 0)               AS RevisedBudgetAmount,
                    0                                   AS Posts,
                    ISNULL(BudgetGrant, 0)              AS BudgetGrant,
                    ISNULL(FinalGrant, 0)               AS FinalGrant,
                    ISNULL(Releases, 0)                 AS Releases,
                    ISNULL(ActualExpenditure, 0)        AS ActualExpenditure,
                    ISNULL(FinalGrantBalance, 0)        AS FinalGrantBalance
                FROM budget.BERData
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@FundCode IS NULL OR FundCode = @FundCode)
                  AND (@DepartmentCode IS NULL OR MinistryDepartment = @DepartmentCode)
                  AND (@CostCenterCode IS NULL OR FundCenterCode = @CostCenterCode)
                  AND (@Keyword IS NULL
                       OR FundCode LIKE '%' + @Keyword + '%'
                       OR FundCenterCode LIKE '%' + @Keyword + '%'
                       OR DetailObject LIKE '%' + @Keyword + '%'
                       OR MinistryDepartment LIKE '%' + @Keyword + '%')
                ORDER BY FinancialYear DESC, FundCode, FundCenterCode
                OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;";

            var result = await connection.QueryAsync<CompleteBudgetReportViewModel>(sql,
                new
                {
                    FinancialYear = filters.FinancialYear,
                    FundCode = filters.FundCode,
                    DepartmentCode = filters.DepartmentCode,
                    CostCenterCode = filters.CostCenterCode,
                    Keyword = filters.Keyword,
                    Offset = offset,
                    PageSize = pageSize
                }, commandTimeout: 60);

            return result.ToList();
        }

        public async Task<List<BudgetComparisonViewModel>> GetBudgetComparisonAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var result = await connection.QueryAsync<BudgetComparisonViewModel>(
                "SELECT * FROM budget.vw_BudgetComparison WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)",
                new { FinancialYear = financialYear });
            return result.ToList();
        }

        public async Task<List<CostCenterReportItemViewModel>> GetCostCenterReportAsync(int? financialYear = null, string? fundCode = null, string? costCenterCode = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT TOP 5000
                    FinancialYear,
                    DepartmentCode,
                    FundCode,
                    CostCenterCode,
                    CommitmentItem,
                    SUM(Posts) AS Posts,
                    SUM(OriginalBudgetAmount) AS OriginalBudgetAmount,
                    SUM(RevisedBudgetAmount) AS RevisedBudgetAmount
                FROM budget.OriginalBudget WITH (NOLOCK)
                WHERE IsDeleted = 0
                    AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                    AND (@FundCode IS NULL OR FundCode = @FundCode)
                    AND (@CostCenterCode IS NULL OR CostCenterCode = @CostCenterCode)
                GROUP BY FinancialYear, DepartmentCode, FundCode, CostCenterCode, CommitmentItem
                ORDER BY FinancialYear DESC, CostCenterCode, CommitmentItem";

            var result = await connection.QueryAsync<CostCenterReportItemViewModel>(
                sql,
                new { FinancialYear = financialYear, FundCode = fundCode, CostCenterCode = costCenterCode },
                commandTimeout: 30);

            return result.ToList();
        }

        public async Task<List<LookupItem>> GetCostCenterLookupAsync(int? financialYear = null, string? fundCode = null, string? sector = null, string? departmentCode = null, List<string>? restrictedDepartments = null)
        {
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var cacheKey = $"Lookup_CostCenter_{financialYear}_{fundCode}_{sector}_{departmentCode}_{(hasRestrictedDepartments ? string.Join(",", restrictedDepartments!) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<LookupItem>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT
                    FundCenterCode AS Value,
                    FundCenterCode + CASE WHEN MAX(ISNULL(FundCenterDescription,'')) <> ''
                                    THEN ' - ' + MAX(FundCenterDescription) ELSE '' END AS Text
                FROM budget.BERData WITH (NOLOCK)
                WHERE FundCenterCode IS NOT NULL AND FundCenterCode <> ''
                  AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@FundCode      IS NULL OR FundCode      = @FundCode)
                  AND (@Sector         IS NULL OR Sector             = @Sector)
                  AND (@DepartmentCode IS NULL OR MinistryDepartment = @DepartmentCode)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                GROUP BY FundCenterCode
                ORDER BY FundCenterCode;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new
                {
                    FinancialYear = financialYear,
                    FundCode = fundCode,
                    Sector = sector,
                    DepartmentCode = departmentCode,
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                });
            var list = result.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        public async Task<List<LookupItem>> GetObjectCodeLookupAsync(int? financialYear = null, string? sector = null,
            string? departmentCode = null, string? fundCode = null, IEnumerable<string>? costCenterCodes = null, List<string>? restrictedDepartments = null)
        {
            var ccList = costCenterCodes?.Where(x => !string.IsNullOrWhiteSpace(x)).ToList();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var cacheKey = $"Lookup_ObjectCode_{financialYear}_{sector}_{departmentCode}_{fundCode}_{(ccList != null && ccList.Count > 0 ? string.Join(",", ccList) : "")}_{(hasRestrictedDepartments ? string.Join(",", restrictedDepartments!) : "")}";
            if (_cache.TryGetValue(cacheKey, out List<LookupItem>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    DetailObject AS Value,
                    DetailObject + CASE WHEN MAX(ISNULL(GL_Desc,'')) <> ''
                                    THEN ' - ' + MAX(GL_Desc) ELSE '' END AS Text
                FROM budget.BERData WITH (NOLOCK)
                WHERE DetailObject IS NOT NULL AND DetailObject <> ''
                  AND (@FinancialYear   IS NULL OR FinancialYear      = @FinancialYear)
                  AND (@Sector          IS NULL OR Sector             = @Sector)
                  AND (@DepartmentCode  IS NULL OR MinistryDepartment = @DepartmentCode)
                  AND (@FundCode        IS NULL OR FundCode           = @FundCode)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)"
                + (ccList != null && ccList.Count > 0 ? " AND FundCenterCode IN @CcList" : "") + @"
                GROUP BY DetailObject
                ORDER BY DetailObject;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new
                {
                    FinancialYear = financialYear,
                    Sector = sector,
                    DepartmentCode = departmentCode,
                    FundCode = fundCode,
                    CcList = ccList ?? new List<string>(),
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                });
            var list = result.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        public async Task<List<LookupItem>> GetDepartmentLookupAsync(int? financialYear = null, string? sector = null, List<string>? sectors = null)
        {
            var hasSectors = sectors != null && sectors.Count > 0;
            var cacheKey = $"Lookup_Department_{financialYear}_{sector}_{(hasSectors ? string.Join(",", sectors!) : string.Empty)}";
            if (_cache.TryGetValue(cacheKey, out List<LookupItem>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT
                    B.MinistryDepartment AS Value,
                    B.MinistryDepartment + ISNULL(' - ' + D.DepartmentName, '') AS Text
                FROM budget.BERData B WITH (NOLOCK)
                LEFT JOIN budget.Departments D ON B.MinistryDepartment = D.DepartmentCode
                WHERE B.MinistryDepartment IS NOT NULL AND B.MinistryDepartment <> ''
                  AND (@FinancialYear IS NULL OR B.FinancialYear = @FinancialYear)
                  AND (@Sector IS NULL OR B.Sector = @Sector)
                  AND (@HasSectors = 0 OR B.Sector IN @Sectors)
                ORDER BY B.MinistryDepartment;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new
                {
                    FinancialYear = financialYear,
                    Sector = sector,
                    HasSectors = hasSectors,
                    Sectors = hasSectors ? sectors : new List<string> { string.Empty }
                });
            var list = result.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        public async Task<List<LookupItem>> GetSectorLookupAsync(int? financialYear = null)
        {
            var cacheKey = $"Lookup_Sector_{financialYear}";
            if (_cache.TryGetValue(cacheKey, out List<LookupItem>? cached) && cached != null)
                return cached;

            using var connection = CreateConnection();
            var sql = @"
                SELECT DISTINCT
                    Sector AS Value,
                    Sector AS Text
                FROM budget.BERData WITH (NOLOCK)
                WHERE Sector IS NOT NULL AND Sector <> ''
                  AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                ORDER BY Sector;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new { FinancialYear = financialYear });
            var list = result.ToList();
            _cache.Set(cacheKey, list, LookupCacheDuration);
            return list;
        }

        // Internal DTO for strongly-typed Dapper mapping
        private class NineItemsRaw
        {
            public string Sector { get; set; } = string.Empty;
            public string Object_nine_item { get; set; } = string.Empty;
            public string FundCode { get; set; } = string.Empty;
            public decimal AllocatedBudget { get; set; }
            public decimal OriginalBudgetReleased { get; set; }
            public decimal SupplementaryBudget { get; set; }
            public decimal ReAppPlus { get; set; }
            public decimal ReAppMinus { get; set; }
            public decimal BudgetSurrender { get; set; }
            public decimal FinalBudget { get; set; }
            public decimal TotalActualExpenditure { get; set; }
        }

        public async Task<List<NineItemsReportRowViewModel>> GetDeptWiseReportAsync(int? financialYear = null, List<string>? sectors = null, List<string>? departmentCodes = null, List<string>? restrictedDepartments = null)
        {
            using var connection = CreateConnection();
            var hasRestrictedDepartments = restrictedDepartments != null && restrictedDepartments.Count > 0;
            var hasSectors = sectors != null && sectors.Count > 0;
            var hasDepartmentCodes = departmentCodes != null && departmentCodes.Count > 0;

            var sql = @"
                SELECT
                    ISNULL(DepartmentName, MinistryDepartment) AS DepartmentName,
                    Sector,
                    ISNULL(SUM(allocated_budget),   0)  AS AllocatedBudget,
                    ISNULL(SUM(Releases),           0)  AS OriginalBudgetReleased,
                    ISNULL(SUM(SupplementaryGrant), 0)  AS SupplementaryBudget,
                    ISNULL(SUM(ReappropriationPlus),  0) AS ReAppPlus,
                    ISNULL(SUM(ReappropriationMinus), 0) AS ReAppMinus,
                    ISNULL(SUM(TSSSurrender),       0)  AS BudgetSurrender,
                    ISNULL(SUM(FinalGrant),         0)  AS FinalBudget,
                    ISNULL(SUM(ActualExpenditure),  0)  AS TotalActualExpenditure
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear   IS NULL OR FinancialYear = @FinancialYear)
                  AND (@HasSectors = 0 OR Sector IN @Sectors)
                  AND (@HasDepartmentCodes = 0 OR MinistryDepartment IN @DepartmentCodes)
                  AND (@HasRestrictedDepartments = 0 OR MinistryDepartment IN @RestrictedDepartments)
                  AND ISNULL(DepartmentName, MinistryDepartment) IS NOT NULL
                  AND ISNULL(DepartmentName, MinistryDepartment) <> ''
                  AND Sector IS NOT NULL AND Sector <> ''
                GROUP BY ISNULL(DepartmentName, MinistryDepartment), Sector
                OPTION (RECOMPILE);";

            var rawList = (await connection.QueryAsync<DeptWiseRaw>(
                sql,
                new
                {
                    FinancialYear = financialYear,
                    HasSectors = hasSectors,
                    Sectors = hasSectors ? sectors : new List<string> { string.Empty },
                    HasDepartmentCodes = hasDepartmentCodes,
                    DepartmentCodes = hasDepartmentCodes ? departmentCodes : new List<string> { string.Empty },
                    HasRestrictedDepartments = hasRestrictedDepartments,
                    RestrictedDepartments = hasRestrictedDepartments ? restrictedDepartments : new List<string> { string.Empty }
                },
                commandTimeout: 120)).ToList();


            var rows = new List<NineItemsReportRowViewModel>();
            var grandTotal = new NineItemsReportRowViewModel { Description = "Grand Total", RowType = "grand_total" };

            foreach (var deptGroup in rawList.GroupBy(r => r.DepartmentName).OrderBy(g => g.Key))
            {
                var deptRow = new NineItemsReportRowViewModel { Description = deptGroup.Key, RowType = "dept" };
                foreach (var r in deptGroup)
                {
                    deptRow.AllocatedBudget        += r.AllocatedBudget;
                    deptRow.OriginalBudgetReleased += r.OriginalBudgetReleased;
                    deptRow.SupplementaryBudget     += r.SupplementaryBudget;
                    deptRow.ReAppPlus               += r.ReAppPlus;
                    deptRow.ReAppMinus              += r.ReAppMinus;
                    deptRow.BudgetSurrender          += r.BudgetSurrender;
                    deptRow.FinalBudget              += r.FinalBudget;
                    deptRow.TotalActualExpenditure   += r.TotalActualExpenditure;
                }
                rows.Add(deptRow);

                grandTotal.AllocatedBudget        += deptRow.AllocatedBudget;
                grandTotal.OriginalBudgetReleased += deptRow.OriginalBudgetReleased;
                grandTotal.SupplementaryBudget     += deptRow.SupplementaryBudget;
                grandTotal.ReAppPlus               += deptRow.ReAppPlus;
                grandTotal.ReAppMinus              += deptRow.ReAppMinus;
                grandTotal.BudgetSurrender          += deptRow.BudgetSurrender;
                grandTotal.FinalBudget              += deptRow.FinalBudget;
                grandTotal.TotalActualExpenditure   += deptRow.TotalActualExpenditure;

                foreach (var r in deptGroup.OrderBy(x => x.Sector))
                {
                    rows.Add(new NineItemsReportRowViewModel
                    {
                        Description             = r.Sector,
                        RowType                  = "sector",
                        AllocatedBudget          = r.AllocatedBudget,
                        OriginalBudgetReleased   = r.OriginalBudgetReleased,
                        SupplementaryBudget      = r.SupplementaryBudget,
                        ReAppPlus                = r.ReAppPlus,
                        ReAppMinus               = r.ReAppMinus,
                        BudgetSurrender          = r.BudgetSurrender,
                        FinalBudget              = r.FinalBudget,
                        TotalActualExpenditure   = r.TotalActualExpenditure
                    });
                }
            }

            rows.Add(grandTotal);
            return rows;
        }

        // Internal DTO for strongly-typed Dapper mapping
        private class DeptWiseRaw
        {
            public string DepartmentName { get; set; } = string.Empty;
            public string Sector { get; set; } = string.Empty;
            public decimal AllocatedBudget { get; set; }
            public decimal OriginalBudgetReleased { get; set; }
            public decimal SupplementaryBudget { get; set; }
            public decimal ReAppPlus { get; set; }
            public decimal ReAppMinus { get; set; }
            public decimal BudgetSurrender { get; set; }
            public decimal FinalBudget { get; set; }
            public decimal TotalActualExpenditure { get; set; }
        }

        public async Task<List<NineItemsReportRowViewModel>> GetNineItemsReportAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();

            var sql = @"
                SELECT
                    Sector,
                    object_nine_item                                             AS Object_nine_item,
                    FundCode,
                    ISNULL(SUM(allocated_budget),   0)                          AS AllocatedBudget,
                    ISNULL(SUM(Releases),           0)                          AS OriginalBudgetReleased,
                    ISNULL(SUM(SupplementaryGrant), 0)                          AS SupplementaryBudget,
                    ISNULL(SUM(ReappropriationPlus),  0)                         AS ReAppPlus,
                    ISNULL(SUM(ReappropriationMinus), 0)                         AS ReAppMinus,
                    ISNULL(SUM(TSSSurrender),       0)                          AS BudgetSurrender,
                    ISNULL(SUM(FinalGrant),         0)                          AS FinalBudget,
                    ISNULL(SUM(ActualExpenditure),  0)                          AS TotalActualExpenditure
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND Sector IS NOT NULL AND Sector <> ''
                GROUP BY Sector, object_nine_item, FundCode
                OPTION (RECOMPILE);";

            // Strongly-typed — no dynamic boxing overhead
            var rawList = (await connection.QueryAsync<NineItemsRaw>(
                sql, new { FinancialYear = financialYear }, commandTimeout: 120)).ToList();

            // Build lookup: (sector, nineItem, fundCode) → row  — O(1) access
            var lookup = rawList
                .GroupBy(r => (r.Sector, r.Object_nine_item, r.FundCode))
                .ToDictionary(g => g.Key, g => g.First());

            // Helper: sum all raw rows matching a predicate
            NineItemsReportRowViewModel Sum(IEnumerable<NineItemsRaw> items, string desc, string rowType)
            {
                var vm = new NineItemsReportRowViewModel { Description = desc, RowType = rowType };
                foreach (var i in items)
                {
                    vm.AllocatedBudget          += i.AllocatedBudget;
                    vm.OriginalBudgetReleased   += i.OriginalBudgetReleased;
                    vm.SupplementaryBudget      += i.SupplementaryBudget;
                    vm.ReAppPlus                += i.ReAppPlus;
                    vm.ReAppMinus               += i.ReAppMinus;
                    vm.BudgetSurrender          += i.BudgetSurrender;
                    vm.FinalBudget              += i.FinalBudget;
                    vm.TotalActualExpenditure   += i.TotalActualExpenditure;
                }
                return vm;
            }

            // Pre-group by sector for fast access
            var bySector = rawList
                .GroupBy(r => r.Sector)
                .ToDictionary(g => g.Key, g => g.ToList());

            var rows = new List<NineItemsReportRowViewModel>();

            var sectorOrder = new[] { "Capital-A/c-I", "Capital-A/c-II", "Development", "Non Development" };
            var pfcLabels   = new Dictionary<string, string>
            {
                { "PC21103", "DEAs" },
                { "PC21108", "DHAs" },
                { "PC21175", "LGs" }
            };
            var sdItems = new[] { "4.Subsidies", "5.Operating Expense", "6.Interest Payment", "7.Physical Assets", "8.M&R", "9.Remaining" };
            var sdSet   = new HashSet<string>(sdItems);

            var grandTotals = new NineItemsReportRowViewModel { Description = "Grand Total", RowType = "grand_total" };

            foreach (var sector in sectorOrder)
            {
                if (!bySector.TryGetValue(sector, out var sectorData)) continue;

                var sectorRow = Sum(sectorData, sector, "sector");
                rows.Add(sectorRow);

                grandTotals.AllocatedBudget         += sectorRow.AllocatedBudget;
                grandTotals.OriginalBudgetReleased  += sectorRow.OriginalBudgetReleased;
                grandTotals.SupplementaryBudget     += sectorRow.SupplementaryBudget;
                grandTotals.ReAppPlus               += sectorRow.ReAppPlus;
                grandTotals.ReAppMinus              += sectorRow.ReAppMinus;
                grandTotals.BudgetSurrender         += sectorRow.BudgetSurrender;
                grandTotals.FinalBudget             += sectorRow.FinalBudget;
                grandTotals.TotalActualExpenditure  += sectorRow.TotalActualExpenditure;

                if (sector == "Non Development")
                {
                    // Pre-group by nineItem for O(1) lookups
                    var byNineItem = sectorData
                        .GroupBy(r => r.Object_nine_item)
                        .ToDictionary(g => g.Key, g => g.ToList());

                    if (byNineItem.TryGetValue("1.Salary", out var sal))
                        rows.Add(Sum(sal, "1.Salary", "item"));

                    if (byNineItem.TryGetValue("2.Pension", out var pen))
                        rows.Add(Sum(pen, "2.Pension", "item"));

                    if (byNineItem.TryGetValue("3.PFC", out var pfc))
                    {
                        rows.Add(Sum(pfc, "3.PFC", "item"));
                        var pfcByFund = pfc.GroupBy(r => r.FundCode).ToDictionary(g => g.Key, g => g.ToList());
                        foreach (var fc in pfcLabels)
                        {
                            if (pfcByFund.TryGetValue(fc.Key, out var sub))
                                rows.Add(Sum(sub, fc.Value, "pfc_sub"));
                        }
                    }

                    var sdData = sectorData.Where(r => sdSet.Contains(r.Object_nine_item)).ToList();
                    if (sdData.Any())
                    {
                        rows.Add(Sum(sdData, "SERVICE DELIVERY", "service_delivery"));
                        var bySd = sdData.GroupBy(r => r.Object_nine_item).ToDictionary(g => g.Key, g => g.ToList());
                        foreach (var item in sdItems)
                        {
                            if (bySd.TryGetValue(item, out var itemData))
                                rows.Add(Sum(itemData, item, "item"));
                        }
                    }
                }
            }

            rows.Add(grandTotals);
            return rows;
        }

        public async Task<(int rowsCopied, int failedStep, string message)> UpdateBERDataAsync()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();

            try
            {
                // Step 1: Delete all from BERData and re-insert from BerSapData
                var step1Sql = @"
                DELETE FROM budget.BERData;

                INSERT INTO budget.BERData
                (
                    FinancialYear, FundCode, FundCenterCode, FundCenterDescription,
                    MinistryDepartment, AttachedDepartment, ProfitCenter, SubDetailFunction,
                    DetailObject, BudgetGrant, SupplementaryGrant, ReappropriationPlus,
                    ReappropriationMinus, TSSSurrender, FinalGrant, Releases,
                    JulExp, AugExp, SepExp, OctExp, NovExp, DecExp,
                    JanExp, FebExp, MarExp, AprExp, MayExp, JunExp,
                    ActualExpenditure, FinalGrantBalance
                )
                SELECT
                    FinancialYear, FundCode, FundCenterCode, FundCenterDescription,
                    MinistryDepartment, AttachedDepartment, ProfitCenter, SubDetailFunction,
                    DetailObject, BudgetGrant, SupplementaryGrant, ReappropriationPlus,
                    ReappropriationMinus, TSSSurrender, FinalGrant, Releases,
                    JulExp, AugExp, SepExp, OctExp, NovExp, DecExp,
                    JanExp, FebExp, MarExp, AprExp, MayExp, JunExp,
                    ActualExpenditure, FinalGrantBalance
                FROM budget.BerSapData;

                SELECT @@ROWCOUNT;";

                var rowsCopied = await connection.ExecuteScalarAsync<int>(
                    step1Sql, param: null, transaction: transaction, commandTimeout: 600);

                // Step 2: MERGE allocated_budget from OriginalBudget
                var step2Sql = @"
                ;WITH BudgetData AS
                (
                    SELECT
                        FinancialYear,
                        FundCode,
                        CostCenterCode,
                        CommitmentItem,
                        DepartmentCode,
                        SUM(OriginalBudgetAmount) AS AllocatedBudget
                    FROM [budget].[OriginalBudget]
                    GROUP BY
                        FinancialYear,
                        FundCode,
                        CostCenterCode,
                        CommitmentItem,
                        DepartmentCode
                )
                MERGE [budget].[BERData] AS T
                USING BudgetData AS S
                ON  T.FinancialYear  = S.FinancialYear
                AND T.FundCode       = S.FundCode
                AND T.FundCenterCode = S.CostCenterCode
                AND T.DetailObject   = S.CommitmentItem
                WHEN MATCHED THEN
                    UPDATE SET
                        T.allocated_budget   = S.AllocatedBudget,
                        T.MinistryDepartment = S.DepartmentCode
                WHEN NOT MATCHED BY TARGET AND S.AllocatedBudget > 0 THEN
                    INSERT (FinancialYear, FundCode, FundCenterCode, DetailObject, MinistryDepartment, allocated_budget)
                    VALUES (S.FinancialYear, S.FundCode, S.CostCenterCode, S.CommitmentItem, S.DepartmentCode, S.AllocatedBudget);";

                await connection.ExecuteAsync(step2Sql, param: null, transaction: transaction, commandTimeout: 600);

                // Step 3: Update Fund_Des and sector from Sec_Fund
                var step3Sql = @"
                UPDATE T
                SET T.Fund_Des = SF.Fund_Des,
                    T.sector   = SF.sector
                FROM budget.BERData T
                INNER JOIN budget.Sec_Fund SF ON T.FundCode = SF.[Fund_Code];";

                await connection.ExecuteAsync(step3Sql, param: null, transaction: transaction, commandTimeout: 300);

                // Step 4: Update GL_Desc from Gl_Codes
                var step4Sql = @"
                UPDATE T
                SET T.GL_Desc = GL.GL_Desc
                FROM budget.BERData T
                INNER JOIN budget.Gl_Codes GL ON T.DetailObject = GL.[GL_Code];";

                await connection.ExecuteAsync(step4Sql, param: null, transaction: transaction, commandTimeout: 300);

                // Step 5: Update cost centre mapping columns from CostCentersMapping
                var step5Sql = @"
                UPDATE T
                SET T.CostCenterDescription = CCM.CostCenterDescription,
                    T.OldCostCenter         = CCM.OldCostCenter,
                    T.OldCostCenterDes      = CCM.OldCostCenterDes,
                    T.OldFund               = CCM.OldFund,
                    T.OldFundCenter         = CCM.OldFundCenter
                FROM budget.BERData T
                INNER JOIN budget.CostCentersMapping CCM ON T.FundCenterCode = CCM.costcenter;";

                await connection.ExecuteAsync(step5Sql, param: null, transaction: transaction, commandTimeout: 300);

                // Step 6: Update DepartmentName from Departments table using MinistryDepartment
                var step6Sql = @"
                UPDATE T
                SET T.DepartmentName = D.DepartmentName
                FROM budget.BERData T
                INNER JOIN budget.Departments D ON T.MinistryDepartment = D.DepartmentCode;";

                await connection.ExecuteAsync(step6Sql, param: null, transaction: transaction, commandTimeout: 300);

                transaction.Commit();
                return (rowsCopied, 0, $"BERData updated successfully. {rowsCopied} records copied from BerSapData.");
            }
            catch (Exception)
            {
                transaction.Rollback();
                throw;
            }
        }

        // ── Individual step methods — each opens its own connection and commits independently ──

        public async Task<int> UpdateBERDataStep1Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            {
                // TRUNCATE is minimally logged and instant (unlike DELETE, which logs every
                // row individually). Non-clustered indexes are DISABLEd before the bulk insert
                // and REBUILT once afterward — maintaining 6 indexes row-by-row during a
                // ~290k-row insert requires large sort memory grants, which can stall for a
                // long time (RESOURCE_SEMAPHORE waits) on machines/hosts under memory pressure.
                // Disable + single rebuild avoids that entirely and is dramatically faster.
                //
                // NO explicit transaction: TRUNCATE takes a SCH-M lock on the table. Wrapping
                // everything in one transaction held that lock for the entire insert + rebuild,
                // so if ANY other connection was reading BERData (an open dashboard page),
                // the TRUNCATE waited indefinitely and the UI froze at 2%. Each statement now
                // auto-commits, and LOCK_TIMEOUT makes a blocked TRUNCATE fail fast with a
                // clear error instead of hanging. Rerunning Step 1 fully rebuilds the table,
                // so a mid-way failure carries no consistency risk.
                var sql = @"
                SET LOCK_TIMEOUT 30000;

                TRUNCATE TABLE budget.BERData;

                ALTER INDEX IX_BERData_FinancialYear      ON budget.BERData DISABLE;
                ALTER INDEX IX_BERData_FundCode            ON budget.BERData DISABLE;
                ALTER INDEX IX_BERData_FundCenterCode      ON budget.BERData DISABLE;
                ALTER INDEX IX_BERData_MinistryDepartment  ON budget.BERData DISABLE;
                ALTER INDEX IX_BERData_DetailObject        ON budget.BERData DISABLE;
                ALTER INDEX IX_BERData_Sector_NineItem     ON budget.BERData DISABLE;

                INSERT INTO budget.BERData WITH (TABLOCK)
                (
                    FinancialYear, FundCode, FundCenterCode, FundCenterDescription,
                    MinistryDepartment, AttachedDepartment, ProfitCenter, SubDetailFunction,
                    DetailObject, BudgetGrant, SupplementaryGrant, ReappropriationPlus,
                    ReappropriationMinus, TSSSurrender, FinalGrant, Releases,
                    JulExp, AugExp, SepExp, OctExp, NovExp, DecExp,
                    JanExp, FebExp, MarExp, AprExp, MayExp, JunExp,
                    ActualExpenditure, FinalGrantBalance
                )
                SELECT
                    FinancialYear, FundCode, FundCenterCode, FundCenterDescription,
                    MinistryDepartment, AttachedDepartment, ProfitCenter, SubDetailFunction,
                    DetailObject, BudgetGrant, SupplementaryGrant, ReappropriationPlus,
                    ReappropriationMinus, TSSSurrender, FinalGrant, Releases,
                    JulExp, AugExp, SepExp, OctExp, NovExp, DecExp,
                    JanExp, FebExp, MarExp, AprExp, MayExp, JunExp,
                    ActualExpenditure, FinalGrantBalance
                FROM budget.BerSapData;

                ALTER INDEX IX_BERData_FinancialYear      ON budget.BERData REBUILD;
                ALTER INDEX IX_BERData_FundCode            ON budget.BERData REBUILD;
                ALTER INDEX IX_BERData_FundCenterCode      ON budget.BERData REBUILD;
                ALTER INDEX IX_BERData_MinistryDepartment  ON budget.BERData REBUILD;
                ALTER INDEX IX_BERData_DetailObject        ON budget.BERData REBUILD;
                ALTER INDEX IX_BERData_Sector_NineItem     ON budget.BERData REBUILD;

                SELECT COUNT(*) FROM budget.BERData;";


                var rows = await connection.ExecuteScalarAsync<int>(sql, commandTimeout: 600);
                return rows;
            }
        }

        public async Task UpdateBERDataStep2Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                ;WITH BudgetData AS
                (
                    SELECT
                        FinancialYear,
                        FundCode,
                        CostCenterCode,
                        CommitmentItem,
                        DepartmentCode,
                        SUM(OriginalBudgetAmount) AS AllocatedBudget
                    FROM [budget].[OriginalBudget]
                    GROUP BY
                        FinancialYear,
                        FundCode,
                        CostCenterCode,
                        CommitmentItem,
                        DepartmentCode
                )
                MERGE [budget].[BERData] AS T
                USING BudgetData AS S
                ON  T.FinancialYear  = S.FinancialYear
                AND T.FundCode       = S.FundCode
                AND T.FundCenterCode = S.CostCenterCode
                AND T.DetailObject   = S.CommitmentItem
                WHEN MATCHED THEN
                    UPDATE SET
                        T.allocated_budget   = S.AllocatedBudget,
                        T.MinistryDepartment = S.DepartmentCode
                WHEN NOT MATCHED BY TARGET AND S.AllocatedBudget > 0 THEN
                    INSERT (FinancialYear, FundCode, FundCenterCode, DetailObject, MinistryDepartment, allocated_budget)
                    VALUES (S.FinancialYear, S.FundCode, S.CostCenterCode, S.CommitmentItem, S.DepartmentCode, S.AllocatedBudget);";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 600);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateBERDataStep3Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                UPDATE T
                SET T.Fund_Des = SF.Fund_Des,
                    T.sector   = SF.sector
                FROM budget.BERData T
                INNER JOIN budget.Sec_Fund SF ON T.FundCode = SF.[Fund_Code];";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 300);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateBERDataStep4Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                UPDATE T
                SET T.GL_Desc = GL.GL_Desc
                FROM budget.BERData T
                INNER JOIN budget.Gl_Codes GL ON T.DetailObject = GL.[GL_Code];";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 300);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateBERDataStep5Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                UPDATE T
                SET T.CostCenterDescription = CCM.CostCenterDescription,
                    T.OldCostCenter         = CCM.OldCostCenter,
                    T.OldCostCenterDes      = CCM.OldCostCenterDes,
                    T.OldFund               = CCM.OldFund,
                    T.OldFundCenter         = CCM.OldFundCenter
                FROM budget.BERData T
                INNER JOIN budget.CostCentersMapping CCM ON T.FundCenterCode = CCM.costcenter;";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 300);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateBERDataStep6Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                UPDATE T
                SET T.DepartmentName = D.DepartmentName
                FROM budget.BERData T
                INNER JOIN budget.Departments D ON T.MinistryDepartment = D.DepartmentCode;";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 300);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }

        public async Task UpdateBERDataStep7Async()
        {
            using var connection = CreateConnection();
            await connection.OpenAsync();
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
                UPDATE [budget].[BERData]
                SET [object_nine_item] =
                CASE
                    -- Highest Priority
                    WHEN [FundCenterCode] IN (
                        'LO005581','LO005580','AK004118','AK004129','AK004131',
                        'AK005579','BH005579','BK005579','BR004106','BR005579',
                        'CH005579','CT005579','DG005579','FD005579','GA004239',
                        'GA005579','GT004107','GT005579','HF005579','JG004095',
                        'JG005579','JM004120','JM005579','KB005579','KS005579',
                        'KW005579','LN005579','LO004895','LO004902','LO005512',
                        'LO005579','LY005579','MB005579','MH005579','MI005579',
                        'MN004467','MN005579','NK005579','NL005579','OK004109',
                        'OK005579','PK005579','RI004424','RI004425','RI004426',
                        'RI004427','RI004428','RI005579','RN005579','RP005579',
                        'SA005579','SG004038','SG005579','SL005579','ST004094',
                        'ST005579','TS005579','VR005579'
                    ) THEN '3.PFC'

                    WHEN [FundCode] = 'PC21260' THEN '2.Pension'

                    WHEN [FundCenterCode] IN (
                        'BR004963',
                        'LO004891',
                        'LO005270',
                        'LO005291',
                        'LO005292',
                        'LO005652',
                        'LO005797',
                        'LO005902',
                        'LO005916',
                        'LO005992',
                        'LO001013'
                    ) THEN '4.Subsidies'

                    WHEN [DetailObject] LIKE 'A01%' THEN '1.Salary'

                    WHEN [DetailObject] LIKE 'A03%' THEN '5.Operating Expense'

                    WHEN [DetailObject] LIKE 'A07%' THEN '6.Interest Payment'

                    WHEN [DetailObject] LIKE 'A09%' THEN '7.Physical Assets'

                    WHEN [DetailObject] LIKE 'A13%' THEN '8.M&R'

                    ELSE '9.Remaining'
                END;";

                await connection.ExecuteAsync(sql, param: null, transaction: transaction, commandTimeout: 300);
                transaction.Commit();
            }
            catch { transaction.Rollback(); throw; }
        }
    }
}
