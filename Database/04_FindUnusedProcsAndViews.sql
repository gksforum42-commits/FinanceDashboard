-- =====================================================================
-- FIND UNUSED STORED PROCEDURES AND VIEWS
-- The application code only references:
--   Procs: admin.sp_GetAuditLogs, budget.sp_DeleteImport,
--          budget.sp_GetImportHistory, budget.sp_GetLookupValues,
--          budget.sp_GetOriginalBudgetFiltered
--   Views: budget.vw_BudgetComparison
-- Anything else flagged 'NOT USED BY APP' below is a removal candidate.
-- This script only ANALYZES — it deletes nothing.
-- =====================================================================

-- 1. All stored procedures: app usage + last execution time on the server
SELECT
	s.name + '.' + p.name AS ProcedureName,
	CASE WHEN s.name + '.' + p.name IN (
		'admin.sp_GetAuditLogs',
		'budget.sp_DeleteImport',
		'budget.sp_GetImportHistory',
		'budget.sp_GetLookupValues',
		'budget.sp_GetOriginalBudgetFiltered'
	) THEN 'USED BY APP' ELSE 'NOT USED BY APP' END AS AppUsage,
	st.last_execution_time  AS LastExecutedOnServer,   -- NULL = not run since last SQL restart
	st.execution_count      AS ExecCountSinceRestart,
	p.create_date,
	p.modify_date
FROM sys.procedures p
JOIN sys.schemas s ON p.schema_id = s.schema_id
LEFT JOIN sys.dm_exec_procedure_stats st
	   ON st.object_id = p.object_id AND st.database_id = DB_ID()
ORDER BY AppUsage DESC, ProcedureName;

-- 2. All views: app usage flag
SELECT
	s.name + '.' + v.name AS ViewName,
	CASE WHEN s.name + '.' + v.name = 'budget.vw_BudgetComparison'
		 THEN 'USED BY APP' ELSE 'NOT USED BY APP' END AS AppUsage,
	v.create_date,
	v.modify_date
FROM sys.views v
JOIN sys.schemas s ON v.schema_id = s.schema_id
ORDER BY AppUsage DESC, ViewName;

-- 3. Check for dependencies between DB objects (a proc/view may be used by
--    ANOTHER proc/view even if the app doesn't call it directly)
SELECT
	OBJECT_SCHEMA_NAME(d.referencing_id) + '.' + OBJECT_NAME(d.referencing_id) AS ReferencedBy,
	d.referenced_schema_name + '.' + d.referenced_entity_name                  AS ReferencedObject
FROM sys.sql_expression_dependencies d
WHERE d.referenced_id IS NOT NULL
  AND OBJECT_NAME(d.referencing_id) IS NOT NULL
ORDER BY ReferencedObject;

-- =====================================================================
-- HOW TO INTERPRET:
--  * 'NOT USED BY APP' + LastExecutedOnServer IS NULL + not referenced
--    in query 3  →  safe to drop.
--  * Note: last_execution_time resets on SQL Server restart, so check
--    after the server has been up a while.
--
-- To drop (only after verifying — procs/views hold no data, and can be
-- recreated from their script if you save it first):
--   Generate the CREATE script first:  right-click object > Script As > CREATE
--   Then: DROP PROCEDURE schema.name;   or   DROP VIEW schema.name;
-- =====================================================================
