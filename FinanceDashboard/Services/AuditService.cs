using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;

namespace FinanceDashboard.Services
{
    public class AuditService
    {
        private readonly IAuditRepository _auditRepository;

        public AuditService(IAuditRepository auditRepository)
        {
            _auditRepository = auditRepository;
        }

        public async Task LogAsync(string userId, string userName, string action, string? entityType = null, string? entityId = null, string? oldValues = null, string? newValues = null, string? ipAddress = null, string status = "Success", string? errorMessage = null)
        {
            var auditLog = new AuditLog
            {
                UserId = userId,
                UserName = userName,
                Action = action,
                EntityType = entityType,
                EntityId = entityId,
                OldValues = oldValues,
                NewValues = newValues,
                IpAddress = ipAddress,
                ActionDate = DateTime.UtcNow,
                Status = status,
                ErrorMessage = errorMessage
            };

            await _auditRepository.LogAsync(auditLog);
        }
    }
}
