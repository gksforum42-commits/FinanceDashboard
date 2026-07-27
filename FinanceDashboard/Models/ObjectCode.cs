using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("ObjectCodes", Schema = "budget")]
    public class ObjectCode
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [MaxLength(20)]
        [Display(Name = "Object Code")]
        public string ObjectCodeValue { get; set; } = string.Empty;

        [MaxLength(500)]
        [Display(Name = "Object Name")]
        public string? ObjectName { get; set; }

        [MaxLength(20)]
        [Display(Name = "Major Head")]
        public string? MajorHead { get; set; }

        [MaxLength(20)]
        [Display(Name = "Minor Head")]
        public string? MinorHead { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
