using Dapper;
using FinanceDashboard.Interfaces;
using FinanceDashboard.ViewModels;
using Microsoft.Data.SqlClient;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;

namespace FinanceDashboard.Repositories
{
    public class SneRepository : ISneRepository
    {
        private readonly string _connectionString;

        public SneRepository(IConfiguration configuration)
        {
            _connectionString = configuration.GetConnectionString("DefaultConnection")!;
        }

        private SqlConnection CreateConnection() => new SqlConnection(_connectionString);

        public async Task<SneLandingViewModel> GetDepartmentLandingAsync(string departmentCode, int financialYear)
        {
            using var connection = CreateConnection();

            var deptSql = @"SELECT TOP 1 DepartmentName FROM budget.Departments WHERE DepartmentCode = @DepartmentCode";
            var departmentName = await connection.QueryFirstOrDefaultAsync<string>(deptSql, new { DepartmentCode = departmentCode });

            var sql = @"
                SELECT
                    ccm.CostCenter        AS CostCenterCode,
                    ccm.CostCenterDescription AS CostCenterDesc,
                    h.Id                   AS HeaderId,
                    h.Status               AS Status,
                    h.SubmittedDate        AS SubmittedDate,
                    h.SneUserLocked        AS SneUserLocked
                FROM budget.CostCentersMapping ccm
                LEFT JOIN sne.SnepHeader h
                    ON h.CostCenterCode = ccm.CostCenter
                    AND h.FinancialYear = @FinancialYear
                WHERE ccm.DepartmentCode = @DepartmentCode
                    AND EXISTS (
                        SELECT 1 FROM budget.OriginalBudget ob WITH (NOLOCK)
                        WHERE ob.CostCenterCode = ccm.CostCenter
                            AND ob.BudgetType = 'SNEP'
                            AND LEN(ob.CostCenterCode) = 8
                            AND (ob.FundCode LIKE 'PC21%' OR ob.FundCode LIKE 'PC24%')
                            AND ob.FinancialYear = @PriorYear
                    )
                ORDER BY ccm.CostCenter";

            var rows = (await connection.QueryAsync<SneCostCenterRowViewModel>(sql, new
            {
                DepartmentCode = departmentCode,
                FinancialYear = financialYear,
                PriorYear = financialYear - 1
            }, commandTimeout: 120)).ToList();

            foreach (var row in rows)
            {
                row.Status = row.HeaderId == null ? "NotCreated" : (row.Status ?? "Draft");
            }

            var originalBudgetSql = @"
                SELECT
                    ISNULL(SUM(ob.Posts), 0)               AS TotalPosts,
                    ISNULL(SUM(ob.OriginalBudgetAmount), 0) AS TotalOriginalBudget
                FROM budget.OriginalBudget ob WITH (NOLOCK)
                WHERE ob.BudgetType = 'SNEP'
                    AND LEN(ob.CostCenterCode) = 8
                    AND (ob.FundCode LIKE 'PC21%' OR ob.FundCode LIKE 'PC24%')
                    AND ob.FinancialYear = @PriorYear
                    AND ob.CostCenterCode IN (SELECT CostCenter FROM budget.CostCentersMapping WITH (NOLOCK) WHERE DepartmentCode = @DepartmentCode)";

            var originalBudgetTotals = await connection.QueryFirstOrDefaultAsync(originalBudgetSql, new
            {
                DepartmentCode = departmentCode,
                PriorYear = financialYear - 1
            }, commandTimeout: 120);

            var totalsSql = @"
                SELECT
                    ISNULL(SUM(s.PostCurrent), 0)       AS TotalPosts,
                    ISNULL(SUM(s.ProposedBudget1), 0)   AS TotalProposedBudget
                FROM sne.snep s WITH (NOLOCK)
                INNER JOIN sne.SnepHeader h WITH (NOLOCK) ON h.Id = s.HeaderId
                WHERE h.CostCenterCode IN (SELECT CostCenter FROM budget.CostCentersMapping WITH (NOLOCK) WHERE DepartmentCode = @DepartmentCode)
                    AND h.FinancialYear = @FinancialYear";

            var totals = await connection.QueryFirstOrDefaultAsync(totalsSql, new
            {
                DepartmentCode = departmentCode,
                FinancialYear = financialYear
            }, commandTimeout: 120);

            var result = new SneLandingViewModel
            {
                DepartmentCode = departmentCode,
                DepartmentName = departmentName,
                FinancialYear = financialYear,
                CostCenters = rows,
                TotalCostCenters = rows.Count,
                CreatedCount = rows.Count(r => r.Status == "Draft"),
                PendingReviewCount = rows.Count(r => r.Status == "SubmittedBySNE"),
                SubmittedCount = rows.Count(r => r.Status == "Submitted"),
                NotCreatedCount = rows.Count(r => r.Status == "NotCreated"),
                TotalPostsPriorYear = originalBudgetTotals?.TotalPosts ?? 0,
                TotalPostsCurrentYear = totals?.TotalPosts ?? 0,
                TotalOriginalBudget = originalBudgetTotals?.TotalOriginalBudget ?? 0,
                TotalProposedBudget = totals?.TotalProposedBudget ?? 0
            };

            return result;
        }

        public async Task<SneLandingViewModel> GetMultiDepartmentLandingAsync(List<string>? departmentCodes, string? filterDepartmentCode, int financialYear, bool isAdminView)
        {
            using var connection = CreateConnection();

            // Available departments for the filter dropdown: all departments for Admin,
            // or only the assigned subset for a multi-department (Budget Officer) user.
            List<SneDepartmentOptionViewModel> availableDepartments;
            if (departmentCodes == null || departmentCodes.Count == 0)
            {
                availableDepartments = (await connection.QueryAsync<SneDepartmentOptionViewModel>(
                    "SELECT DepartmentCode, DepartmentName FROM budget.Departments ORDER BY DepartmentName", commandTimeout: 120)).ToList();
            }
            else
            {
                availableDepartments = (await connection.QueryAsync<SneDepartmentOptionViewModel>(
                    "SELECT DepartmentCode, DepartmentName FROM budget.Departments WHERE DepartmentCode IN @Codes ORDER BY DepartmentName",
                    new { Codes = departmentCodes }, commandTimeout: 120)).ToList();
            }

            // If a specific department filter was requested, honor it only when allowed
            // (Admin can pick any department; a restricted multi-department user can only
            // pick one of their own assigned departments).
            string? effectiveFilter = null;
            if (!string.IsNullOrWhiteSpace(filterDepartmentCode) && !string.Equals(filterDepartmentCode, "all", StringComparison.OrdinalIgnoreCase))
            {
                if (departmentCodes == null || departmentCodes.Any(d => string.Equals(d, filterDepartmentCode, StringComparison.OrdinalIgnoreCase)))
                    effectiveFilter = filterDepartmentCode;
            }

            if (effectiveFilter != null)
            {
                // Narrow to a single department: reuse the existing single-department landing logic.
                var single = await GetDepartmentLandingAsync(effectiveFilter, financialYear);
                single.IsAdminView = isAdminView;
                single.IsMultiDepartmentView = departmentCodes != null && departmentCodes.Count > 1;
                single.AvailableDepartments = availableDepartments;
                return single;
            }

            // Aggregate view: per-department summary across all allowed departments.
            // All three aggregate queries are batched into a single round-trip (QueryMultipleAsync) to reduce
            // network latency against the remote/hosted database, which is the main cause of timeouts here.
            var restrict = departmentCodes != null && departmentCodes.Count > 0;
            var batchSql = @"
                SELECT
                    ccm.DepartmentCode         AS DepartmentCode,
                    d.DepartmentName           AS DepartmentName,
                    h.Status                   AS Status,
                    CASE WHEN h.Id IS NULL THEN 1 ELSE 0 END AS IsNotCreated
                FROM budget.CostCentersMapping ccm WITH (NOLOCK)
                LEFT JOIN budget.Departments d WITH (NOLOCK) ON d.DepartmentCode = ccm.DepartmentCode
                LEFT JOIN sne.SnepHeader h WITH (NOLOCK)
                    ON h.CostCenterCode = ccm.CostCenter
                    AND h.FinancialYear = @FinancialYear
                WHERE ccm.DepartmentCode IS NOT NULL
                    AND (@RestrictDepartments = 0 OR ccm.DepartmentCode IN @Codes)
                    AND EXISTS (
                        SELECT 1 FROM budget.OriginalBudget ob WITH (NOLOCK)
                        WHERE ob.CostCenterCode = ccm.CostCenter
                            AND ob.BudgetType = 'SNEP'
                            AND LEN(ob.CostCenterCode) = 8
                            AND (ob.FundCode LIKE 'PC21%' OR ob.FundCode LIKE 'PC24%')
                            AND ob.FinancialYear = @PriorYear
                    )
                ORDER BY ccm.DepartmentCode;

                SELECT
                    h.DepartmentCode                        AS DepartmentCode,
                    ISNULL(SUM(s.PostCurrent), 0)            AS TotalPosts,
                    ISNULL(SUM(s.ProposedBudget1), 0)        AS TotalProposedBudget
                FROM sne.snep s WITH (NOLOCK)
                INNER JOIN sne.SnepHeader h WITH (NOLOCK) ON h.Id = s.HeaderId
                WHERE h.FinancialYear = @FinancialYear
                    AND (@RestrictDepartments = 0 OR h.DepartmentCode IN @Codes)
                GROUP BY h.DepartmentCode;

                SELECT
                    ccm.DepartmentCode                       AS DepartmentCode,
                    ISNULL(SUM(ob.Posts), 0)                 AS TotalPosts,
                    ISNULL(SUM(ob.OriginalBudgetAmount), 0)   AS TotalOriginalBudget
                FROM budget.OriginalBudget ob WITH (NOLOCK)
                INNER JOIN budget.CostCentersMapping ccm WITH (NOLOCK) ON ccm.CostCenter = ob.CostCenterCode
                WHERE ob.BudgetType = 'SNEP'
                    AND LEN(ob.CostCenterCode) = 8
                    AND (ob.FundCode LIKE 'PC21%' OR ob.FundCode LIKE 'PC24%')
                    AND ob.FinancialYear = @PriorYear
                    AND (@RestrictDepartments = 0 OR ccm.DepartmentCode IN @Codes)
                GROUP BY ccm.DepartmentCode;";

            using var multiAgg = await connection.QueryMultipleAsync(batchSql, new
            {
                FinancialYear = financialYear,
                PriorYear = financialYear - 1,
                RestrictDepartments = restrict ? 1 : 0,
                Codes = restrict ? departmentCodes : new List<string> { string.Empty }
            }, commandTimeout: 180);

            var raw = (await multiAgg.ReadAsync()).ToList();
            var totalsRows = (await multiAgg.ReadAsync()).ToList();
            var originalBudgetRows = (await multiAgg.ReadAsync()).ToList();

            var grouped = raw
                .GroupBy(r => new { DepartmentCode = (string)r.DepartmentCode, DepartmentName = (string?)r.DepartmentName })
                .Select(g => new SneDepartmentSummaryRowViewModel
                {
                    DepartmentCode = g.Key.DepartmentCode,
                    DepartmentName = g.Key.DepartmentName,
                    TotalCostCenters = g.Count(),
                    CreatedCount = g.Count(r => (int)r.IsNotCreated == 0 && (string)(r.Status ?? "Draft") == "Draft"),
                    PendingReviewCount = g.Count(r => (int)r.IsNotCreated == 0 && (string)(r.Status ?? "Draft") == "SubmittedBySNE"),
                    SubmittedCount = g.Count(r => (int)r.IsNotCreated == 0 && (string)(r.Status ?? "Draft") == "Submitted"),
                    NotCreatedCount = g.Count(r => (int)r.IsNotCreated == 1)
                })
                .OrderBy(r => r.DepartmentName ?? r.DepartmentCode)
                .ToList();

            var totalsByDept = totalsRows.ToDictionary(r => (string)r.DepartmentCode, r => r);
            var originalBudgetByDept = originalBudgetRows.ToDictionary(r => (string)r.DepartmentCode, r => r);

            foreach (var dept in grouped)
            {
                if (totalsByDept.TryGetValue(dept.DepartmentCode, out var t))
                {
                    dept.TotalPostsCurrentYear = t.TotalPosts;
                    dept.TotalProposedBudget = t.TotalProposedBudget;
                }
                if (originalBudgetByDept.TryGetValue(dept.DepartmentCode, out var ob))
                {
                    dept.TotalPostsPriorYear = ob.TotalPosts;
                    dept.TotalOriginalBudget = ob.TotalOriginalBudget;
                }
            }

            var model = new SneLandingViewModel
            {
                DepartmentCode = string.Empty,
                DepartmentName = "All Departments",
                FinancialYear = financialYear,
                IsAdminView = isAdminView,
                IsMultiDepartmentView = departmentCodes != null && departmentCodes.Count > 1,
                AvailableDepartments = availableDepartments,
                DepartmentSummaries = grouped,
                TotalCostCenters = grouped.Sum(g => g.TotalCostCenters),
                CreatedCount = grouped.Sum(g => g.CreatedCount),
                PendingReviewCount = grouped.Sum(g => g.PendingReviewCount),
                SubmittedCount = grouped.Sum(g => g.SubmittedCount),
                NotCreatedCount = grouped.Sum(g => g.NotCreatedCount),
                TotalPostsPriorYear = grouped.Sum(g => g.TotalPostsPriorYear),
                TotalPostsCurrentYear = grouped.Sum(g => g.TotalPostsCurrentYear),
                TotalOriginalBudget = grouped.Sum(g => g.TotalOriginalBudget),
                TotalProposedBudget = grouped.Sum(g => g.TotalProposedBudget)
            };

            return model;
        }

        public async Task<SneFormViewModel?> GetCreateOrEditDataAsync(List<string> allowedDepartmentCodes, string costCenterCode, int financialYear)
        {
            using var connection = CreateConnection();

            // An empty allowed-departments list means the caller is unrestricted (e.g. Administrator).
            var ccmSql = allowedDepartmentCodes.Count == 0
                ? @"SELECT TOP 1 CostCenter, CostCenterDescription, DepartmentCode FROM budget.CostCentersMapping WHERE CostCenter = @CostCenterCode"
                : @"SELECT TOP 1 CostCenter, CostCenterDescription, DepartmentCode FROM budget.CostCentersMapping WHERE CostCenter = @CostCenterCode AND DepartmentCode IN @DepartmentCodes";
            var ccm = await connection.QueryFirstOrDefaultAsync(ccmSql, new { CostCenterCode = costCenterCode, DepartmentCodes = allowedDepartmentCodes });
            if (ccm == null)
                return null; // cost center does not belong to any of this user's allowed departments

            string departmentCode = ccm.DepartmentCode;
            var deptSql = @"SELECT TOP 1 DepartmentName FROM budget.Departments WHERE DepartmentCode = @DepartmentCode";
            var departmentName = await connection.QueryFirstOrDefaultAsync<string>(deptSql, new { DepartmentCode = departmentCode });

            var headerSql = @"SELECT Id, Status, SneUserLocked FROM sne.SnepHeader WHERE CostCenterCode = @CostCenterCode AND FinancialYear = @FinancialYear";
            var header = await connection.QueryFirstOrDefaultAsync(headerSql, new { CostCenterCode = costCenterCode, FinancialYear = financialYear });

            var model = new SneFormViewModel
            {
                HeaderId = header?.Id,
                Status = header?.Status ?? "Draft",
                SneUserLocked = header != null && (bool)header.SneUserLocked,
                FinancialYear = financialYear,
                DepartmentCode = departmentCode,
                DepartmentName = departmentName,
                CostCenterCode = ccm.CostCenter,
                CostCenterDesc = ccm.CostCenterDescription
            };

            if (header != null)
            {
                // Load persisted lines for this header (previously saved draft/submitted data)
                var lineSql = @"
                    SELECT Id, Fund, glCode, glDesc, SubHeadCode, PostCode, Designation, BS,
                           PostPrevious, PostCurrent, ActualExpLast8Previous, ActualLast4Current,
                           TotalEstimatedExpCurrent, BudgetEstimateCurrent, ProposedBudget1, ProposedBudget2, ProposedBudget3,
                           Justification
                    FROM sne.snep
                    WHERE HeaderId = @HeaderId
                    ORDER BY Id";
                var lineRows = (await connection.QueryAsync(lineSql, new { HeaderId = header.Id })).ToList();

                if (lineRows.Count > 0)
                {
                    model.FundCode = lineRows[0].Fund;
                    model.Lines = lineRows.Select(r => new SneLineItemViewModel
                    {
                        Id = r.Id,
                        IsFetched = !string.IsNullOrWhiteSpace((string?)r.glCode),
                        GlCode = r.glCode,
                        GlDesc = r.glDesc,
                        SubHeadCode = r.SubHeadCode,
                        PostCode = r.PostCode,
                        Designation = r.Designation,
                        BS = r.BS,
                        PostPrevious = r.PostPrevious,
                        PostCurrent = r.PostCurrent,
                        ActualExpLast8Previous = r.ActualExpLast8Previous,
                        ActualLast4Current = r.ActualLast4Current,
                        TotalEstimatedExpCurrent = r.TotalEstimatedExpCurrent,
                        BudgetEstimateCurrent = r.BudgetEstimateCurrent,
                        ProposedBudget1 = r.ProposedBudget1,
                        ProposedBudget2 = r.ProposedBudget2,
                        ProposedBudget3 = r.ProposedBudget3,
                        Justification = r.Justification
                    }).ToList();
                }
            }

            if (model.Lines.Count == 0)
            {
                // Auto-populate from OriginalBudget (BudgetType = 'SNEP', 8-digit cost center, prior financial year) joined with GL_Codes
                var sourceSql = @"
                    SELECT TOP 1 FundCode, FundDescription
                    FROM budget.OriginalBudget
                    WHERE BudgetType = 'SNEP' AND LEN(CostCenterCode) = 8
                        AND (FundCode LIKE 'PC21%' OR FundCode LIKE 'PC24%')
                        AND CostCenterCode = @CostCenterCode AND FinancialYear = @PriorYear;

                    SELECT
                        ob.CommitmentItem AS GlCode,
                        gl.GL_Desc        AS GlDesc,
                        ob.SubHead        AS SubHeadCode,
                        ob.DesignationCode AS PostCode,
                        ob.DesignationDescription AS Designation,
                        ob.BPS            AS BS,
                        ob.Posts          AS PostPrevious,
                        ob.OriginalBudgetAmount AS BudgetEstimateCurrent
                    FROM budget.OriginalBudget ob
                    LEFT JOIN budget.GL_Codes gl ON gl.GL_Code = ob.CommitmentItem
                    WHERE ob.BudgetType = 'SNEP' AND LEN(ob.CostCenterCode) = 8
                        AND (ob.FundCode LIKE 'PC21%' OR ob.FundCode LIKE 'PC24%')
                        AND ob.CostCenterCode = @CostCenterCode AND ob.FinancialYear = @PriorYear
                    ORDER BY ob.Id;";

                using var multi = await connection.QueryMultipleAsync(sourceSql, new { CostCenterCode = costCenterCode, PriorYear = financialYear - 1 });
                var fund = await multi.ReadFirstOrDefaultAsync();
                var sourceRows = (await multi.ReadAsync<SneLineItemViewModel>()).ToList();

                if (fund != null)
                {
                    model.FundCode = fund.FundCode;
                    model.FundDescription = fund.FundDescription;
                }

                foreach (var row in sourceRows)
                {
                    row.IsFetched = true;
                }

                model.Lines = sourceRows;
            }

            return model;
        }

        public async Task<SneGlCodeLookupViewModel?> GetGlCodeInfoAsync(string glCode)
        {
            using var connection = CreateConnection();

            var glSql = @"SELECT TOP 1 GL_Code, GL_Desc FROM budget.GL_Codes WHERE GL_Code = @GlCode";
            var gl = await connection.QueryFirstOrDefaultAsync(glSql, new { GlCode = glCode });
            if (gl == null)
                return null;

            // budget.OriginalBudget can hold several SubHead values per CommitmentItem (GL Code);
            // pick the most frequently used one as the best-guess default.
            var subHeadSql = @"
                SELECT TOP 1 SubHead
                FROM budget.OriginalBudget
                WHERE CommitmentItem = @GlCode AND SubHead IS NOT NULL AND SubHead <> ''
                GROUP BY SubHead
                ORDER BY COUNT(1) DESC";
            var subHead = await connection.QueryFirstOrDefaultAsync<string>(subHeadSql, new { GlCode = glCode });

            return new SneGlCodeLookupViewModel
            {
                GlCode = gl.GL_Code,
                GlDesc = gl.GL_Desc,
                SubHeadCode = subHead
            };
        }

        public async Task<int> SaveSneAsync(SneSaveRequest request, string? userId, bool isDepartmentSubmitter)
        {
            using var connection = CreateConnection();
            connection.Open();
            using var transaction = connection.BeginTransaction();

            try
            {
                int headerId;

                // Two-stage submission: an SNEDepartmentUser submission moves the header to
                // "SubmittedBySNE" (pending department review); a DepartmentUser/Administrator
                // submission moves it to "Submitted" (final). Saving without submitting always
                // resets the header to Draft so it can keep being edited.
                var status = !request.IsSubmit
                    ? SneFormViewModel.StatusDraft
                    : (isDepartmentSubmitter ? SneFormViewModel.StatusSubmitted : SneFormViewModel.StatusSubmittedBySne);

                if (request.HeaderId.HasValue)
                {
                    headerId = request.HeaderId.Value;

                    var updateHeaderSql = request.IsSubmit
                        ? (isDepartmentSubmitter
                            ? @"UPDATE sne.SnepHeader
                                SET Status = @Status, UpdatedByUserId = @UserId, UpdatedDate = GETDATE(),
                                    SubmittedByUserId = @UserId, SubmittedDate = GETDATE(),
                                    DeptSubmittedByUserId = @UserId, DeptSubmittedDate = GETDATE(),
                                    SneUserLocked = 0
                                WHERE Id = @HeaderId"
                            : @"UPDATE sne.SnepHeader
                                SET Status = @Status, UpdatedByUserId = @UserId, UpdatedDate = GETDATE(),
                                    SubmittedByUserId = @UserId, SubmittedDate = GETDATE(),
                                    SneSubmittedByUserId = @UserId, SneSubmittedDate = GETDATE()
                                WHERE Id = @HeaderId")
                        : @"UPDATE sne.SnepHeader
                            SET Status = @Status, UpdatedByUserId = @UserId, UpdatedDate = GETDATE()
                            WHERE Id = @HeaderId";

                    await connection.ExecuteAsync(updateHeaderSql, new { Status = status, UserId = userId, HeaderId = headerId }, transaction);

                    await connection.ExecuteAsync("DELETE FROM sne.snep WHERE HeaderId = @HeaderId", new { HeaderId = headerId }, transaction);
                }
                else
                {
                    var ccmSql = "SELECT TOP 1 DepartmentCode, CostCenterDescription FROM budget.CostCentersMapping WHERE CostCenter = @CostCenterCode";
                    var ccm = await connection.QueryFirstOrDefaultAsync(ccmSql, new { request.CostCenterCode }, transaction);

                    var insertHeaderSql = @"
                        INSERT INTO sne.SnepHeader
                            (FinancialYear, CostCenterCode, CostCenterDesc, DepartmentCode, Status, CreatedByUserId, CreatedDate,
                             SubmittedByUserId, SubmittedDate, SneSubmittedByUserId, SneSubmittedDate, DeptSubmittedByUserId, DeptSubmittedDate)
                        OUTPUT INSERTED.Id
                        VALUES
                            (@FinancialYear, @CostCenterCode, @CostCenterDesc, @DepartmentCode, @Status, @UserId, GETDATE(),
                             @SubmittedByUserId, @SubmittedDate, @SneSubmittedByUserId, @SneSubmittedDate, @DeptSubmittedByUserId, @DeptSubmittedDate)";

                    headerId = await connection.ExecuteScalarAsync<int>(insertHeaderSql, new
                    {
                        request.FinancialYear,
                        request.CostCenterCode,
                        CostCenterDesc = ccm?.CostCenterDescription,
                        DepartmentCode = ccm?.DepartmentCode,
                        Status = status,
                        UserId = userId,
                        SubmittedByUserId = request.IsSubmit ? userId : null,
                        SubmittedDate = request.IsSubmit ? (DateTime?)DateTime.UtcNow : null,
                        SneSubmittedByUserId = (request.IsSubmit && !isDepartmentSubmitter) ? userId : null,
                        SneSubmittedDate = (request.IsSubmit && !isDepartmentSubmitter) ? (DateTime?)DateTime.UtcNow : null,
                        DeptSubmittedByUserId = (request.IsSubmit && isDepartmentSubmitter) ? userId : null,
                        DeptSubmittedDate = (request.IsSubmit && isDepartmentSubmitter) ? (DateTime?)DateTime.UtcNow : null
                    }, transaction);
                }

                var ccmDesc = await connection.QueryFirstOrDefaultAsync<string>(
                    "SELECT TOP 1 CostCenterDescription FROM budget.CostCentersMapping WHERE CostCenter = @CostCenterCode",
                    new { request.CostCenterCode }, transaction);

                var insertLineSql = @"
                    INSERT INTO sne.snep
                        (Fund, CostCenterCode, CostCenterDesc, glCode, glDesc, SubHeadCode, PostCode, Designation, BS,
                         PostPrevious, PostCurrent, ActualExpLast8Previous, ActualLast4Current, TotalEstimatedExpCurrent,
                         BudgetEstimateCurrent, ProposedBudget1, ProposedBudget2, ProposedBudget3, HeaderId, FinancialYear, Justification)
                    VALUES
                        (@Fund, @CostCenterCode, @CostCenterDesc, @GlCode, @GlDesc, @SubHeadCode, @PostCode, @Designation, @BS,
                         @PostPrevious, @PostCurrent, @ActualExpLast8Previous, @ActualLast4Current, @TotalEstimatedExpCurrent,
                         @BudgetEstimateCurrent, @ProposedBudget1, @ProposedBudget2, @ProposedBudget3, @HeaderId, @FinancialYear, @Justification)";

                foreach (var line in request.Lines)
                {
                    await connection.ExecuteAsync(insertLineSql, new
                    {
                        Fund = request.FundCode,
                        request.CostCenterCode,
                        CostCenterDesc = ccmDesc,
                        line.GlCode,
                        line.GlDesc,
                        line.SubHeadCode,
                        line.PostCode,
                        line.Designation,
                        line.BS,
                        line.PostPrevious,
                        line.PostCurrent,
                        line.ActualExpLast8Previous,
                        line.ActualLast4Current,
                        line.TotalEstimatedExpCurrent,
                        line.BudgetEstimateCurrent,
                        line.ProposedBudget1,
                        line.ProposedBudget2,
                        line.ProposedBudget3,
                        HeaderId = headerId,
                        request.FinancialYear,
                        line.Justification
                    }, transaction);
                }

                // When a plain SNE Department User submits (status -> SubmittedBySNE), snapshot the
                // just-saved line items into sne.snep_SneSnapshot so the SNE user's own view keeps
                // showing exactly what they submitted, even if the Budget Officer later edits
                // sne.snep. Replace any previous snapshot for this header.
                if (request.IsSubmit && !isDepartmentSubmitter)
                {
                    await connection.ExecuteAsync("DELETE FROM sne.snep_SneSnapshot WHERE HeaderId = @HeaderId", new { HeaderId = headerId }, transaction);

                    var snapshotSql = @"
                        INSERT INTO sne.snep_SneSnapshot
                            (HeaderId, Fund, glCode, glDesc, SubHeadCode, PostCode, Designation, BS,
                             PostPrevious, PostCurrent, ActualExpLast8Previous, ActualLast4Current,
                             TotalEstimatedExpCurrent, BudgetEstimateCurrent, ProposedBudget1, ProposedBudget2, ProposedBudget3, Justification)
                        SELECT HeaderId, Fund, glCode, glDesc, SubHeadCode, PostCode, Designation, BS,
                               PostPrevious, PostCurrent, ActualExpLast8Previous, ActualLast4Current,
                               TotalEstimatedExpCurrent, BudgetEstimateCurrent, ProposedBudget1, ProposedBudget2, ProposedBudget3, Justification
                        FROM sne.snep
                        WHERE HeaderId = @HeaderId";
                    await connection.ExecuteAsync(snapshotSql, new { HeaderId = headerId }, transaction);
                }

                transaction.Commit();
                return headerId;
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }

        public async Task<string?> GetHeaderStatusAsync(int headerId)
        {
            using var connection = CreateConnection();
            return await connection.QueryFirstOrDefaultAsync<string?>(
                "SELECT Status FROM sne.SnepHeader WHERE Id = @HeaderId", new { HeaderId = headerId });
        }

        public async Task<List<SneLineItemViewModel>> GetSneSnapshotLinesAsync(int headerId)
        {
            using var connection = CreateConnection();
            var sql = @"
                SELECT Id, Fund, glCode, glDesc, SubHeadCode, PostCode, Designation, BS,
                       PostPrevious, PostCurrent, ActualExpLast8Previous, ActualLast4Current,
                       TotalEstimatedExpCurrent, BudgetEstimateCurrent, ProposedBudget1, ProposedBudget2, ProposedBudget3,
                       Justification
                FROM sne.snep_SneSnapshot
                WHERE HeaderId = @HeaderId
                ORDER BY Id";
            var rows = (await connection.QueryAsync(sql, new { HeaderId = headerId })).ToList();

            return rows.Select(r => new SneLineItemViewModel
            {
                Id = r.Id,
                IsFetched = !string.IsNullOrWhiteSpace((string?)r.glCode),
                GlCode = r.glCode,
                GlDesc = r.glDesc,
                SubHeadCode = r.SubHeadCode,
                PostCode = r.PostCode,
                Designation = r.Designation,
                BS = r.BS,
                PostPrevious = r.PostPrevious,
                PostCurrent = r.PostCurrent,
                ActualExpLast8Previous = r.ActualExpLast8Previous,
                ActualLast4Current = r.ActualLast4Current,
                TotalEstimatedExpCurrent = r.TotalEstimatedExpCurrent,
                BudgetEstimateCurrent = r.BudgetEstimateCurrent,
                ProposedBudget1 = r.ProposedBudget1,
                ProposedBudget2 = r.ProposedBudget2,
                ProposedBudget3 = r.ProposedBudget3,
                Justification = r.Justification
            }).ToList();
        }

        public async Task RevertSneAsync(int headerId, string? userId, bool lockOutSneUser)
        {
            using var connection = CreateConnection();
            var sql = @"
                UPDATE sne.SnepHeader
                SET Status = @Status, RevertedByUserId = @UserId, RevertedDate = GETDATE(),
                    UpdatedByUserId = @UserId, UpdatedDate = GETDATE(),
                    SneUserLocked = @SneUserLocked
                WHERE Id = @HeaderId";
            await connection.ExecuteAsync(sql, new { Status = SneFormViewModel.StatusDraft, UserId = userId, HeaderId = headerId, SneUserLocked = lockOutSneUser });
        }

        private static readonly string[] ExportHeaders = new[]
        {
            "GL Code", "Description", "Sub Head", "Post Code", "Designation", "BS",
            "No of Posts Previous", "No of Posts Current",
            "Actual Exp Last 8 Months", "Actual Exp First 4 Months",
            "Total Estimate Exp", "Budget Estimate Current",
            "Proposed Budget 1", "Proposed Budget 2", "Proposed Budget 3", "Justification"
        };

        // Builds the Excel header labels to match exactly what is shown in the BM-II form UI
        // (Views/Sne/CreateOrEdit.cshtml), including the financial-year suffixes.
        private static string[] BuildExportHeaders(int financialYear)
        {
            var yearNext = financialYear + 1;
            var yearAfter = yearNext + 1;
            var yearFinal = yearAfter + 1;
            var priorYear = financialYear - 1;
            string Yy(int y) => y.ToString().Substring(2);

            return new[]
            {
                "Gl Code", "Description", "Sub Head", "Post Code", "Designation", "BS",
                $"No of Posts {priorYear}-{Yy(financialYear)}",
                $"No of Posts {financialYear}-{Yy(yearNext)}",
                "Actual Exp Last 8 Months", "Actual Exp First 4 Months",
                $"Total Estimate Exp {priorYear}-{Yy(financialYear)}",
                $"Budget Estimate {priorYear}-{Yy(financialYear)}",
                $"Proposed Budget {financialYear}-{Yy(yearNext)}",
                $"Budget Estimate {yearNext}-{Yy(yearAfter)}",
                $"Budget Estimate {yearAfter}-{Yy(yearFinal)}",
                "Justification"
            };
        }

        public async Task<(byte[] content, string fileName)?> ExportCostCenterExcelAsync(List<string> allowedDepartmentCodes, string costCenterCode, int financialYear)
        {
            var model = await GetCreateOrEditDataAsync(allowedDepartmentCodes, costCenterCode, financialYear);
            if (model == null)
                return null;

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage();
            var ws = package.Workbook.Worksheets.Add("SNE");

            // Header info rows
            ws.Cells[1, 1].Value = "Cost Center Code";
            ws.Cells[1, 2].Value = model.CostCenterCode;
            ws.Cells[2, 1].Value = "Cost Center Description";
            ws.Cells[2, 2].Value = model.CostCenterDesc;
            ws.Cells[3, 1].Value = "Department";
            ws.Cells[3, 2].Value = model.DepartmentName;
            ws.Cells[4, 1].Value = "Fund Code";
            ws.Cells[4, 2].Value = model.FundCode;
            ws.Cells[5, 1].Value = "Financial Year";
            ws.Cells[5, 2].Value = model.FinancialYear;
            for (int r = 1; r <= 5; r++)
                ws.Cells[r, 1].Style.Font.Bold = true;

            int headerRow = 7;
            var exportHeaders = BuildExportHeaders(model.FinancialYear);
            for (int c = 0; c < exportHeaders.Length; c++)
            {
                var cell = ws.Cells[headerRow, c + 1];
                cell.Value = exportHeaders[c];
                cell.Style.Font.Bold = true;
                cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
                cell.Style.Fill.BackgroundColor.SetColor(Color.LightGray);
            }

            int row = headerRow + 1;
            foreach (var line in model.Lines)
            {
                ws.Cells[row, 1].Value = line.GlCode;
                ws.Cells[row, 2].Value = line.GlDesc;
                ws.Cells[row, 3].Value = line.SubHeadCode;
                ws.Cells[row, 4].Value = line.PostCode;
                ws.Cells[row, 5].Value = line.Designation;
                ws.Cells[row, 6].Value = line.BS;
                ws.Cells[row, 7].Value = line.PostPrevious;
                ws.Cells[row, 8].Value = line.PostCurrent;
                ws.Cells[row, 9].Value = line.ActualExpLast8Previous;
                ws.Cells[row, 10].Value = line.ActualLast4Current;
                ws.Cells[row, 11].Value = line.TotalEstimatedExpCurrent;
                ws.Cells[row, 12].Value = line.BudgetEstimateCurrent;
                ws.Cells[row, 13].Value = line.ProposedBudget1;
                ws.Cells[row, 14].Value = line.ProposedBudget2;
                ws.Cells[row, 15].Value = line.ProposedBudget3;
                ws.Cells[row, 16].Value = line.Justification;
                row++;
            }

            ws.Cells[headerRow, 1, row - 1, exportHeaders.Length].AutoFitColumns();

            var fileName = $"SNE_{model.CostCenterCode}_{model.FinancialYear}.xlsx";
            return (package.GetAsByteArray(), fileName);
        }

        public async Task<(List<SneLineItemViewModel> lines, List<string> errors)> ImportCostCenterExcelAsync(Stream fileStream, string expectedCostCenterCode, string? expectedFundCode)
        {
            var lines = new List<SneLineItemViewModel>();
            var errors = new List<string>();

            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
            using var package = new ExcelPackage(fileStream);
            var ws = package.Workbook.Worksheets.FirstOrDefault();
            if (ws == null)
            {
                errors.Add("The uploaded file does not contain any worksheet.");
                return (lines, errors);
            }

            // Locate the header row (matches ExportHeaders[0] = "GL Code") so the import tolerates
            // the leading header-info rows written by ExportCostCenterExcelAsync.
            int headerRow = -1;
            int maxScanRow = Math.Min(ws.Dimension?.Rows ?? 0, 20);
            for (int r = 1; r <= maxScanRow; r++)
            {
                if (string.Equals(ws.Cells[r, 1].Text.Trim(), ExportHeaders[0], StringComparison.OrdinalIgnoreCase))
                {
                    headerRow = r;
                    break;
                }
            }

            if (headerRow == -1)
            {
                errors.Add("Could not find the data header row (expected a 'GL Code' column). Please use the exported template.");
                return (lines, errors);
            }

            // Validate the header-info rows (written by ExportCostCenterExcelAsync as rows 1-5:
            // Cost Center Code, Cost Center Description, Department, Fund Code, Financial Year)
            // against the cost center/fund of the form currently being edited, so a file exported
            // for a different cost center or fund cannot be imported here by mistake.
            string? ReadInfoValue(string label)
            {
                for (int r = 1; r < headerRow; r++)
                {
                    if (string.Equals(ws.Cells[r, 1].Text.Trim(), label, StringComparison.OrdinalIgnoreCase))
                        return ws.Cells[r, 2].Text?.Trim();
                }
                return null;
            }

            var fileCostCenterCode = ReadInfoValue("Cost Center Code");
            var fileFundCode = ReadInfoValue("Fund Code");

            if (!string.IsNullOrWhiteSpace(fileCostCenterCode) &&
                !string.Equals(fileCostCenterCode, expectedCostCenterCode, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"The uploaded file is for Cost Center '{fileCostCenterCode}', but this form is for Cost Center '{expectedCostCenterCode}'. Please upload the correct file.");
            }

            if (!string.IsNullOrWhiteSpace(fileFundCode) && !string.IsNullOrWhiteSpace(expectedFundCode) &&
                !string.Equals(fileFundCode, expectedFundCode, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"The uploaded file is for Fund Code '{fileFundCode}', but this form is for Fund Code '{expectedFundCode}'. Please upload the correct file.");
            }

            if (errors.Count > 0)
                return (lines, errors);

            // Map canonical field name -> column index. Exported header labels now include dynamic
            // financial-year suffixes (e.g. "No of Posts 2025-26") to match the UI form, so columns are
            // matched using fixed exact names or ordered prefixes rather than a literal string match.
            // This keeps import working regardless of which financial year the file was exported for.
            var colMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int maxCol = ws.Dimension?.Columns ?? ExportHeaders.Length;
            var headerTexts = new List<(int col, string text)>();
            for (int c = 1; c <= maxCol; c++)
            {
                var header = ws.Cells[headerRow, c].Text.Trim();
                if (!string.IsNullOrWhiteSpace(header))
                    headerTexts.Add((c, header));
            }

            void MapExact(string canonical, params string[] candidates)
            {
                foreach (var (col, text) in headerTexts)
                {
                    if (candidates.Any(cand => string.Equals(text, cand, StringComparison.OrdinalIgnoreCase)))
                    {
                        colMap[canonical] = col;
                        return;
                    }
                }
            }

            void MapByPrefixOccurrence(string prefix, params string[] canonicalNamesInOrder)
            {
                var matches = headerTexts.Where(h => h.text.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).ToList();
                for (int i = 0; i < canonicalNamesInOrder.Length && i < matches.Count; i++)
                    colMap[canonicalNamesInOrder[i]] = matches[i].col;
            }

            MapExact("GL Code", "GL Code", "Gl Code");
            MapExact("Description", "Description");
            MapExact("Sub Head", "Sub Head");
            MapExact("Post Code", "Post Code");
            MapExact("Designation", "Designation");
            MapExact("BS", "BS");
            MapExact("Actual Exp Last 8 Months", "Actual Exp Last 8 Months");
            MapExact("Actual Exp First 4 Months", "Actual Exp First 4 Months");
            MapExact("Justification", "Justification");
            MapByPrefixOccurrence("No of Posts", "No of Posts Previous", "No of Posts Current");
            MapByPrefixOccurrence("Total Estimate Exp", "Total Estimate Exp");
            MapByPrefixOccurrence("Proposed Budget", "Proposed Budget 1");
            MapByPrefixOccurrence("Budget Estimate", "Budget Estimate Current", "Proposed Budget 2", "Proposed Budget 3");

            int Col(string name) => colMap.TryGetValue(name, out var c) ? c : -1;
            string? Text(int row, string colName)
            {
                var c = Col(colName);
                if (c == -1) return null;
                var v = ws.Cells[row, c].Text?.Trim();
                return string.IsNullOrWhiteSpace(v) ? null : v;
            }
            long? Num(int row, string colName, int excelRowForError, List<string> rowErrors)
            {
                var c = Col(colName);
                if (c == -1) return null;
                var text = ws.Cells[row, c].Text?.Trim();
                if (string.IsNullOrWhiteSpace(text)) return null;
                var cleaned = text.Replace(",", "");
                if (long.TryParse(cleaned, out var l)) return l;
                if (decimal.TryParse(cleaned, out var d)) return (long)d;
                rowErrors.Add($"Row {excelRowForError}: '{colName}' value '{text}' is not a valid number.");
                return null;
            }

            int lastRow = ws.Dimension?.Rows ?? headerRow;
            for (int r = headerRow + 1; r <= lastRow; r++)
            {
                // Skip completely blank rows
                bool rowHasData = false;
                for (int c = 1; c <= maxCol; c++)
                {
                    if (!string.IsNullOrWhiteSpace(ws.Cells[r, c].Text))
                    {
                        rowHasData = true;
                        break;
                    }
                }
                if (!rowHasData) continue;

                var rowErrors = new List<string>();
                var glCode = Text(r, "GL Code");

                if (string.IsNullOrWhiteSpace(glCode))
                {
                    rowErrors.Add($"Row {r}: 'GL Code' is required.");
                    errors.AddRange(rowErrors);
                    continue;
                }

                var glInfo = await GetGlCodeInfoAsync(glCode);
                if (glInfo == null)
                {
                    rowErrors.Add($"Row {r}: GL Code '{glCode}' does not exist.");
                    errors.AddRange(rowErrors);
                    continue;
                }

                var line = new SneLineItemViewModel
                {
                    IsFetched = false,
                    GlCode = glInfo.GlCode,
                    GlDesc = !string.IsNullOrWhiteSpace(Text(r, "Description")) ? Text(r, "Description") : glInfo.GlDesc,
                    SubHeadCode = !string.IsNullOrWhiteSpace(Text(r, "Sub Head")) ? Text(r, "Sub Head") : glInfo.SubHeadCode,
                    PostCode = Text(r, "Post Code"),
                    Designation = Text(r, "Designation"),
                    BS = Text(r, "BS"),
                    PostPrevious = Num(r, "No of Posts Previous", r, rowErrors),
                    PostCurrent = Num(r, "No of Posts Current", r, rowErrors),
                    ActualExpLast8Previous = Num(r, "Actual Exp Last 8 Months", r, rowErrors),
                    ActualLast4Current = Num(r, "Actual Exp First 4 Months", r, rowErrors),
                    BudgetEstimateCurrent = Num(r, "Budget Estimate Current", r, rowErrors),
                    ProposedBudget1 = Num(r, "Proposed Budget 1", r, rowErrors),
                    ProposedBudget2 = Num(r, "Proposed Budget 2", r, rowErrors),
                    ProposedBudget3 = Num(r, "Proposed Budget 3", r, rowErrors),
                    Justification = Text(r, "Justification")
                };
                line.TotalEstimatedExpCurrent = (line.ActualExpLast8Previous ?? 0) + (line.ActualLast4Current ?? 0);

                if (rowErrors.Count > 0)
                {
                    errors.AddRange(rowErrors);
                    continue;
                }

                lines.Add(line);
            }

            if (lines.Count == 0 && errors.Count == 0)
                errors.Add("No data rows were found in the uploaded file.");

            return (lines, errors);
        }
    }
}
