using Dapper;
using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace FinanceDashboard.Repositories
{
    public class AuditRepository : IAuditRepository
    {
        private readonly ApplicationDbContext _context;
        private readonly string _connectionString;

        public AuditRepository(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
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
            using var connection = new SqlConnection(_connectionString);
            var parameters = new DynamicParameters();
            parameters.Add("@UserId", userId, DbType.String);
            parameters.Add("@Action", action, DbType.String);
            parameters.Add("@FromDate", fromDate, DbType.DateTime2);
            parameters.Add("@ToDate", toDate, DbType.DateTime2);
            parameters.Add("@PageNumber", pageNumber, DbType.Int32);
            parameters.Add("@PageSize", pageSize, DbType.Int32);

            var result = await connection.QueryAsync<AuditLog>(
                "admin.sp_GetAuditLogs",
                parameters,
                commandType: CommandType.StoredProcedure);

            return result.ToList();
        }

        public async Task<int> GetAuditLogsCountAsync(string? userId = null, string? action = null, DateTime? fromDate = null, DateTime? toDate = null)
        {
            return await _context.AuditLogs
                .AsNoTracking()
                .Where(x =>
                    (userId == null || x.UserId == userId) &&
                    (action == null || x.Action == action) &&
                    (fromDate == null || x.ActionDate >= fromDate) &&
                    (toDate == null || x.ActionDate <= toDate))
                .CountAsync();
        }
    }
}
