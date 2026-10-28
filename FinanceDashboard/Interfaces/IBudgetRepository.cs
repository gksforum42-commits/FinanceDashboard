using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface IBudgetRepository
    {
        // Dashboard
        Task<DashboardSummaryViewModel> GetDashboardSummaryAsync(int? financialYear = null, List<string>? restrictedDepartments = null);
        Task<List<ChartDataViewModel>> GetBudgetChartDataAsync(int? financialYear = null, List<string>? restrictedDepartments = null);
        Task<List<MonthlyExpenditureViewModel>> GetMonthlyExpenditureAsync(int financialYear, string? fundCode = null, List<string>? restrictedDepartments = null);

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

        // PnD File Data
        Task TruncatePnDFileAsync();
        Task<int> InsertPnDFileAsync(List<PnDFile> records);
        Task<List<PnDFile>> GetAllPnDFilesAsync();
        Task<int> UpsertPdSecAsync(List<PdSec> records);
        Task<int> UpdatePdSecFromPnDFileAsync();

        // YpProject Data
        Task DeleteYpProjectByYearAsync(int fiscalYear);
        Task<int> InsertYpProjectAsync(List<YpProject> records);

        // YpProject -> devExp Update
        Task DeleteDevExpByYearAsync(int fiscalYear);
        Task<int> MergeYpProjectIntoDevExpAsync(int fiscalYear);
        Task<int> UpdateDevExpGlCodeAsync();
        Task<int> UpdateDevExpGlDescriptionAsync();
        Task<int> UpdateDevExpPnDSectorAsync();

        // Import History
        Task<List<ImportHistory>> GetImportHistoryAsync(string? importType = null, int pageNumber = 1, int pageSize = 20);
        Task<ImportHistory?> GetImportHistoryByIdAsync(long id);
        Task<long> AddImportHistoryAsync(ImportHistory importHistory);
        Task UpdateImportHistoryAsync(ImportHistory importHistory);
        Task UpdateImportHeartbeatAsync(long importId, int recordsSoFar);
        Task<bool> DeleteImportAsync(long importId, string deletedBy);

        // Lookups
        Task<List<LookupItem>> GetLookupValuesAsync(string type, string? searchTerm = null);
        Task<List<LookupItem>> GetFundLookupAsync(int? financialYear = null, string? departmentCode = null, string? sector = null, List<string>? restrictedDepartments = null);
        Task<List<LookupItem>> GetDepartmentLookupAsync(int? financialYear = null, string? sector = null, List<string>? sectors = null);
        Task<List<LookupItem>> GetSectorLookupAsync(int? financialYear = null);
        Task<List<int>> GetFinancialYearsAsync();

        // Reports
        Task<List<FundWiseReportViewModel>> GetFundWiseReportAsync(int? financialYear = null, string? fundCode = null, string? keyword = null, string? sector = null, List<string>? restrictedDepartments = null);
        Task<List<FundWiseReportViewModel>> GetDrillFundsAsync(string departmentName, int? financialYear = null, string? sector = null, List<string>? restrictedDepartments = null);
        Task<List<CostCenterWiseReportViewModel>> GetDrillCostCentersAsync(string departmentName, string fundCode, int? financialYear = null, string? sector = null, List<string>? restrictedDepartments = null);
        Task<List<DepartmentWiseReportViewModel>> GetDepartmentWiseReportAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null, List<string>? restrictedDepartments = null);
        Task<List<CostCenterWiseReportViewModel>> GetCostCenterWiseReportAsync(int? financialYear = null, string? fundCode = null, string? costCenterCode = null, IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null, string? sector = null, string? departmentCode = null, List<string>? restrictedDepartments = null);
        Task<List<CostCenterGLDetailViewModel>> GetCostCenterGLDetailAsync(string costCenterCode, int? financialYear = null, string? fundCode = null, IEnumerable<string>? objectCodes = null);
        Task<List<FunctionWiseReportViewModel>> GetFunctionWiseReportAsync(int? financialYear = null, string? keyword = null, List<string>? restrictedDepartments = null);
        Task<List<ObjectWiseReportViewModel>> GetObjectWiseReportAsync(int? financialYear = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, string? sector = null, IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null);
        Task<List<ObjectWiseGLDetailViewModel>> GetObjectWiseGLDetailAsync(string detailObject, int? financialYear = null);
        Task<List<ObjectWiseGroupedViewModel>> GetObjectWiseGroupedAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null, string? costCenterCode = null, string? keyword = null, IEnumerable<string>? costCenterCodes = null, IEnumerable<string>? objectCodes = null);
        Task<List<CompleteBudgetReportViewModel>> GetCompleteBudgetReportAsync(FilterViewModel filters, int pageNumber = 1, int pageSize = 50);
        Task<List<DevBudgetReportViewModel>> GetDevBudgetReportAsync(int? financialYear = null, List<string>? parentProjects = null, List<string>? gsNos = null, List<string>? sectors = null, List<string>? restrictedDepartments = null);
        Task<List<LookupItem>> GetParentProjectLookupAsync(int? financialYear = null);
        Task<List<LookupItem>> GetGsNoLookupAsync(int? financialYear = null);
        Task<List<LookupItem>> GetDevBudgetSectorLookupAsync(int? financialYear = null);
        Task<List<BudgetComparisonViewModel>> GetBudgetComparisonAsync(int? financialYear = null);
        Task<List<CostCenterReportItemViewModel>> GetCostCenterReportAsync(int? financialYear = null, string? fundCode = null, string? costCenterCode = null);
        Task<List<LookupItem>> GetCostCenterLookupAsync(int? financialYear = null, string? fundCode = null, string? sector = null, string? departmentCode = null, List<string>? restrictedDepartments = null);
        Task<List<LookupItem>> GetObjectCodeLookupAsync(int? financialYear = null, string? sector = null, string? departmentCode = null, string? fundCode = null, IEnumerable<string>? costCenterCodes = null, List<string>? restrictedDepartments = null);

        Task<List<NineItemsReportRowViewModel>> GetNineItemsReportAsync(int? financialYear = null);
        Task<List<NineItemsReportRowViewModel>> GetDeptWiseReportAsync(int? financialYear = null, List<string>? sectors = null, List<string>? departmentCodes = null, List<string>? restrictedDepartments = null);

        // BER Update
        Task<(int rowsCopied, int failedStep, string message)> UpdateBERDataAsync();
        Task<int> UpdateBERDataStep1Async();
        Task UpdateBERDataStep2Async();
        Task UpdateBERDataStep3Async();
        Task UpdateBERDataStep4Async();
        Task UpdateBERDataStep5Async();
        Task UpdateBERDataStep6Async();
        Task UpdateBERDataStep7Async();
    }

    public class LookupItem
    {
        public string Value { get; set; } = string.Empty;
        public string Text { get; set; } = string.Empty;
    }
}
