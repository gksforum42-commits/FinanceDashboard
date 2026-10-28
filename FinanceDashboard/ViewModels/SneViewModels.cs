namespace FinanceDashboard.ViewModels
{
    public class SneLandingViewModel
    {
        public string DepartmentCode { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
        public int FinancialYear { get; set; }

        public int TotalCostCenters { get; set; }
        public int CreatedCount { get; set; }   // Draft
        public int PendingReviewCount { get; set; } // SubmittedBySNE - submitted by SNE Dept User, awaiting Budget Officer review
        public int SubmittedCount { get; set; } // Submitted - final Budget Officer submission
        public int NotCreatedCount { get; set; }

        // --- Summary totals ---
        public long TotalPostsPriorYear { get; set; }      // Total No Posts Year 2026-27 (from budget.OriginalBudget)
        public long TotalPostsCurrentYear { get; set; }    // Total No Posts 2027-28 (from sne.snep)
        public decimal TotalOriginalBudget { get; set; }   // Total Budget 2026-27 (from budget.OriginalBudget)
        public decimal TotalProposedBudget { get; set; }   // Proposed Budget 2027-28 (from sne.snep)

        public List<SneCostCenterRowViewModel> CostCenters { get; set; } = new();

        // --- Admin / multi-department view support ---

        /// <summary>
        /// True when the current user is an Administrator viewing the SNE dashboard.
        /// Enables the "All Departments" summary/filter UI instead of the single-department view.
        /// </summary>
        public bool IsAdminView { get; set; }

        /// <summary>
        /// True when the current user has been granted access to more than one SNE department
        /// (e.g. a "Budget Officer" who is both DepartmentUser and SNEDepartmentUser).
        /// </summary>
        public bool IsMultiDepartmentView { get; set; }

        /// <summary>
        /// True when the current user is a plain SNE Department User (no DepartmentUser role).
        /// Used by the view to label cards/columns from the SNE user's own perspective
        /// (e.g. "Submitted" means "Submitted by me, pending review" rather than "Finally Submitted").
        /// </summary>
        public bool IsPlainSneUserView { get; set; }

        /// <summary>
        /// The list of department codes the current user is allowed to see/filter on
        /// (all departments for Administrator, or the assigned subset for a multi-department SNE user).
        /// </summary>
        public List<SneDepartmentOptionViewModel> AvailableDepartments { get; set; } = new();

        /// <summary>
        /// Per-department breakdown, populated for the Administrator / multi-department "All Departments" view.
        /// </summary>
        public List<SneDepartmentSummaryRowViewModel> DepartmentSummaries { get; set; } = new();
    }

    public class SneDepartmentOptionViewModel
    {
        public string DepartmentCode { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }
    }

    public class SneCostCenterRowViewModel
    {
        public string CostCenterCode { get; set; } = string.Empty;
        public string? CostCenterDesc { get; set; }

        // Status: NotCreated | Draft | Submitted
        public string Status { get; set; } = "NotCreated";

        /// <summary>
        /// True only when an Administrator reverted a finally-Submitted header back to Draft.
        /// The plain SNE Department User's dashboard keeps showing this row as "Submitted" in that
        /// case (they are locked out and should not see it flip back to Draft/actionable).
        /// </summary>
        public bool SneUserLocked { get; set; }

        public int? HeaderId { get; set; }
        public DateTime? SubmittedDate { get; set; }
    }

    /// <summary>
    /// Per-department summary row used in the Administrator "All Departments" SNE landing view.
    /// </summary>
    public class SneDepartmentSummaryRowViewModel
    {
        public string DepartmentCode { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }

        public int TotalCostCenters { get; set; }
        public int CreatedCount { get; set; }   // Draft
        public int PendingReviewCount { get; set; } // SubmittedBySNE
        public int SubmittedCount { get; set; } // Submitted (final)
        public int NotCreatedCount { get; set; }

        public long TotalPostsPriorYear { get; set; }
        public long TotalPostsCurrentYear { get; set; }
        public decimal TotalOriginalBudget { get; set; }
        public decimal TotalProposedBudget { get; set; }
    }

    public class SneFormViewModel
    {
        public int? HeaderId { get; set; }
        public int FinancialYear { get; set; }

        public string DepartmentCode { get; set; } = string.Empty;
        public string? DepartmentName { get; set; }

        public string CostCenterCode { get; set; } = string.Empty;
        public string? CostCenterDesc { get; set; }

        public string? FundCode { get; set; }
        public string? FundDescription { get; set; }

        // Draft | SubmittedBySNE | Submitted
        public const string StatusDraft = "Draft";
        public const string StatusSubmittedBySne = "SubmittedBySNE";
        public const string StatusSubmitted = "Submitted";

        public string Status { get; set; } = StatusDraft;

        /// <summary>
        /// True only when an Administrator has reverted a finally-Submitted header back to Draft.
        /// Per the business rule, once that happens, only the Budget Officer/Administrator may edit
        /// or submit it again — the plain SNE Department User stays locked out. A Budget-Officer
        /// revert of a SubmittedBySNE (pending review) header does NOT set this flag, so the SNE
        /// Department User keeps normal access to that Draft.
        /// </summary>
        public bool SneUserLocked { get; set; }

        /// <summary>
        /// Computed by the controller based on the current user's role and the header's status.
        /// True when the current user is not allowed to edit/save/submit this form.
        /// </summary>
        public bool IsReadOnly { get; set; }

        /// <summary>
        /// True when the current user (DepartmentUser/Administrator) is allowed to revert this
        /// form back to Draft for further edits, even after it has been submitted.
        /// </summary>
        public bool CanRevert { get; set; }

        public List<SneLineItemViewModel> Lines { get; set; } = new();
    }

    public class SneLineItemViewModel
    {
        public int? Id { get; set; }

        // Fetched from OriginalBudget/GL_Codes - read-only when fetched from source (IsFetched = true)
        public bool IsFetched { get; set; }

        public string? GlCode { get; set; }
        public string? GlDesc { get; set; }
        public string? SubHeadCode { get; set; }
        public string? PostCode { get; set; }
        public string? Designation { get; set; }
        public string? BS { get; set; }

        public long? PostPrevious { get; set; }   // No of Posts 2026-27 (fetched)
        public long? PostCurrent { get; set; }    // No of Posts 2027-28 (user input)

        public long? ActualExpLast8Previous { get; set; }
        public long? ActualLast4Current { get; set; }
        public long? TotalEstimatedExpCurrent { get; set; }
        public long? BudgetEstimateCurrent { get; set; }

        public long? ProposedBudget1 { get; set; } // Proposed Budget 2027-28
        public long? ProposedBudget2 { get; set; } // Budget Estimate 2028-29
        public long? ProposedBudget3 { get; set; } // Budget Estimate 2029-30

        public string? Justification { get; set; }
    }

    public class SneSaveRequest
    {
        public int? HeaderId { get; set; }
        public string CostCenterCode { get; set; } = string.Empty;
        public int FinancialYear { get; set; }
        public string? FundCode { get; set; }
        public bool IsSubmit { get; set; }
        public List<SneLineItemViewModel> Lines { get; set; } = new();
    }

    public class SneRevertRequest
    {
        public int HeaderId { get; set; }
        public string? CostCenterCode { get; set; }
        public int FinancialYear { get; set; }
    }

    /// <summary>
    /// Result of a GL Code lookup against budget.GL_Codes (+ budget.OriginalBudget for Sub Head),
    /// used to auto-fill the GL Description and Sub Head fields when a user types a GL Code
    /// into a newly added line item row.
    /// </summary>
    public class SneGlCodeLookupViewModel
    {
        public string GlCode { get; set; } = string.Empty;
        public string? GlDesc { get; set; }
        public string? SubHeadCode { get; set; }
    }
}
