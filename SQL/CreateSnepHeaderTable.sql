USE FinanceDashboardDB;
GO

-- Header table: one row per (CostCenter, FinancialYear) SNE submission workflow.
-- Tracks status (Draft/Submitted), who created/submitted/reverted it and when.
IF NOT EXISTS (
	SELECT 1
	FROM sys.tables t
	JOIN sys.schemas s ON t.schema_id = s.schema_id
	WHERE s.name = 'sne' AND t.name = 'SnepHeader'
)
BEGIN
	CREATE TABLE sne.SnepHeader
	(
		Id INT IDENTITY(1,1) PRIMARY KEY,

		FinancialYear INT NOT NULL,

		CostCenterCode NVARCHAR(7) NOT NULL,
		CostCenterDesc NVARCHAR(255) NULL,
		DepartmentCode NVARCHAR(20) NULL,

		Status NVARCHAR(20) NOT NULL DEFAULT ('Draft'), -- Draft | Submitted

		CreatedByUserId NVARCHAR(450) NULL,
		CreatedDate DATETIME NOT NULL DEFAULT (GETDATE()),

		UpdatedByUserId NVARCHAR(450) NULL,
		UpdatedDate DATETIME NULL,

		SubmittedByUserId NVARCHAR(450) NULL,
		SubmittedDate DATETIME NULL,

		RevertedByUserId NVARCHAR(450) NULL,
		RevertedDate DATETIME NULL,

		CONSTRAINT UQ_SnepHeader_CostCenter_Year UNIQUE (CostCenterCode, FinancialYear)
	);
END
GO

-- Link SNE line items (snep) to their header record.
IF NOT EXISTS (
	SELECT 1 FROM sys.columns
	WHERE object_id = OBJECT_ID('sne.snep') AND name = 'HeaderId'
)
BEGIN
	ALTER TABLE sne.snep ADD HeaderId INT NULL;

	ALTER TABLE sne.snep
		ADD CONSTRAINT FK_Snep_SnepHeader FOREIGN KEY (HeaderId)
		REFERENCES sne.SnepHeader (Id);
END
GO

IF NOT EXISTS (
	SELECT 1 FROM sys.columns
	WHERE object_id = OBJECT_ID('sne.snep') AND name = 'FinancialYear'
)
BEGIN
	ALTER TABLE sne.snep ADD FinancialYear INT NULL;
END
GO
