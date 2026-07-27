using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("Departments", Schema = "budget")]
    public class Department
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Department Code")]
        public string DepartmentCode { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Department Name")]
        public string? DepartmentName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Ministry Code")]
        public string? MinistryCode { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
