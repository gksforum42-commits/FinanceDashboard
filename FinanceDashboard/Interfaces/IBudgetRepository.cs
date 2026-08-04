using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface IBudgetRepository
    {
        // Dashboard
        Task<DashboardSummaryViewModel> GetDashboardSummaryAsync(int? financialYear = null);
        Task<List<ChartDataViewModel>> GetBudgetChartDataAsync(int? financialYear = null);
        Task<List<MonthlyExpenditureViewModel>> GetMonthlyExpenditureAsync(int financialYear, string? fundCode = null);

        // Original Budget
        Task<PagedResultViewModel<OriginalBudget>> GetOriginalBudgetAsync(FilterViewModel filters, PaginationViewModel pagination);
        Task<OriginalBudget?> GetOriginalBudgetByIdAsync(long id);
        Task<int> InsertOriginalBudgetAsync(List<OriginalBudget> records);
        Task<int> DeleteOriginalBudgetByImportIdAsync(long importId);

        // BER Data
        Task<PagedResultViewModel<BERData>> GetBERDataAsync(FilterViewModel filters, PaginationViewModel pagination);
        Task<BERData?> GetBERDataByIdAsync(long id);
        Task TruncateBerSapDataAsync();
        Task<int> InsertBERDataAsync(List<BERData> records);

        // Import History
        Task<List<ImportHistory>> GetImportHistoryAsync(string? importType = null, int pageNumber = 1, int pageSize = 20);
        Task<ImportHistory?> GetImportHistoryByIdAsync(long id);
        Task<long> AddImportHistoryAsync(ImportHistory importHistory);
        Task UpdateImportHistoryAsync(ImportHistory importHistory);
        Task<bool> DeleteImportAsync(long importId, string deletedBy);

        // Lookups
        Task<List<LookupItem>> GetLookupValuesAsync(string type, string? searchTerm = null);
        Task<List<LookupItem>> GetFundLookupAsync(int? financialYear = null, string? departmentCode = null, string? sector = null);
        Task<List<LookupItem>> GetDepartmentLookupAsync(int? financialYear = null, string? sector = null);
        Task<List<LookupItem>> GetSectorLookupAsync(int? financialYear = null);
        Task<List<int>> GetFinancialYearsAsync();

        // Reports
        Task<List<FundWiseReportViewModel>> GetFundWiseReportAsync(int? financialYear = null, string? fundCode = null, string? keyword = null, string? sector = null);
        Task<List<FundWiseReportViewModel>> GetDrillFundsAsync(string departmentName, int? financialYear = null, string? sector = null);
        Task<List<CostCenterWiseReportViewModel>> GetDrillCostCentersAsync(string departmentName, string fundCode, int? financialYear = null, string? sector = null);
        Task<List<DepartmentWiseReportViewModel>> GetDepartmentWiseReportAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null);
        Task<List<CostCenterWiseReportViewModel>> GetCostCenterWiseReportAsync(int? financialYear = null, string? fundCode = null, string? costCenterCode = null);
        Task<List<CostCenterGLDetailViewModel>> GetCostCenterGLDetailAsync(string costCenterCode, int? financialYear = null, string? fundCode = null);
        Task<List<FunctionWiseReportViewModel>> GetFunctionWiseReportAsync(int? financialYear = null, string? keyword = null);
        Task<List<ObjectWiseReportViewModel>> GetObjectWiseReportAsync(int? financialYear = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, string? sector = null);
        Task<List<ObjectWiseGLDetailViewModel>> GetObjectWiseGLDetailAsync(string detailObject, int? financialYear = null);
        Task<List<ObjectWiseGroupedViewModel>> GetObjectWiseGroupedAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null);
        Task<List<CompleteBudgetReportViewModel>> GetCompleteBudgetReportAsync(FilterViewModel filters, int pageNumber = 1, int pageSize = 50);
        Task<List<BudgetComparisonViewModel>> GetBudgetComparisonAsync(int? financialYear = null);
        Task<List<CostCenterReportItemViewModel>> GetCostCenterReportAsync(int? financialYear = null, string? fundCode = null, string? costCenterCode = null);
        Task<List<LookupItem>> GetCostCenterLookupAsync(int? financialYear = null, string? fundCode = null);

        Task<List<NineItemsReportRowViewModel>> GetNineItemsReportAsync(int? financialYear = null);

        // BER Update
        Task<(int rowsCopied, int failedStep, string message)> UpdateBERDataAsync();
        Task<int> UpdateBERDataStep1Async();
        Task UpdateBERDataStep2Async();
        Task UpdateBERDataStep3Async();
        Task UpdateBERDataStep4Async();
        Task UpdateBERDataStep5Async();
        Task UpdateBERDataStep6Async();
    }

    public class LookupItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
