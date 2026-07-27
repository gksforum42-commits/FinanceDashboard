-- Run this script once against your database to create the budget.ObjectCodes table
-- which stores GL Code → GL Description mappings used in the Cost Center GL Detail sub-report.

IF NOT EXISTS (
	SELECT 1 FROM sys.tables t
	JOIN sys.schemas s ON s.schema_id = t.schema_id
	WHERE s.name = 'budget' AND t.name = 'ObjectCodes'
)
BEGIN
	CREATE TABLE budget.ObjectCodes (
		Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
		ObjectCodeValue  NVARCHAR(20)  NOT NULL,
		ObjectName       NVARCHAR(500) NULL,
		MajorHead        NVARCHAR(20)  NULL,
		MinorHead        NVARCHAR(20)  NULL,
		IsActive         BIT           NOT NULL DEFAULT 1,
		CreatedDate      DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
		CONSTRAINT UQ_ObjectCodes_ObjectCodeValue UNIQUE (ObjectCodeValue)
	);

	PRINT 'budget.ObjectCodes table created successfully.';
END
ELSE
BEGIN
	PRINT 'budget.ObjectCodes table already exists — skipped.';
END
