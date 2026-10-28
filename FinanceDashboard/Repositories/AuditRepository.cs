using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using Microsoft.EntityFrameworkCore;

namespace FinanceDashboard.Repositories
{
    public class AuditRepository : IAuditRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<AuditRepository> _logger;

        public AuditRepository(ApplicationDbContext context, ILogger<AuditRepository> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task LogAsync(AuditLog auditLog)
        {
            try
            {
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
            }
            catch
            {
                // Silently fail audit logging to prevent application errors
            }
        }

        public async Task<List<AuditLog>> GetAuditLogsAsync(string? userId = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null, int pageNumber = 1, int pageSize = 50)
        {
            var query = _context.AuditLogs.AsNoTracking();

            // Filter by UserId if provided
            if (!string.IsNullOrEmpty(userId))
            {
                query = query.Where(x => x.UserId == userId);
            }

            // Filter by Action if provided
            if (!string.IsNullOrEmpty(action))
            {
                query = query.Where(x => x.Action == action);
            }

            // Filter by FromDate if provided
            if (fromDate.HasValue)
            {
                query = query.Where(x => x.ActionDate >= fromDate.Value);
            }

            // Filter by ToDate if provided - include entire end day
            if (toDate.HasValue)
            {
                var toDateEnd = toDate.Value.AddDays(1);
                query = query.Where(x => x.ActionDate < toDateEnd);
            }

            var result = await query
                .OrderByDescending(x => x.ActionDate)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return result;
        }

        public async Task<int> GetAuditLogsCountAsync(string? userId = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var query = _context.AuditLogs.AsNoTracking();

            if (!string.IsNullOrEmpty(userId))
                query = query.Where(x => x.UserId == userId);

            if (!string.IsNullOrEmpty(action))
                query = query.Where(x => x.Action == action);

            if (fromDate.HasValue)
                query = query.Where(x => x.ActionDate >= fromDate.Value);

            if (toDate.HasValue)
            {
                var toDateEnd = toDate.Value.AddDays(1);
                query = query.Where(x => x.ActionDate < toDateEnd);
            }

            return await query.CountAsync();
        }
    }
}
