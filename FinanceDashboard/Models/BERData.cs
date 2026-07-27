using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("BerSapData", Schema = "budget")]
    public class BERData
    {
        [Key]
        public long Id { get; set; }

        [Required]
        [Display(Name = "Financial Year")]
        public int FinancialYear { get; set; }

        [MaxLength(100)]
        [Display(Name = "Sector")]
        public string? Sector { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Fund Code")]
        public string FundCode { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        [Display(Name = "Fund Center Code")]
        public string FundCenterCode { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Fund Center Description")]
        public string? FundCenterDescription { get; set; }

        [MaxLength(20)]
        [Display(Name = "Ministry/Department")]
        public string? MinistryDepartment { get; set; }

        [MaxLength(200)]
        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        [MaxLength(200)]
        [Display(Name = "Attached Department")]
        public string? AttachedDepartment { get; set; }

        [MaxLength(50)]
        [Display(Name = "Profit Center")]
        public string? ProfitCenter { get; set; }

        [MaxLength(20)]
        [Display(Name = "Sub Detail Function")]
        public string? SubDetailFunction { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Detail Object")]
        public string DetailObject { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "GL Description")]
        [Column("GL_Desc")]
        public string? GL_Desc { get; set; }

        [Display(Name = "Budget Grant")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal BudgetGrant { get; set; }

        [Display(Name = "Allocated Budget")]
        [Column("allocated_budget", TypeName = "decimal(18,2)")]
        public decimal AllocatedBudget { get; set; }

        [Display(Name = "Supplementary Grant")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SupplementaryGrant { get; set; }

        [Display(Name = "Reappropriation (+)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReappropriationPlus { get; set; }

        [Display(Name = "Reappropriation (-)")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ReappropriationMinus { get; set; }

        [Display(Name = "TSS / Surrender")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal TSSSurrender { get; set; }

        [Display(Name = "Final Grant")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalGrant { get; set; }

        [Display(Name = "Releases")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Releases { get; set; }

        [Display(Name = "July Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JulExp { get; set; }

        [Display(Name = "August Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AugExp { get; set; }

        [Display(Name = "September Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal SepExp { get; set; }

        [Display(Name = "October Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal OctExp { get; set; }

        [Display(Name = "November Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal NovExp { get; set; }

        [Display(Name = "December Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DecExp { get; set; }

        [Display(Name = "January Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JanExp { get; set; }

        [Display(Name = "February Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FebExp { get; set; }

        [Display(Name = "March Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MarExp { get; set; }

        [Display(Name = "April Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal AprExp { get; set; }

        [Display(Name = "May Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal MayExp { get; set; }

        [Display(Name = "June Exp")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal JunExp { get; set; }

        [Display(Name = "Actual Expenditure")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal ActualExpenditure { get; set; }

        [Display(Name = "Final Grant Balance")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal FinalGrantBalance { get; set; }

        public long ImportId { get; set; }


    }
}
