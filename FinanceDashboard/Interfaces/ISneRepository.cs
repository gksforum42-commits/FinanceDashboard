using FinanceDashboard.ViewModels;

namespace FinanceDashboard.Interfaces
{
    public interface ISneRepository
    {
        Task<SneLandingViewModel> GetDepartmentLandingAsync(string departmentCode, int financialYear);

        /// <summary>
        /// Loads the SNE landing dashboard scoped to a set of departments (or all departments when
        /// <paramref name="departmentCodes"/> is null/empty). Used by Administrators (all departments,
        /// with an optional single-department filter) and multi-department "Budget Officer" users
        /// (restricted to their assigned departments).
        /// </summary>
        Task<SneLandingViewModel> GetMultiDepartmentLandingAsync(List<string>? departmentCodes, string? filterDepartmentCode, int financialYear, bool isAdminView);

        Task<SneFormViewModel?> GetCreateOrEditDataAsync(List<string> allowedDepartmentCodes, string costCenterCode, int financialYear);

        /// <summary>
        /// Looks up a GL Code in budget.GL_Codes and returns its description plus the most
        /// commonly associated Sub Head (from budget.OriginalBudget), for auto-filling a newly
        /// added SNE line item row. Returns null if the GL Code does not exist.
        /// </summary>
        Task<SneGlCodeLookupViewModel?> GetGlCodeInfoAsync(string glCode);

        /// <summary>
        /// Saves the SNE draft/submission. <paramref name="isDepartmentSubmitter"/> distinguishes
        /// between an SNEDepartmentUser submission (sets Status = SubmittedBySNE, pending department
        /// review) and a DepartmentUser/Administrator submission (sets Status = Submitted, final).
        /// Both submission events are recorded independently (SneSubmittedBy/Date and
        /// DeptSubmittedBy/Date) so neither record is overwritten by the other.
        /// </summary>
        Task<int> SaveSneAsync(SneSaveRequest request, string? userId, bool isDepartmentSubmitter);

        /// <summary>
        /// Reverts a submitted/pending-review header back to Draft so it can be edited again.
        /// Restricted to DepartmentUser/Administrator callers (enforced by the controller).
        /// <paramref name="lockOutSneUser"/> should be true only for an Administrator reverting a
        /// finally-Submitted form (locks the plain SNE Department User out of the new Draft, leaving
        /// it to the Budget Officer/Administrator); false for a Budget Officer reverting a
        /// SubmittedBySNE (pending review) form (keeps it open to the SNE Department User).
        /// </summary>
        Task RevertSneAsync(int headerId, string? userId, bool lockOutSneUser);

        /// <summary>
        /// Returns the current Status of a SnepHeader row, or null if it does not exist.
        /// Used by the controller to authorize Revert (only Administrators may revert a finally
        /// Submitted form; DepartmentUser/Budget Officer may only revert while SubmittedBySNE).
        /// </summary>
        Task<string?> GetHeaderStatusAsync(int headerId);

        /// <summary>
        /// Returns the immutable snapshot of line items the plain SNE Department User submitted
        /// (captured at the moment of their SubmittedBySNE submission), from sne.snep_SneSnapshot.
        /// Used so the SNE user's own read-only view always shows exactly what they submitted,
        /// even after the Budget Officer edits and re-saves the live sne.snep data.
        /// Returns an empty list if no snapshot exists for the header.
        /// </summary>
        Task<List<SneLineItemViewModel>> GetSneSnapshotLinesAsync(int headerId);

        /// <summary>
        /// Builds an .xlsx workbook containing the current line items for a cost center/financial
        /// year, for download/offline editing. Reuses GetCreateOrEditDataAsync so the export always
        /// reflects the same data the on-screen grid shows.
        /// </summary>
        Task<(byte[] content, string fileName)?> ExportCostCenterExcelAsync(List<string> allowedDepartmentCodes, string costCenterCode, int financialYear);

        /// <summary>
        /// Parses an uploaded .xlsx file (produced by ExportCostCenterExcelAsync or filled in manually
        /// using the same column headers) and validates every row: GL Code must exist in
        /// budget.GL_Codes (auto-filling GL Description/Sub Head), and numeric fields must parse.
        /// Also validates that the workbook's "Cost Center Code" and "Fund Code" header-info rows
        /// (rows 1-4) match <paramref name="expectedCostCenterCode"/> and <paramref name="expectedFundCode"/>
        /// so a file exported for a different cost center/fund cannot be imported here by mistake.
        /// Returns the validated line items plus any row-level validation errors.
        /// </summary>
        Task<(List<SneLineItemViewModel> lines, List<string> errors)> ImportCostCenterExcelAsync(Stream fileStream, string expectedCostCenterCode, string? expectedFundCode);
    }
}
