-- =====================================================================
-- Adds a dedicated SneUserLocked flag to sne.SnepHeader, replacing the
-- previous (buggy) approach of inferring "locked from SNE user" from the
-- mere presence of DeptSubmittedByUserId. That column is never cleared by
-- a plain re-save or by the older revert flow, so any header that was ever
-- finally submitted in the past (including old test data) stayed
-- permanently locked for the SNE Department User, even in Draft status.
--
-- New behavior: SneUserLocked is only set to 1 when an Administrator
-- reverts a finally-Submitted header (per the business rule: after an
-- Admin revert, only the Budget Officer/Administrator can touch it again,
-- not the plain SNE Department User). A Budget-Officer-initiated revert of
-- a SubmittedBySNE (pending review) header does NOT set this flag, so the
-- SNE Department User keeps access to that Draft as normal.
--
-- Defaults to 0 for all existing rows, which fixes any records (e.g.
-- AK004197) that were incorrectly locked by the old inference logic.
-- Idempotent: safe to re-run.
-- =====================================================================

IF NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID('sne.SnepHeader') AND name = 'SneUserLocked')
	ALTER TABLE sne.SnepHeader ADD SneUserLocked BIT NOT NULL DEFAULT (0);
GO
