using FinanceDashboard.Models;
using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface IExcelService
    {
        Task<ImportPreviewViewModel> PreviewExcelAsync(IFormFile file, string importType);
        Task<(int originalCount, int berCount, List<string> errors)> ImportExcelAsync(IFormFile file, int financialYear, string importType, long importId);

        /// <summary>
        /// Processes one bounded chunk of rows from a sheet already saved to disk and returns
        /// whether more chunks remain. Used by the client-driven chunked import flow: each HTTP
        /// request does a small bounded amount of work and returns, so nothing depends on a
        /// detached background thread surviving after the request ends — which is what silently
        /// died on constrained shared hosting (e.g. MonsterASP has no "Always On"/keep-alive,
        /// so fire-and-forget Task.Run background imports get killed once the request completes).
        /// </summary>
        Task<(int inserted, int totalRows, bool done)> ImportChunkAsync(
            string filePath, string importType, string sheetKind, int financialYear, long importId, int startRow, int chunkSize,
            Func<long, string, OfficeOpenXml.ExcelPackage> getOrOpenWorkbook);

        /// <summary>
        /// Imports a PnD Excel file: always reads the FIRST worksheet (regardless of its name),
        /// maps columns by header name (row 1), truncates budget.pndFile, then bulk inserts.
        /// </summary>
        Task<int> ImportPnDFileAsync(IFormFile file);

        Task<byte[]> ExportToExcelAsync<T>(List<T> data, string sheetName);
        Task<byte[]> ExportOriginalBudgetAsync(FilterViewModel filters);
        Task<byte[]> ExportBERDataAsync(FilterViewModel filters);
        Task<byte[]> ExportReportAsync(string reportType, FilterViewModel filters);
        Task<byte[]> ExportCostCenterReportAsync(int? financialYear, string? fundCode, string? costCenterCode);
        Task<byte[]> ExportDepartmentWiseFullAsync(FilterViewModel filters);
        Task<byte[]> ExportDevBudgetExcelAsync(int? financialYear, List<string>? parentProjects, List<string>? gsNos = null, List<string>? sectors = null);
    }
}
