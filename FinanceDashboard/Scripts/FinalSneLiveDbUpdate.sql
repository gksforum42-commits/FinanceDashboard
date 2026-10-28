-- =====================================================================
-- FINAL combined SNE live-database update script.
-- Safe/idempotent to run multiple times against the live (MonsterASP) DB.
-- Combines:
--   1) AddSneHeaderSubmissionTrackingColumns.sql - two-stage submission
--      tracking columns (SneSubmittedBy/Date, DeptSubmittedBy/Date,
--      RevertedBy/Date).
--   2) AddSneUserLockedColumn.sql - SneUserLocked flag used to keep the
--      plain SNE Department User locked out after an Administrator revert
--      of a finally-Submitted header.
--   3) AddSneJustificationColumn.sql - Justification column on sne.snep
--      to store free-text justification notes per line item.
--   4) AddSneSnapshotTable.sql - sne.snep_SneSnapshot table storing an
--      immutable snapshot of the SNE Department User's submitted lines,
--      so their view never reflects later Budget Officer edits.
--   5) Data fix - resets SneUserLocked to 0 for any header that is not
--      currently in Draft status (the flag is only meaningful for Draft
--      rows that were reverted by an Administrator).
--
-- Run this against the live database (MonsterASP) connection:
--   Server=db64915.databaseasp.net;Database=db64915;...
-- =====================================================================

-- 1) Two-stage submission tracking columns
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

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'RevertedByUserId')
	ALTER TABLE sne.SnepHeader ADD RevertedByUserId NVARCHAR(450) NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'RevertedDate')
	ALTER TABLE sne.SnepHeader ADD RevertedDate DATETIME NULL;
GO

-- 2) SneUserLocked flag
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'SneUserLocked')
	ALTER TABLE sne.SnepHeader ADD SneUserLocked BIT NOT NULL DEFAULT (0);
GO

-- 3) Justification column on sne.snep (per line item)
IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.snep') AND name = 'Justification')
BEGIN
	ALTER TABLE sne.snep ADD Justification NVARCHAR(MAX) NULL;
END
GO

-- 4) sne.snep_SneSnapshot table (immutable snapshot of SNE user's submission)
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE object_id = OBJECT_ID('sne.snep_SneSnapshot'))
BEGIN
	CREATE TABLE sne.snep_SneSnapshot
	(
		Id                       INT IDENTITY(1,1) PRIMARY KEY,
		HeaderId                 INT NOT NULL,
		Fund                     NVARCHAR(50) NULL,
		glCode                   NVARCHAR(50) NULL,
		glDesc                   NVARCHAR(255) NULL,
		SubHeadCode              NVARCHAR(50) NULL,
		PostCode                 NVARCHAR(50) NULL,
		Designation              NVARCHAR(255) NULL,
		BS                       NVARCHAR(50) NULL,
		PostPrevious             BIGINT NULL,
		PostCurrent              BIGINT NULL,
		ActualExpLast8Previous   BIGINT NULL,
		ActualLast4Current       BIGINT NULL,
		TotalEstimatedExpCurrent BIGINT NULL,
		BudgetEstimateCurrent    BIGINT NULL,
		ProposedBudget1          BIGINT NULL,
		ProposedBudget2          BIGINT NULL,
		ProposedBudget3          BIGINT NULL,
		Justification            NVARCHAR(MAX) NULL,
		SnapshotDate             DATETIME NOT NULL DEFAULT GETDATE(),
		CONSTRAINT FK_snep_SneSnapshot_Header FOREIGN KEY (HeaderId) REFERENCES sne.SnepHeader(Id)
	);
	CREATE INDEX IX_snep_SneSnapshot_HeaderId ON sne.snep_SneSnapshot(HeaderId);
END
GO

-- 5) Data fix: SneUserLocked is only meaningful while a header is in Draft
-- status (it means "Admin reverted this Submitted form; only Budget
-- Officer/Admin may touch it"). Clear any stale/incorrect flag on
-- non-Draft rows (Submitted / SubmittedBySNE) so it doesn't leak into
-- other statuses.
UPDATE sne.SnepHeader
SET SneUserLocked = 0
WHERE SneUserLocked = 1
  AND Status <> 'Draft';
GO
