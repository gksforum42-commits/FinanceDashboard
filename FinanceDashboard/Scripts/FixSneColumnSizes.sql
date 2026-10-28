-- Fixes "String or binary data would be truncated" errors on sne.SnepHeader / sne.snep
-- by widening the text columns. Safe to run multiple times.

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
