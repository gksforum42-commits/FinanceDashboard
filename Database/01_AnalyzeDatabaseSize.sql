-- =====================================================================
-- STEP 1: ANALYZE — Run this FIRST. It changes NOTHING.
-- Shows exactly where the 4.78 GB is being used.
-- =====================================================================

-- 1. Data file vs Log file size (log files are often the hidden culprit)
SELECT
	name              AS FileName,
	type_desc         AS FileType,
	size * 8 / 1024   AS SizeMB,
	CAST(FILEPROPERTY(name, 'SpaceUsed') AS int) * 8 / 1024 AS UsedMB,
	(size - CAST(FILEPROPERTY(name, 'SpaceUsed') AS int)) * 8 / 1024 AS FreeMB
FROM sys.database_files;

-- 2. Size per table (biggest first)
SELECT
	s.name + '.' + t.name                              AS TableName,
	p.rows                                             AS [RowCount],
	SUM(a.total_pages) * 8 / 1024                      AS TotalMB,
	SUM(a.used_pages)  * 8 / 1024                      AS UsedMB,
	(SUM(a.total_pages) - SUM(a.used_pages)) * 8 / 1024 AS UnusedMB
FROM sys.tables t
JOIN sys.schemas s        ON t.schema_id = s.schema_id
JOIN sys.indexes i        ON t.object_id = i.object_id
JOIN sys.partitions p     ON i.object_id = p.object_id AND i.index_id = p.index_id
JOIN sys.allocation_units a ON p.partition_id = a.container_id
GROUP BY s.name, t.name, p.rows
ORDER BY TotalMB DESC;

-- 3. AuditLogs breakdown (often grows huge from OldValues/NewValues text)
SELECT
	Action,
	COUNT(*)                              AS Rows,
	MIN(ActionDate)                       AS Oldest,
	MAX(ActionDate)                       AS Newest
FROM admin.AuditLogs
GROUP BY Action
ORDER BY Rows DESC;

-- 4. ImportHistory breakdown
SELECT Status, COUNT(*) AS Rows, MIN(ImportedDate) AS Oldest
FROM budget.ImportHistory
GROUP BY Status;

-- 5. BerSapData staging row count (this table is fully replaced on every import —
--    it should only contain the LAST import's rows)
SELECT COUNT(*) AS BerSapDataRows FROM budget.BerSapData;

-- 6. BERData row count per financial year (check for duplicated/old years)
SELECT FinancialYear, COUNT(*) AS Rows
FROM budget.BERData
GROUP BY FinancialYear
ORDER BY FinancialYear DESC;

-- 7. Index fragmentation (heavily fragmented indexes waste space)
SELECT
	s.name + '.' + t.name AS TableName,
	i.name                AS IndexName,
	ips.avg_fragmentation_in_percent AS FragPct,
	ips.page_count * 8 / 1024        AS IndexMB
FROM sys.dm_db_index_physical_stats(DB_ID(), NULL, NULL, NULL, 'LIMITED') ips
JOIN sys.tables t  ON ips.object_id = t.object_id
JOIN sys.schemas s ON t.schema_id = s.schema_id
JOIN sys.indexes i ON ips.object_id = i.object_id AND ips.index_id = i.index_id
WHERE ips.page_count > 100
ORDER BY IndexMB DESC;
