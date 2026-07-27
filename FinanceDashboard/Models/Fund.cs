using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("Funds", Schema = "budget")]
    public class Fund
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Fund Code")]
        public string FundCode { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Fund Description")]
        public string? FundDescription { get; set; }

        [MaxLength(50)]
        [Display(Name = "Fund Type")]
        public string? FundType { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
