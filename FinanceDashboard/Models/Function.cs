using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("Functions", Schema = "budget")]
    public class Function
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Function Code")]
        public string FunctionCode { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Function Name")]
        public string? FunctionName { get; set; }

        [MaxLength(100)]
        [Display(Name = "Function Category")]
        public string? FunctionCategory { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
