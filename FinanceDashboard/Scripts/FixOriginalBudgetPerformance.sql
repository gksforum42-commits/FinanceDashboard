-- =====================================================================
-- Performance fix for SNE / Permanent Budget landing page timeouts.
-- Run this ONCE against the live/production database (db64915).
-- All statements are idempotent and safe to re-run.
--
-- Root cause: budget.OriginalBudget has no supporting indexes for the
-- SNE landing query, and the WHERE clause used LEN(CostCenterCode) = 8,
-- which is non-sargable (SQL Server must compute LEN() for every row,
-- so it cannot use an index seek and instead does a full table/clustered
-- index scan). On a shared/free-tier hosted SQL Server (MonsterASP) this
-- easily exceeds the command timeout once the table has a non-trivial
-- number of rows.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. Supporting index for the SNE landing query filters
--    (BudgetType = 'SNEP' AND FinancialYear = @Y), grouped/joined by
--    CostCenterCode, and covering the aggregated columns.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_OriginalBudget_SneLanding' AND object_id = OBJECT_ID('budget.OriginalBudget'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_OriginalBudget_SneLanding
		ON budget.OriginalBudget (BudgetType, FinancialYear, CostCenterCode)
		INCLUDE (Posts, OriginalBudgetAmount);
END
GO

-- ---------------------------------------------------------------------
-- 2. Supporting index for budget.CostCentersMapping join
--    (join on CostCenter, select DepartmentCode).
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CostCentersMapping_CostCenter' AND object_id = OBJECT_ID('budget.CostCentersMapping'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_CostCentersMapping_CostCenter
		ON budget.CostCentersMapping (CostCenter)
		INCLUDE (DepartmentCode, CostCenterDescription);
END
GO

-- ---------------------------------------------------------------------
-- 2b. Supporting index for the SNE landing page's main filter, which
--     queries budget.CostCentersMapping WHERE DepartmentCode = @X
--     (or DepartmentCode IN @Codes for the multi-department/Admin view).
--     Without this, DepartmentCode has no leading index and SQL Server
--     must scan the entire table for every landing page load.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_CostCentersMapping_DepartmentCode' AND object_id = OBJECT_ID('budget.CostCentersMapping'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_CostCentersMapping_DepartmentCode
		ON budget.CostCentersMapping (DepartmentCode)
		INCLUDE (CostCenter, CostCenterDescription);
END
GO

-- ---------------------------------------------------------------------
-- 3. Supporting indexes for sne.SnepHeader / sne.snep aggregate queries.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnepHeader_FinancialYear_DepartmentCode' AND object_id = OBJECT_ID('sne.SnepHeader'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_SnepHeader_FinancialYear_DepartmentCode
		ON sne.SnepHeader (FinancialYear, DepartmentCode)
		INCLUDE (Status, CostCenterCode);
END
GO

-- ---------------------------------------------------------------------
-- 3b. Supporting index for the SNE landing page's per-cost-center LEFT
--     JOIN, which joins on CostCenterCode + FinancialYear (not on
--     DepartmentCode). Without a leading-CostCenterCode index, this
--     join falls back to a scan of sne.SnepHeader for every load.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_SnepHeader_CostCenterCode_FinancialYear' AND object_id = OBJECT_ID('sne.SnepHeader'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_SnepHeader_CostCenterCode_FinancialYear
		ON sne.SnepHeader (CostCenterCode, FinancialYear)
		INCLUDE (Id, Status, SubmittedDate, DepartmentCode);
END
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Snep_HeaderId' AND object_id = OBJECT_ID('sne.snep'))
BEGIN
	CREATE NONCLUSTERED INDEX IX_Snep_HeaderId
		ON sne.snep (HeaderId)
		INCLUDE (PostCurrent, ProposedBudget1);
END
GO

-- ---------------------------------------------------------------------
-- 5. Update statistics so the query optimizer picks the new indexes.
-- ---------------------------------------------------------------------
UPDATE STATISTICS budget.OriginalBudget;
UPDATE STATISTICS budget.CostCentersMapping;
UPDATE STATISTICS sne.SnepHeader;
UPDATE STATISTICS sne.snep;
GO
