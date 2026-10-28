-- =====================================================================
-- STEP 2: SAFE CLEANUP — Run ONLY after reviewing the analysis results.
-- Each section is independent and safe: it does NOT touch current
-- dashboard data (BERData for the active year, OriginalBudget, users).
-- Run section by section, not all at once.
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- SECTION A: Shrink the transaction LOG file (usually the biggest win).
-- On SIMPLE recovery this is safe. On FULL recovery you need a log
-- backup first, or switch to SIMPLE (fine for this kind of app).
-- ─────────────────────────────────────────────────────────────────────
ALTER DATABASE FinanceDashboardDB SET RECOVERY SIMPLE;
GO
-- Find the log file logical name:
SELECT name FROM sys.database_files WHERE type_desc = 'LOG';
-- Then shrink it (replace name if different):
DBCC SHRINKFILE (FinanceDashboardDB_log, 100);  -- shrink log to 100 MB
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION B: Trim old AuditLogs (keep last 90 days).
-- Delete in batches so the log doesn't blow up again.
-- ─────────────────────────────────────────────────────────────────────
DECLARE @cutoff datetime = DATEADD(DAY, -90, GETUTCDATE());
WHILE 1 = 1
BEGIN
	DELETE TOP (10000) FROM admin.AuditLogs WHERE ActionDate < @cutoff;
	IF @@ROWCOUNT = 0 BREAK;
END
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION C: Clear NULL-able heavy text in remaining audit rows (optional).
-- OldValues/NewValues are unbounded NVARCHAR(MAX) — biggest per-row cost.
-- Keeps the audit trail, drops the payload for rows older than 30 days.
-- ─────────────────────────────────────────────────────────────────────
UPDATE admin.AuditLogs
SET OldValues = NULL, NewValues = NULL
WHERE ActionDate < DATEADD(DAY, -30, GETUTCDATE())
  AND (OldValues IS NOT NULL OR NewValues IS NOT NULL);
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION D: Remove old/soft-deleted ImportHistory rows and their
-- orphaned OriginalBudget rows. Keeps the latest 20 imports.
-- ─────────────────────────────────────────────────────────────────────
;WITH keep AS (
	SELECT TOP (20) Id FROM budget.ImportHistory
	WHERE IsDeleted = 0
	ORDER BY ImportedDate DESC
)
DELETE FROM budget.ImportHistory
WHERE Id NOT IN (SELECT Id FROM keep)
  AND Status IN ('Failed', 'Deleted');   -- only failed/deleted ones outside the keep-set
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION E: BerSapData staging — this is wiped on every import anyway.
-- If you are NOT mid-import, you can truncate it right now; the dashboard
-- reads from BERData, not from staging.
-- ─────────────────────────────────────────────────────────────────────
-- TRUNCATE TABLE budget.BerSapData;   -- uncomment if analysis shows it is large
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION F: Old financial years in BERData (OPTIONAL — only if the
-- analysis shows years you no longer need on the dashboard).
-- Replace 2023 with the year you want to remove. DO NOT run blindly.
-- ─────────────────────────────────────────────────────────────────────
-- DELETE FROM budget.BERData WHERE FinancialYear <= 2023;
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION G: Rebuild fragmented indexes + reclaim freed space.
-- Run LAST, after the deletes above.
-- ─────────────────────────────────────────────────────────────────────
-- Rebuild all indexes on the big tables:
ALTER INDEX ALL ON budget.BERData        REBUILD;
ALTER INDEX ALL ON budget.BerSapData     REBUILD;
ALTER INDEX ALL ON budget.OriginalBudget REBUILD;
ALTER INDEX ALL ON admin.AuditLogs       REBUILD;
GO

-- Finally shrink the data file to release space back (find name first):
SELECT name FROM sys.database_files WHERE type_desc = 'ROWS';
-- DBCC SHRINKFILE (FinanceDashboardDB, 500);  -- target MB; adjust after seeing UsedMB
GO

-- Re-check size:
SELECT name, size * 8 / 1024 AS SizeMB,
	   CAST(FILEPROPERTY(name, 'SpaceUsed') AS int) * 8 / 1024 AS UsedMB
FROM sys.database_files;
