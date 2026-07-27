using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    public enum AuditAction
    {
        Login,
        Logout,
        Import,
        Update,
        Delete,
        Export,
        Create,
        View,
        Error
    }

    [Table("AuditLogs", Schema = "admin")]
    public class AuditLog
    {
        [Key]
        public long Id { get; set; }

        [MaxLength(450)]
        public string? UserId { get; set; }

        [MaxLength(256)]
        public string? UserName { get; set; }

        [Required]
        [MaxLength(100)]
        public string Action { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? EntityType { get; set; }

        [MaxLength(100)]
        public string? EntityId { get; set; }

        public string? OldValues { get; set; }

        public string? NewValues { get; set; }

        [MaxLength(50)]
        public string? IpAddress { get; set; }

        [MaxLength(500)]
        public string? UserAgent { get; set; }

        public DateTime ActionDate { get; set; } = DateTime.UtcNow;

        [MaxLength(50)]
        public string Status { get; set; } = "Success";

        public string? ErrorMessage { get; set; }

        [NotMapped]
        public string ActionBadgeClass => Action switch
        {
            "Login" => "bg-success",
            "Logout" => "bg-info",
            "Import" => "bg-primary",
            "Update" => "bg-warning",
            "Delete" => "bg-danger",
            "Export" => "bg-secondary",
            "Create" => "bg-success",
            "View" => "bg-light text-dark",
            "Error" => "bg-danger",
            _ => "bg-light text-dark"
        };

        [NotMapped]
        public string ActionIcon => Action switch
        {
            "Login" => "bi-box-arrow-in-right",
            "Logout" => "bi-box-arrow-right",
            "Import" => "bi-upload",
            "Update" => "bi-pencil",
            "Delete" => "bi-trash",
            "Export" => "bi-download",
            "Create" => "bi-plus-circle",
            "View" => "bi-eye",
            "Error" => "bi-exclamation-triangle",
            _ => "bi-circle"
        };
    }
}
