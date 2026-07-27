namespace FinanceDashboard.ViewModels
{
    // ── 9-Items Expenditure Summary Report ───────────────────────────────────
    public class NineItemsReportRowViewModel
    {
        public string RowType { get; set; } = string.Empty;   // "sector","item","pfc_sub","service_delivery","grand_total"
        public string Description { get; set; } = string.Empty;
        public decimal AllocatedBudget { get; set; }
        public decimal OriginalBudgetReleased { get; set; }   // = Releases
        public decimal SupplementaryBudget { get; set; }
        public decimal ReAppPlus { get; set; }                 // ReappropriationPlus
        public decimal ReAppMinus { get; set; }                // ReappropriationMinus
        public decimal BudgetSurrender { get; set; }           // TSSSurrender
        public decimal FinalBudget { get; set; }               // FinalGrant
        public decimal TotalActualExpenditure { get; set; }
    }

    public class DashboardSummaryViewModel
    {
        // 4 sector tiles — Allocated Budget (FinalGrant) per sector
        public decimal NonDevelopmentBudget { get; set; }
        public decimal CapitalAc1Budget { get; set; }
        public decimal CapitalAc2Budget { get; set; }
        public decimal DevelopmentBudget { get; set; }

        // Supporting stats
        public long TotalBERRecords { get; set; }

        // Derived total across all sectors
        public decimal TotalFinalGrant => NonDevelopmentBudget + CapitalAc1Budget + CapitalAc2Budget + DevelopmentBudget;
    }

    public class ChartDataViewModel
    {
        public string[] Labels { get; set; } = Array.Empty<string>();
        public decimal[] Data { get; set; } = Array.Empty<decimal>();
        public string[] BackgroundColors { get; set; } = Array.Empty<string>();
    }

    public class MonthlyExpenditureViewModel
    {
        public string Month { get; set; } = string.Empty;
        public int MonthOrder { get; set; }
        public decimal Amount { get; set; }
    }

    public class BudgetComparisonViewModel
    {
        public int FinancialYear { get; set; }
        public string DepartmentCode { get; set; } = string.Empty;
        public string FundCode { get; set; } = string.Empty;
        public decimal OriginalBudget { get; set; }
        public decimal FinalGrant { get; set; }
        public decimal ActualExpenditure { get; set; }
        public decimal Balance { get; set; }
        public decimal ExpenditurePercentage { get; set; }
    }
}
