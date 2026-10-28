-- =====================================================================
-- Consolidated deployment script for SNE module database changes.
-- Run this ONCE against the live/production database (db64915) to bring
-- it in sync with local development. All statements are idempotent and
-- safe to re-run.
-- =====================================================================

-- ---------------------------------------------------------------------
-- 1. Add Sector and DepartmentCode columns to budget.CostCentersMapping
--    and populate them from budget.BERData.
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('budget.CostCentersMapping') AND name = 'Sector')
	ALTER TABLE budget.CostCentersMapping ADD Sector VARCHAR(20) NULL;

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('budget.CostCentersMapping') AND name = 'DepartmentCode')
	ALTER TABLE budget.CostCentersMapping ADD DepartmentCode NVARCHAR(40) NULL;
GO

;WITH BerLookup AS (
	SELECT
		b.FundCenterCode,
		b.Sector,
		b.MinistryDepartment AS DepartmentCode,
		ROW_NUMBER() OVER (PARTITION BY b.FundCenterCode ORDER BY b.Id) AS rn
	FROM budget.BERData b
)
UPDATE ccm
SET ccm.Sector = bl.Sector,
	ccm.DepartmentCode = bl.DepartmentCode
FROM budget.CostCentersMapping ccm
JOIN BerLookup bl ON bl.FundCenterCode = ccm.CostCenter AND bl.rn = 1
WHERE ccm.Sector IS NULL OR ccm.DepartmentCode IS NULL;
GO

-- ---------------------------------------------------------------------
-- 2. Create sne schema
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'sne')
	EXEC('CREATE SCHEMA sne');
GO

-- ---------------------------------------------------------------------
-- 3. Create sne.SnepHeader table (header/status per cost center + year)
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('sne.SnepHeader'))
BEGIN
	CREATE TABLE sne.SnepHeader
	(
		Id                  INT IDENTITY(1,1) PRIMARY KEY,
		FinancialYear       INT NOT NULL,
		CostCenterCode      NVARCHAR(50) NOT NULL,
		CostCenterDesc      NVARCHAR(500) NULL,
		DepartmentCode      NVARCHAR(50) NULL,
		Status              NVARCHAR(50) NOT NULL DEFAULT ('Draft'), -- Draft | Submitted
		CreatedByUserId     NVARCHAR(450) NULL,
		CreatedDate         DATETIME NOT NULL DEFAULT (GETDATE()),
		UpdatedByUserId     NVARCHAR(450) NULL,
		UpdatedDate         DATETIME NULL,
		SubmittedByUserId   NVARCHAR(450) NULL,
		SubmittedDate       DATETIME NULL,
		RevertedByUserId    NVARCHAR(450) NULL,
		RevertedDate        DATETIME NULL
	);
END
GO

-- ---------------------------------------------------------------------
-- 4. Create sne.snep table (line items, linked to SnepHeader)
-- ---------------------------------------------------------------------
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('sne.snep'))
BEGIN
	CREATE TABLE sne.snep
	(
		Id                          INT IDENTITY(1,1) PRIMARY KEY,
		Fund                        NVARCHAR(50) NULL,
		CostCenterCode              NVARCHAR(50) NULL,
		CostCenterDesc              NVARCHAR(500) NULL,
		glCode                      NVARCHAR(50) NULL,
		glDesc                      NVARCHAR(500) NULL,
		SubHeadCode                 NVARCHAR(50) NULL,
		PostCode                    NVARCHAR(50) NULL,
		Designation                 NVARCHAR(300) NULL,
		BS                          NVARCHAR(50) NULL,
		PostPrevious                BIGINT NULL,
		PostCurrent                 BIGINT NULL,
		ActualExpLast8Previous      BIGINT NULL,
		ActualLast4Current          BIGINT NULL,
		TotalEstimatedExpCurrent    BIGINT NULL,
		BudgetEstimateCurrent       BIGINT NULL,
		ProposedBudget1             BIGINT NULL,
		ProposedBudget2             BIGINT NULL,
		ProposedBudget3             BIGINT NULL,
		HeaderId                    INT NULL,
		FinancialYear               INT NULL,
		CONSTRAINT FK_Snep_SnepHeader FOREIGN KEY (HeaderId) REFERENCES sne.SnepHeader(Id)
	);
END
GO

-- ---------------------------------------------------------------------
-- 5. Widen text columns (fixes "String or binary data would be
--    truncated" errors seen with smaller column definitions).
-- ---------------------------------------------------------------------
IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'CostCenterCode')
	ALTER TABLE sne.SnepHeader ALTER COLUMN CostCenterCode NVARCHAR(50) NOT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'CostCenterDesc')
	ALTER TABLE sne.SnepHeader ALTER COLUMN CostCenterDesc NVARCHAR(500) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'DepartmentCode')
	ALTER TABLE sne.SnepHeader ALTER COLUMN DepartmentCode NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'Status')
	ALTER TABLE sne.SnepHeader ALTER COLUMN Status NVARCHAR(50) NOT NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'Fund')
	ALTER TABLE sne.snep ALTER COLUMN Fund NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'CostCenterCode')
	ALTER TABLE sne.snep ALTER COLUMN CostCenterCode NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'CostCenterDesc')
	ALTER TABLE sne.snep ALTER COLUMN CostCenterDesc NVARCHAR(500) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'glCode')
	ALTER TABLE sne.snep ALTER COLUMN glCode NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'glDesc')
	ALTER TABLE sne.snep ALTER COLUMN glDesc NVARCHAR(500) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'SubHeadCode')
	ALTER TABLE sne.snep ALTER COLUMN SubHeadCode NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'PostCode')
	ALTER TABLE sne.snep ALTER COLUMN PostCode NVARCHAR(50) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'Designation')
	ALTER TABLE sne.snep ALTER COLUMN Designation NVARCHAR(300) NULL;

IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'BS')
	ALTER TABLE sne.snep ALTER COLUMN BS NVARCHAR(50) NULL;
GO
