USE FinanceDashboardDB;
GO

IF NOT EXISTS (
	SELECT 1 FROM sys.columns
	WHERE object_id = OBJECT_ID('budget.CostCentersMapping') AND name = 'Sector'
)
BEGIN
	ALTER TABLE budget.CostCentersMapping ADD Sector VARCHAR(20) NULL;
END
GO

IF NOT EXISTS (
	SELECT 1 FROM sys.columns
	WHERE object_id = OBJECT_ID('budget.CostCentersMapping') AND name = 'DepartmentCode'
)
BEGIN
	ALTER TABLE budget.CostCentersMapping ADD DepartmentCode NVARCHAR(20) NULL;
END
GO

;WITH SourceData AS
(
	SELECT
		b.FundCenterCode,
		b.Sector,
		b.MinistryDepartment,
		ROW_NUMBER() OVER (PARTITION BY b.FundCenterCode ORDER BY (SELECT NULL)) AS rn
	FROM budget.BERData b
)
UPDATE ccm
SET
	ccm.Sector = sd.Sector,
	ccm.DepartmentCode = sd.MinistryDepartment
FROM budget.CostCentersMapping ccm
JOIN SourceData sd
	ON sd.FundCenterCode = ccm.CostCenter
	AND sd.rn = 1;
GO
