-- =====================================================================
-- Adds separate submission-tracking columns to sne.SnepHeader so both the
-- SNEDepartmentUser's submission and the DepartmentUser's (final) submission
-- can be recorded independently, supporting the two-stage SNE workflow:
--   Draft -> SubmittedBySNE (SNE user submitted, pending department review)
--         -> Submitted (Department user final submission)
-- Existing SubmittedByUserId/SubmittedDate columns are kept as-is and continue
-- to reflect the most recent submission (for backward compatibility with any
-- existing reports/queries).
-- Idempotent: safe to re-run.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'SneSubmittedByUserId')
	ALTER TABLE sne.SnepHeader ADD SneSubmittedByUserId NVARCHAR(450) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'SneSubmittedDate')
	ALTER TABLE sne.SnepHeader ADD SneSubmittedDate DATETIME NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'DeptSubmittedByUserId')
	ALTER TABLE sne.SnepHeader ADD DeptSubmittedByUserId NVARCHAR(450) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'DeptSubmittedDate')
	ALTER TABLE sne.SnepHeader ADD DeptSubmittedDate DATETIME NULL;
GO

-- RevertedByUserId / RevertedDate already exist per DeploySneModule.sql, but guard here too
-- in case this script is run on a database that predates that script.
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'RevertedByUserId')
	ALTER TABLE sne.SnepHeader ADD RevertedByUserId NVARCHAR(450) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'RevertedDate')
	ALTER TABLE sne.SnepHeader ADD RevertedDate DATETIME NULL;
GO
