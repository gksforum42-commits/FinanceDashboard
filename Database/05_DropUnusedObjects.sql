-- =====================================================================
-- DROP UNUSED PROCEDURES / VIEWS / FUNCTIONS
-- Verified: not called by the app, never executed, referenced by nothing.
--
-- BEFORE RUNNING: back up their definitions (so you can restore any time):
--   In SSMS: right-click each object > Script As > CREATE To > File
--   Or run the SELECT below to dump all definitions at once and save output.
-- =====================================================================

-- Backup all definitions first (copy the output somewhere safe):
SELECT o.type_desc, s.name + '.' + o.name AS ObjectName, m.definition
FROM sys.sql_modules m
JOIN sys.objects o ON m.object_id = o.object_id
JOIN sys.schemas s ON o.schema_id = s.schema_id
WHERE s.name + '.' + o.name IN (
	'budget.sp_GetBERDataFiltered', 'budget.sp_GetCompleteBudgetReport',
	'budget.sp_GetCostCenterWiseReport', 'budget.sp_GetDashboardSummary',
	'budget.sp_GetDepartmentWiseReport', 'budget.sp_GetFunctionWiseReport',
	'budget.sp_GetFundWiseReport', 'budget.sp_GetGrantWiseReport',
	'budget.sp_GetObjectWiseReport',
	'budget.vw_BERByFund', 'budget.vw_DashboardSummary',
	'budget.vw_MonthlyExpenditure', 'budget.vw_OriginalBudgetByDepartment',
	'budget.fn_GetMonthlyTrend');
GO

-- ── Unused stored procedures ──────────────────────────────────────────
DROP PROCEDURE IF EXISTS budget.sp_GetBERDataFiltered;
DROP PROCEDURE IF EXISTS budget.sp_GetCompleteBudgetReport;
DROP PROCEDURE IF EXISTS budget.sp_GetCostCenterWiseReport;
DROP PROCEDURE IF EXISTS budget.sp_GetDashboardSummary;
DROP PROCEDURE IF EXISTS budget.sp_GetDepartmentWiseReport;
DROP PROCEDURE IF EXISTS budget.sp_GetFunctionWiseReport;
DROP PROCEDURE IF EXISTS budget.sp_GetFundWiseReport;
DROP PROCEDURE IF EXISTS budget.sp_GetGrantWiseReport;
DROP PROCEDURE IF EXISTS budget.sp_GetObjectWiseReport;
GO

-- ── Unused views ──────────────────────────────────────────────────────
DROP VIEW IF EXISTS budget.vw_BERByFund;
DROP VIEW IF EXISTS budget.vw_DashboardSummary;
DROP VIEW IF EXISTS budget.vw_MonthlyExpenditure;
DROP VIEW IF EXISTS budget.vw_OriginalBudgetByDepartment;
GO

-- ── Unused function ───────────────────────────────────────────────────
DROP FUNCTION IF EXISTS budget.fn_GetMonthlyTrend;
GO

-- ── DO NOT DROP (kept for reference) ─────────────────────────────────
-- USED BY APP:  admin.sp_GetAuditLogs, budget.sp_DeleteImport,
--               budget.sp_GetImportHistory, budget.sp_GetLookupValues,
--               budget.sp_GetOriginalBudgetFiltered, budget.vw_BudgetComparison
-- SYSTEM:       dbo.sp_MScleanupmergepublisher, dbo.sp_MSrepl_startup, dbo.spt_values

-- Verify what remains:
SELECT s.name + '.' + p.name AS ProcName FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id
WHERE s.name IN ('budget','admin') ORDER BY 1;
SELECT s.name + '.' + v.name AS ViewName FROM sys.views v
JOIN sys.schemas s ON v.schema_id = s.schema_id
WHERE s.name IN ('budget','admin') ORDER BY 1;
