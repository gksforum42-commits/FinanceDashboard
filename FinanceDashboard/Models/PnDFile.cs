using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FinanceDashboard.Models
{
    [Table("pndFile", Schema = "budget")]
    public class PnDFile
    {
        [Key]
        public int Id { get; set; }

        [MaxLength(100)]
        public string? SRNo { get; set; }

        public int? GSNo { get; set; }

        [MaxLength(40)]
        public string? SchemeNo { get; set; }

        [MaxLength(40)]
        public string? FullSchemeNo { get; set; }

        [MaxLength(100)]
        public string? LoNumberCapital { get; set; }

        [MaxLength(100)]
        public string? LoNumberRevenue { get; set; }

        [MaxLength(2000)]
        public string? Name { get; set; }

        [MaxLength(400)]
        public string? District { get; set; }

        [MaxLength(1000)]
        public string? DistrictName { get; set; }

        public string? Scope { get; set; }

        public string? Objectives { get; set; }

        public string? IssueRemarks { get; set; }

        [MaxLength(200)]
        public string? GestationPeriod { get; set; }

        [MaxLength(400)]
        public string? ApprovalStatus { get; set; }

        public decimal? InitialCost { get; set; }

        public decimal? Cost { get; set; }

        public decimal? ExpenseUptoJune { get; set; }

        public decimal? ActualExpenditureUptoJune2026 { get; set; }

        public decimal? LocalSouthPunjab { get; set; }

        public decimal? ForeignSouthPunjab { get; set; }

        public decimal? AllocationSouthPunjab { get; set; }

        public decimal? LocalAllocation { get; set; }

        public decimal? ForeignAllocation { get; set; }

        public decimal? Allocation { get; set; }

        public decimal? LocalRevisedAllocation { get; set; }

        public decimal? ForeignRevisedAllocation { get; set; }

        public decimal? RevisedAllocation { get; set; }

        public decimal? LocalPDDRelease { get; set; }

        public decimal? ForeignPDDRelease { get; set; }

        public decimal? ReleaseByPDD { get; set; }

        public decimal? ReleaseReceivedByAD { get; set; }

        public decimal? FDRelease { get; set; }

        public decimal? SpendingRelease { get; set; }

        public decimal? UtilizationPifra { get; set; }

        public decimal? TotalThrowForward { get; set; }

        public decimal? Utilization { get; set; }

        public decimal? PctUtilCont { get; set; }

        public decimal? PctUtilRevAlloc { get; set; }

        public int? SectorID { get; set; }

        public int? SectorOrder { get; set; }

        [MaxLength(400)]
        public string? SectorName { get; set; }

        [MaxLength(400)]
        public string? SchemeType { get; set; }

        [MaxLength(400)]
        public string? SchemeSubType { get; set; }

        [MaxLength(200)]
        public string? AcToBeCredited { get; set; }

        [MaxLength(400)]
        public string? ProjectStatusType { get; set; }

        [MaxLength(100)]
        public string? ApprovalForum { get; set; }

        [MaxLength(20)]
        public string? AA { get; set; }

        [MaxLength(20)]
        public string? TS { get; set; }

        [MaxLength(2000)]
        public string? ExecutingAgency { get; set; }

        [MaxLength(1000)]
        public string? SponsoringAgency { get; set; }

        [MaxLength(20)]
        public string? IsSNERequired { get; set; }

        [MaxLength(20)]
        public string? IsPCI { get; set; }

        [MaxLength(400)]
        public string? AttachDepartment { get; set; }

        [MaxLength(400)]
        public string? AdminDepartment { get; set; }

        [MaxLength(20)]
        public string? IsMOM { get; set; }

        [MaxLength(20)]
        public string? IsAA { get; set; }

        public int? ProjectID { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
