using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface IExcelService
    {
        Task<ImportPreviewViewModel> PreviewExcelAsync(IFormFile file, string importType);
        Task<(int originalCount, int berCount, List<string> errors)> ImportExcelAsync(IFormFile file, int financialYear, string importType, long importId);
        Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName);
        Task<byte[]> ExportOriginalBudgetAsync(FilterViewModel filters);
        Task<byte[]> ExportBERDataAsync(FilterViewModel filters);
        Task<byte[]> ExportReportAsync(string reportType, FilterViewModel filters);
        Task<byte[]> ExportCostCenterReportAsync(int? financialYear, string? fundCode, string? costCenterCode);
        Task<byte[]> ExportDepartmentWiseFullAsync(FilterViewModel filters);
    }
}
