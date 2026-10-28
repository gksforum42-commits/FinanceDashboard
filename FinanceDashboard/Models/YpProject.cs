using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("ypProject", Schema = "budget")]
    public class YpProject
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Fiscal Year")]
        public int FiscalYear { get; set; }

        [MaxLength(50)]
        [Display(Name = "Parent Project")]
        public string? ParentProject { get; set; }

        [Required]
        [MaxLength(50)]
        [Display(Name = "Project ID")]
        public string ProjectID { get; set; } = string.Empty;

        [Required]
        [MaxLength(50)]
        [Display(Name = "WBS Element")]
        public string WBSElement { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Profit Center")]
        public string ProfitCenter { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Cost Center")]
        public string CostCenter { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Ministry/Department")]
        public string MinistryDept { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Attached Department")]
        public string AttachedDept { get; set; } = string.Empty;

        [Required]
        [Display(Name = "Detail Function Code")]
        public int DetailFunctionCode { get; set; }

        [Display(Name = "Original Budget")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OriginalBudget { get; set; }

        [Display(Name = "Supplementary Budget")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplementaryBudget { get; set; }

        [Display(Name = "Surrender")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Surrender { get; set; }

        [Display(Name = "Reapp (+)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReappPlus { get; set; }

        [Display(Name = "Reapp (-)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReappMinus { get; set; }

        [Display(Name = "Final Budget")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalBudget { get; set; }

        [Display(Name = "Progressive Releases")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProgReleases { get; set; }

        [Display(Name = "Interim Date Expenditure")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal InterimDateExp { get; set; }

        [Display(Name = "Progressive Expenditure")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ProgressiveExp { get; set; }

        [Display(Name = "Balance")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Balance { get; set; }

        [Required]
        [MaxLength(10)]
        [Display(Name = "Sector ID")]
        public string SectorID { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Sub Sector")]
        public string SubSect { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        [Display(Name = "Fund")]
        public string Fund { get; set; } = string.Empty;

        [Display(Name = "July Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JulyExp { get; set; }

        [Display(Name = "August Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AugustExp { get; set; }

        [Display(Name = "September Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SeptemberExp { get; set; }

        [Display(Name = "October Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OctoberExp { get; set; }

        [Display(Name = "November Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NovemberExp { get; set; }

        [Display(Name = "December Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DecemberExp { get; set; }

        [Display(Name = "January Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JanuaryExp { get; set; }

        [Display(Name = "February Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FebruaryExp { get; set; }

        [Display(Name = "March Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MarchExp { get; set; }

        [Display(Name = "April Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AprilExp { get; set; }

        [Display(Name = "May Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MayExp { get; set; }

        [Display(Name = "June Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JuneExp { get; set; }

        [Required]
        [MaxLength(500)]
        [Display(Name = "Project Description")]
        public string ProjectDescription { get; set; } = string.Empty;
    }
}
