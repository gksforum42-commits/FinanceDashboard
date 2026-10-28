USE FinanceDashboardDB;
GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.schemas
	WHERE name = 'sne'
)
BEGIN
	EXEC('CREATE SCHEMA sne');
END
GO

IF NOT EXISTS (
	SELECT 1
	FROM sys.tables t
	JOIN sys.schemas s ON t.schema_id = s.schema_id
	WHERE s.name = 'sne' AND t.name = 'snep'
)
BEGIN
	CREATE TABLE sne.snep
	(
		Id INT IDENTITY(1,1) PRIMARY KEY,

		Fund NVARCHAR(7) NOT NULL,

		CostCenterCode NVARCHAR(7) NOT NULL,
		CostCenterDesc NVARCHAR(255) NULL,

		glCode NVARCHAR(6) NULL,
		glDesc NVARCHAR(255) NULL,

		SubHeadCode NVARCHAR(6) NULL,

		PostCode NVARCHAR(10) NULL,
		Designation NVARCHAR(100) NULL,
		BS NVARCHAR(25) NULL,

		PostPrevious BIGINT NULL,
		PostCurrent BIGINT NULL,

		ActualExpLast8Previous BIGINT NULL,
		ActualLast4Current BIGINT NULL,
		TotalEstimatedExpCurrent BIGINT NULL,

		BudgetEstimateCurrent BIGINT NULL,

		ProposedBudget1 BIGINT NULL,
		ProposedBudget2 BIGINT NULL,
		ProposedBudget3 BIGINT NULL
	);
END
GO
