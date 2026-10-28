using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("pdSec", Schema = "budget")]
    public class PdSec
    {
        public int? GSNo { get; set; }
        public int? SectorID { get; set; }
        public string? SectorName { get; set; }
        public string? Project { get; set; }
        public string? sectorDesc { get; set; }
    }
}
