-- Adds a Justification column to sne.snep to store long free-text justification
-- notes per line item. Safe to run multiple times (idempotent).

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'Justification')
BEGIN
	ALTER TABLE sne.snep ADD Justification NVARCHAR(MAX) NULL;
END
GO
