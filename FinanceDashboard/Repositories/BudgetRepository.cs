using Dapper;
using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FinanceDashboard.Repositories
{
    public class BudgetRepository : IBudgetRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly string _connectionString;

        public BudgetRepository(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
            _connectionString = _configuration.GetConnectionString("DefaultConnection")!;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        // Dashboard
        public async Task<DashboardSummaryViewModel> GetDashboardSummaryAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    -- Allocated Budget per sector
                    ISNULL(SUM(CASE WHEN sector = 'Non Development' THEN allocated_budget   ELSE 0 END), 0) AS NonDevelopmentBudget,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-I'   THEN allocated_budget   ELSE 0 END), 0) AS CapitalAc1Budget,
                    ISNULL(SUM(CASE WHEN sector = 'Capital-A/c-II'  THEN allocated_budget   ELSE 0 END), 0) AS CapitalAc2Budget,
                    ISNULL(SUM(CASE WHEN sector = 'Development'      THEN allocated_budget   ELSE 0 END), 0) AS DevelopmentBudget,
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
                    COUNT(1) AS TotalBERRecords
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                OPTION (RECOMPILE);";

            var summary = await connection.QueryFirstOrDefaultAsync<DashboardSummaryViewModel>(
                sql, new { FinancialYear = financialYear }, commandTimeout: 120);

            return summary ?? new DashboardSummaryViewModel();
        }

        public async Task<List<ChartDataViewModel>> GetBudgetChartDataAsync(int? financialYear = null)
        {
            using var connection = CreateConnection();
            var charts = new List<ChartDataViewModel>();

            // Fund-wise Allocated Budget distribution from BERData
            var fundData = await connection.QueryAsync(
                @"SELECT TOP 10 FundCode AS Label, ISNULL(SUM(allocated_budget), 0) AS Value
                  FROM budget.BERData WITH (NOLOCK)
                  WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  GROUP BY FundCode ORDER BY Value DESC",
                new { FinancialYear = financialYear });

            charts.Add(new ChartDataViewModel
            {
                Labels = fundData.Select(f => (string)f.Label).ToArray(),
                Data = fundData.Select(f => (decimal)f.Value).ToArray(),
                BackgroundColors = new[] { "#0d6efd", "#6610f2", "#6f42c1", "#d63384", "#dc3545", "#fd7e14", "#ffc107", "#198754", "#20c997", "#0dcaf0" }
            });

            return charts;
        }

        public async Task<List<MonthlyExpenditureViewModel>> GetMonthlyExpenditureAsync(int financialYear, string? fundCode = null)
        {
            using var connection = CreateConnection();
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
                GROUP BY v.MonthOrder, v.Month
                ORDER BY v.MonthOrder;";

            var result = await connection.QueryAsync<MonthlyExpenditureViewModel>(
                sql,
                new { FinancialYear = financialYear, FundCode = fundCode },
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

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = "budget.OriginalBudget",
                BatchSize = 10000,
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

            using var bulkCopy = new SqlBulkCopy(connection)
            {
                DestinationTableName = "budget.BerSapData",
                BatchSize = 50000,
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


        // Import History
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

        public async Task<List<LookupItem>> GetFundLookupAsync(int? financialYear = null, string? departmentCode = null, string? sector = null)
        {
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
                GROUP BY FundCode
                ORDER BY FundCode;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new { FinancialYear = financialYear, DepartmentCode = departmentCode, Sector = sector });
            return result.ToList();
        }

        public async Task<List<int>> GetFinancialYearsAsync()
        {
            using var connection = CreateConnection();
            var years = await connection.QueryAsync<int>(
                @"SELECT DISTINCT FinancialYear FROM budget.OriginalBudget WHERE IsDeleted = 0
                  UNION
                  SELECT DISTINCT FinancialYear FROM budget.BERData
                  ORDER BY FinancialYear DESC");
            return years.ToList();
        }

        // Reports
        public async Task<List<FundWiseReportViewModel>> GetFundWiseReportAsync(int? financialYear = null, string? fundCode = null, string? keyword = null, string? sector = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    FundCode,
                    MAX(ISNULL(Fund_Des, ''))            AS FundDes,
                    ISNULL(SUM(allocated_budget),0)      AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)           AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)              AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)            AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)     AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)     AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
                  AND (@Sector   IS NULL OR Sector   = @Sector)
                  AND (@FundCode IS NULL OR FundCode = @FundCode)
                  AND (@Keyword   IS NULL OR FundCode LIKE '%' + @Keyword + '%'
                                         OR Fund_Des LIKE '%' + @Keyword + '%')
                GROUP BY FundCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<FundWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, Sector = sector, FundCode = fundCode, Keyword = keyword },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<DepartmentWiseReportViewModel>> GetDepartmentWiseReportAsync(
            int? financialYear = null, string? sector = null,
            string? departmentCode = null, string? fundCode = null)
        {
            using var connection = CreateConnection();
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
                GROUP BY ISNULL(DepartmentName, MinistryDepartment)
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<DepartmentWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, Sector = sector,
                      DepartmentCode = departmentCode, FundCode = fundCode },
                commandTimeout: 60);
            return result.ToList();
        }

        // Drill-down: Funds for a specific department
        public async Task<List<FundWiseReportViewModel>> GetDrillFundsAsync(
            string departmentName, int? financialYear = null, string? sector = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    FundCode,
                    MAX(ISNULL(Fund_Des, ''))         AS FundDes,
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
                GROUP BY FundCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<FundWiseReportViewModel>(sql,
                new { DepartmentName = departmentName, FinancialYear = financialYear, Sector = sector },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CostCenterWiseReportViewModel>> GetDrillCostCentersAsync(
            string departmentName, string fundCode, int? financialYear = null, string? sector = null)
        {
            using var connection = CreateConnection();
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
                GROUP BY b.FundCenterCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<CostCenterWiseReportViewModel>(sql,
                new { DepartmentName = departmentName, FundCode = fundCode, FinancialYear = financialYear, Sector = sector },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CostCenterWiseReportViewModel>> GetCostCenterWiseReportAsync(
            int? financialYear = null, string? fundCode = null, string? costCenterCode = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    FundCenterCode,
                    MAX(ISNULL(FundCenterDescription,'')) AS CostCenterDescription,
                    MAX(ISNULL(Fund_Des,''))              AS FundDes,
                    ISNULL(SUM(allocated_budget),0)       AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)            AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)               AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)             AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)      AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)      AS TotalBalance
                FROM budget.BERData WITH (NOLOCK)
                WHERE (@FinancialYear   IS NULL OR FinancialYear   = @FinancialYear)
                  AND (@FundCode        IS NULL OR FundCode        = @FundCode)
                  AND (@CostCenterCode  IS NULL OR FundCenterCode  = @CostCenterCode)
                GROUP BY FundCenterCode
                ORDER BY TotalFinalGrant DESC;";

            var result = await connection.QueryAsync<CostCenterWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, FundCode = fundCode, CostCenterCode = costCenterCode },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<CostCenterGLDetailViewModel>> GetCostCenterGLDetailAsync(
            string costCenterCode, int? financialYear = null, string? fundCode = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    b.DetailObject                           AS GLCode,
                    MAX(ISNULL(b.GL_Desc, b.DetailObject))   AS GLDescription,
                    ISNULL(SUM(b.allocated_budget), 0)       AS TotalAllocatedBudget,
                    ISNULL(SUM(b.BudgetGrant),      0)       AS TotalBudgetGrant,
                    ISNULL(SUM(b.Releases),         0)       AS TotalReleases,
                    ISNULL(SUM(b.FinalGrant),       0)       AS TotalFinalGrant,
                    ISNULL(SUM(b.ActualExpenditure),0)       AS TotalActualExpenditure,
                    ISNULL(SUM(b.FinalGrantBalance),0)       AS TotalBalance
                FROM budget.BERData b WITH (NOLOCK)
                WHERE b.FundCenterCode = @CostCenterCode
                  AND (@FinancialYear IS NULL OR b.FinancialYear = @FinancialYear)
                  AND (@FundCode      IS NULL OR b.FundCode      = @FundCode)
                GROUP BY b.DetailObject
                ORDER BY b.DetailObject;";

            var result = await connection.QueryAsync<CostCenterGLDetailViewModel>(sql,
                new { CostCenterCode = costCenterCode, FinancialYear = financialYear, FundCode = fundCode },
                commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<FunctionWiseReportViewModel>> GetFunctionWiseReportAsync(int? financialYear = null, string? keyword = null)
        {
            using var connection = CreateConnection();
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
                GROUP BY SubDetailFunction
                ORDER BY TotalRevisedBudget DESC;";

            var result = await connection.QueryAsync<FunctionWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, Keyword = keyword }, commandTimeout: 60);
            return result.ToList();
        }

        public async Task<List<ObjectWiseReportViewModel>> GetObjectWiseReportAsync(int? financialYear = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, string? sector = null)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT
                    DetailObject                             AS DetailObject,
                    MAX(GL_Desc)                             AS ObjectDescription,
                    ISNULL(SUM(allocated_budget),0)          AS TotalAllocatedBudget,
                    ISNULL(SUM(BudgetGrant),0)               AS TotalBudgetGrant,
                    ISNULL(SUM(Releases),0)                  AS TotalReleases,
                    ISNULL(SUM(FinalGrant),0)                AS TotalFinalGrant,
                    ISNULL(SUM(ActualExpenditure),0)         AS TotalActualExpenditure,
                    ISNULL(SUM(FinalGrantBalance),0)         AS TotalBalance
                FROM budget.BERData
                WHERE (@FinancialYear   IS NULL OR FinancialYear      = @FinancialYear)
                  AND (@Sector          IS NULL OR Sector             = @Sector)
                  AND (@Department      IS NULL OR MinistryDepartment = @Department)
                  AND (@FundCode        IS NULL OR FundCode           = @FundCode)
                  AND (@CostCenterCode  IS NULL OR FundCenterCode     = @CostCenterCode)
                  AND (@Keyword         IS NULL OR DetailObject LIKE '%' + @Keyword + '%')
                GROUP BY DetailObject
                ORDER BY TotalAllocatedBudget DESC;";

            var result = await connection.QueryAsync<ObjectWiseReportViewModel>(sql,
                new { FinancialYear = financialYear, Sector = sector, Department = departmentCode, FundCode = fundCode, CostCenterCode = costCenterCode, Keyword = keyword }, commandTimeout: 60);
            return result.ToList();
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

        public async Task<List<LookupItem>> GetCostCenterLookupAsync(int? financialYear = null, string? fundCode = null)
        {
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
                GROUP BY FundCenterCode
                ORDER BY FundCenterCode;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new { FinancialYear = financialYear, FundCode = fundCode });
            return result.ToList();
        }

        public async Task<List<LookupItem>> GetDepartmentLookupAsync(int? financialYear = null, string? sector = null)
        {
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
                ORDER BY B.MinistryDepartment;";

            var result = await connection.QueryAsync<LookupItem>(
                sql, new { FinancialYear = financialYear, Sector = sector });
            return result.ToList();
        }

        public async Task<List<LookupItem>> GetSectorLookupAsync(int? financialYear = null)
        {
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
            return result.ToList();
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
            using var transaction = connection.BeginTransaction();
            try
            {
                var sql = @"
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

                var rows = await connection.ExecuteScalarAsync<int>(sql, param: null, transaction: transaction, commandTimeout: 600);
                transaction.Commit();
                return rows;
            }
            catch { transaction.Rollback(); throw; }
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
    }
}
