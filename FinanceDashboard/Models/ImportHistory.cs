using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    public enum ImportStatus
    {
        Pending,
        Processing,
        Completed,
        Failed,
        Deleted
    }

    public enum ImportType
    {
        OriginalBudget,
        BER,
        Both
    }

    [Table("ImportHistory", Schema = "budget")]
    public class ImportHistory
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "File Name")]
        public string FileName { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        [Display(Name = "Original File Name")]
        public string OriginalFileName { get; set; } = string.Empty;

        [Display(Name = "File Size (bytes)")]
        public long FileSize { get; set; }

        [MaxLength(1000)]
        [Display(Name = "File Path")]
        public string? FilePath { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Import Type")]
        public string ImportType { get; set; } = string.Empty;

        [MaxLength(100)]
        [Display(Name = "Sheet Name")]
        public string? SheetName { get; set; }

        [Display(Name = "Records Imported")]
        public int RecordsImported { get; set; }

        [Display(Name = "Original Budget Count")]
        public int OriginalBudgetCount { get; set; }

        [Display(Name = "BER Count")]
        public int BERCount { get; set; }

        [Display(Name = "Validation Errors")]
        public int ValidationErrors { get; set; }

        [Required]
        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        [Display(Name = "Error Message")]
        public string? ErrorMessage { get; set; }

        [Display(Name = "Financial Year")]
        public int FinancialYear { get; set; }

        [Required]
        [MaxLength(450)]
        [Display(Name = "Imported By")]
        public string ImportedBy { get; set; } = string.Empty;

        [Display(Name = "Imported Date")]
        public DateTime ImportedDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "Completed Date")]
        public DateTime? CompletedDate { get; set; }

        public bool IsDeleted { get; set; } = false;

        [NotMapped]
        public string FileSizeFormatted => FileSize > 0 
            ? FileSize < 1024 * 1024 
                ? $"{FileSize / 1024.0:F2} KB" 
                : $"{FileSize / (1024.0 * 1024.0):F2} MB"
            : "0 KB";

        [NotMapped]
        public string StatusBadgeClass => Status switch
        {
            "Completed" => "bg-success",
            "Processing" => "bg-warning",
            "Failed" => "bg-danger",
            "Deleted" => "bg-secondary",
            _ => "bg-info"
        };
    }
}
