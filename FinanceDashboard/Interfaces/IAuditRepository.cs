using FinanceDashboard.Models;

namespace FinanceDashboard.Interfaces
{
    public interface IAuditRepository
    {
        Task LogAsync(AuditLog auditLog);
        Task<List<AuditLog>> GetAuditLogsAsync(string? userId = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 50);
        Task<int> GetAuditLogsCountAsync(string? userId = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null);
    }
}
