using FinanceDashboard.ViewModels.Pivot;

namespace FinanceDashboard.Interfaces
{
    public interface IPivotExportService
    {
        /// <summary>Exports pivot result to an Excel (.xlsx) workbook.</summary>
        Task<byte[]> ExportExcelAsync(PivotResultViewModel result, PivotRequestViewModel request);

        /// <summary>Exports pivot result to a CSV byte array.</summary>
        Task<byte[]> ExportCsvAsync(PivotResultViewModel result, PivotRequestViewModel request);
    }
}
