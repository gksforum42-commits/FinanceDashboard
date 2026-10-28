-- =====================================================================
-- Adds sne.snep_SneSnapshot: an immutable snapshot of the line items the
-- plain SNE Department User submitted (SubmittedBySNE), captured at the
-- moment of that submission.
--
-- Problem solved: sne.snep is a single "live/working" table. Once a Budget
-- Officer edits and re-saves the form, those edits overwrite the same rows
-- the SNE Department User originally entered, so the SNE user's own view
-- would otherwise show the Budget Officer's numbers instead of what they
-- themselves submitted.
--
-- Solution: whenever a plain SNE Department User submits (Status moves to
-- SubmittedBySNE), the current sne.snep rows for that header are copied
-- into sne.snep_SneSnapshot (replacing any previous snapshot for that
-- header). The plain SNE user's read-only view always reads from this
-- snapshot; the Budget Officer/Administrator continue to read/write the
-- live sne.snep table as before.
--
-- Idempotent: safe to re-run.
-- =====================================================================

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
