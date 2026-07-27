using System.ComponentModel.DataAnnotations;

namespace FinanceDashboard.ViewModels
{
    public class FundWiseReportViewModel
    {
        public string FundCode        { get; set; } = string.Empty;
        public string FundDes         { get; set; } = string.Empty;   // Fund_Des
        public decimal TotalAllocatedBudget    { get; set; }          // SUM(allocated_budget)
        public decimal TotalBudgetGrant        { get; set; }          // SUM(BudgetGrant)
        public decimal TotalReleases           { get; set; }          // SUM(Releases)
        public decimal TotalFinalGrant         { get; set; }          // SUM(FinalGrant)
        public decimal TotalActualExpenditure  { get; set; }          // SUM(ActualExpenditure)
        public decimal TotalBalance            { get; set; }          // SUM(FinalGrantBalance)

        // Computed in C# to avoid SQL division issues
        public decimal ReleasePercentage   => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class DepartmentWiseReportViewModel
    {
        public string DepartmentName          { get; set; } = string.Empty;
        public decimal TotalAllocatedBudget   { get; set; }
        public decimal TotalBudgetGrant       { get; set; }
        public decimal TotalReleases          { get; set; }
        public decimal TotalFinalGrant        { get; set; }
        public decimal TotalActualExpenditure { get; set; }
        public decimal TotalBalance           { get; set; }

        public decimal ReleasePercentage     => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class CostCenterWiseReportViewModel
    {
        public string  FundCenterCode        { get; set; } = string.Empty;
        public string? CostCenterDescription { get; set; }   // FundCenterDescription alias
        public string? FundDes               { get; set; }   // Fund description
        public decimal TotalAllocatedBudget  { get; set; }   // SUM(allocated_budget)
        public decimal TotalBudgetGrant      { get; set; }   // SUM(BudgetGrant)
        public decimal TotalReleases         { get; set; }   // SUM(Releases)
        public decimal TotalFinalGrant       { get; set; }   // SUM(FinalGrant)
        public decimal TotalActualExpenditure{ get; set; }   // SUM(ActualExpenditure)
        public decimal TotalBalance          { get; set; }   // SUM(FinalGrantBalance)

        public decimal ReleasePercentage    => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class FunctionWiseReportViewModel
    {
        public string? FunctionCode { get; set; }
        public int TotalRecords { get; set; }
        public decimal TotalOriginalBudget { get; set; }
        public decimal TotalRevisedBudget { get; set; }
        public int DepartmentCount { get; set; }
        public int FundCount { get; set; }
    }

    public class ObjectWiseReportViewModel
    {
        public string  DetailObject           { get; set; } = string.Empty;
        public string? ObjectDescription      { get; set; }
        public decimal TotalAllocatedBudget   { get; set; }
        public decimal TotalBudgetGrant       { get; set; }
        public decimal TotalReleases          { get; set; }
        public decimal TotalFinalGrant        { get; set; }
        public decimal TotalActualExpenditure { get; set; }
        public decimal TotalBalance           { get; set; }

        public decimal ReleasePercentage     => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class ObjectWiseGLDetailViewModel
    {
        public string  GLCode                { get; set; } = string.Empty;
        public string? GLDescription         { get; set; }
        public decimal TotalAllocatedBudget  { get; set; }
        public decimal TotalBudgetGrant      { get; set; }
        public decimal TotalReleases         { get; set; }
        public decimal TotalFinalGrant       { get; set; }
        public decimal TotalActualExpenditure{ get; set; }
        public decimal TotalBalance          { get; set; }

        public decimal ReleasePercentage     => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class CompleteBudgetReportViewModel
    {
        public int FinancialYear { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string FundCode { get; set; } = string.Empty;
        public string? FundDescription { get; set; }
        public string CostCenterCode { get; set; } = string.Empty;
        public string CommitmentItem { get; set; } = string.Empty;
        public string? FunctionCode { get; set; }
        public decimal OriginalBudgetAmount { get; set; }
        public decimal RevisedBudgetAmount { get; set; }
        public int Posts { get; set; }
        public decimal BudgetGrant { get; set; }
        public decimal FinalGrant { get; set; }
        public decimal Releases { get; set; }
        public decimal ActualExpenditure { get; set; }
        public decimal FinalGrantBalance { get; set; }
    }

    public class CostCenterGLDetailViewModel
    {
        public string  GLCode                { get; set; } = string.Empty;   // DetailObject
        public string? GLDescription         { get; set; }                   // GL_Desc from BERData
        public decimal TotalAllocatedBudget  { get; set; }
        public decimal TotalBudgetGrant      { get; set; }
        public decimal TotalReleases         { get; set; }
        public decimal TotalFinalGrant       { get; set; }
        public decimal TotalActualExpenditure{ get; set; }
        public decimal TotalBalance          { get; set; }

        public decimal ReleasePercentage     => TotalAllocatedBudget > 0
            ? Math.Round(TotalReleases / TotalAllocatedBudget * 100, 2) : 0;
        public decimal UtilizationPercentage => TotalFinalGrant > 0
            ? Math.Round(TotalActualExpenditure / TotalFinalGrant * 100, 2) : 0;
    }

    public class CostCenterReportItemViewModel
    {
        [Display(Name = "Financial Year")]
        public int FinancialYear { get; set; }

        [Display(Name = "Department Code")]
        public string DepartmentCode { get; set; } = string.Empty;

        [Display(Name = "Fund Code")]
        public string FundCode { get; set; } = string.Empty;

        [Display(Name = "Cost Center Code")]
        public string CostCenterCode { get; set; } = string.Empty;

        [Display(Name = "Commitment Item")]
        public string CommitmentItem { get; set; } = string.Empty;

        [Display(Name = "Posts")]
        public int Posts { get; set; }

        [Display(Name = "Original Budget (PKR)")]
        public decimal OriginalBudgetAmount { get; set; }

        [Display(Name = "Revised Budget (PKR)")]
        public decimal RevisedBudgetAmount { get; set; }

        [Display(Name = "Variance (PKR)")]
        public decimal Variance => RevisedBudgetAmount - OriginalBudgetAmount;
    }
}
