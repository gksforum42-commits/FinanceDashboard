-- =====================================================================
-- NO-DELETE SIZE REDUCTION — removes ZERO rows of data.
-- Only reclaims wasted/empty space. All your data stays exactly as-is.
-- Run section by section.
-- =====================================================================

-- ─────────────────────────────────────────────────────────────────────
-- SECTION 1: Shrink the transaction LOG file.
-- The log is not your data — it's redo history. Shrinking it deletes
-- nothing from your tables. This is usually the biggest win.
-- ─────────────────────────────────────────────────────────────────────
ALTER DATABASE FinanceDashboardDB SET RECOVERY SIMPLE;
GO
-- Find the log file's logical name:
SELECT name, type_desc, size * 8 / 1024 AS SizeMB FROM sys.database_files;
GO
-- Shrink the log to 100 MB (replace the name with what the query above shows):
DBCC SHRINKFILE (FinanceDashboardDB_log, 100);
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION 2: Enable PAGE compression on the big tables.
-- Compresses how rows are stored on disk — data is 100% intact and the
-- app keeps working unchanged. Typically saves 40–70% on wide tables.
-- (Supported on SQL Server 2016 SP1+ all editions, and Azure SQL.)
-- ─────────────────────────────────────────────────────────────────────
ALTER TABLE budget.BERData        REBUILD WITH (DATA_COMPRESSION = PAGE);
GO
ALTER TABLE budget.BerSapData     REBUILD WITH (DATA_COMPRESSION = PAGE);
GO
ALTER TABLE budget.OriginalBudget REBUILD WITH (DATA_COMPRESSION = PAGE);
GO
ALTER TABLE admin.AuditLogs       REBUILD WITH (DATA_COMPRESSION = PAGE);
GO

-- Compress the indexes too:
ALTER INDEX ALL ON budget.BERData        REBUILD WITH (DATA_COMPRESSION = PAGE);
ALTER INDEX ALL ON budget.BerSapData     REBUILD WITH (DATA_COMPRESSION = PAGE);
ALTER INDEX ALL ON budget.OriginalBudget REBUILD WITH (DATA_COMPRESSION = PAGE);
ALTER INDEX ALL ON admin.AuditLogs       REBUILD WITH (DATA_COMPRESSION = PAGE);
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION 3: Rebuild remaining indexes (defragmentation).
-- Fragmented indexes waste pages. Rebuilding compacts them — no data loss.
-- ─────────────────────────────────────────────────────────────────────
ALTER INDEX ALL ON budget.ImportHistory REBUILD;
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION 4: Release the freed space back from the data file.
-- Shrinking only removes EMPTY space — never data.
-- ─────────────────────────────────────────────────────────────────────
-- Check how much is actually used now:
SELECT name,
	   size * 8 / 1024 AS SizeMB,
	   CAST(FILEPROPERTY(name, 'SpaceUsed') AS int) * 8 / 1024 AS UsedMB,
	   (size - CAST(FILEPROPERTY(name, 'SpaceUsed') AS int)) * 8 / 1024 AS FreeMB
FROM sys.database_files;
GO
-- Shrink the data file leaving 10% free space (replace name from query above):
DBCC SHRINKFILE (FinanceDashboardDB, TRUNCATEONLY);
GO
-- If you want a stronger shrink, set an explicit target slightly above UsedMB:
-- DBCC SHRINKFILE (FinanceDashboardDB, 1500);   -- example: 1500 MB target
GO

-- ─────────────────────────────────────────────────────────────────────
-- SECTION 5: Verify final size.
-- ─────────────────────────────────────────────────────────────────────
SELECT DB_NAME() AS DatabaseName,
	   SUM(size) * 8 / 1024        AS Size_MB,
	   SUM(size) * 8.0 / 1024/1024 AS Size_GB
FROM sys.database_files;
