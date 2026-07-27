using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface IPdfService
    {
        Task<byte[]> GenerateReportPdfAsync(string reportType, FilterViewModel filters, List<string> columns, string title);
        Task<byte[]> GenerateOriginalBudgetPdfAsync(FilterViewModel filters);
        Task<byte[]> GenerateBERPdfAsync(FilterViewModel filters);
        Task<byte[]> GenerateCostCenterReportPdfAsync(int? financialYear, string? fundCode, string? costCenterCode);
    }
}
