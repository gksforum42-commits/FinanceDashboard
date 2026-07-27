using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("OriginalBudget", Schema = "budget")]
    public class OriginalBudget
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Financial Year")]
        public int FinancialYear { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Document Number")]
        public string DocumentNumber { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Department")]
        public string DepartmentCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Budget Type")]
        public string BudgetType { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Fund Code")]
        public string FundCode { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Fund Description")]
        public string? FundDescription { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Cost Center")]
        public string CostCenterCode { get; set; } = string.Empty;

        [MaxLength(30)]
        [Display(Name = "SNE Code")]
        public string? SNECode { get; set; }

        [Required]
        [MaxLength(30)]
        [Display(Name = "Commitment Item")]
        public string CommitmentItem { get; set; } = string.Empty;

        [MaxLength(20)]
        [Display(Name = "Sub Head")]
        public string? SubHead { get; set; }

        [MaxLength(10)]
        [Display(Name = "BPS")]
        public string? BPS { get; set; }

        [MaxLength(20)]
        [Display(Name = "Designation Code")]
        public string? DesignationCode { get; set; }

        [Display(Name = "Posts")]
        public int Posts { get; set; }

        [Display(Name = "Original Budget Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalBudgetAmount { get; set; }

        [Display(Name = "Revised Budget Amount")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal RevisedBudgetAmount { get; set; }

        [MaxLength(200)]
        [Display(Name = "Designation Description")]
        public string? DesignationDescription { get; set; }

        [MaxLength(500)]
        public string? Description { get; set; }

        [Display(Name = "Posting Date")]
        public DateTime? PostingDate { get; set; }

        [MaxLength(50)]
        [Display(Name = "User Name")]
        public string? UserName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Function")]
        public string? FunctionCode { get; set; }

        [MaxLength(10)]
        [Display(Name = "Type")]
        public string? Type { get; set; }

        [Display(Name = "Import ID")]
        public long ImportId { get; set; }

        public bool IsDeleted { get; set; } = false;

        [Display(Name = "Created Date")]
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
