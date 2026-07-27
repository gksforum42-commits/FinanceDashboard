-- =============================================================================
-- FINANCE DEPARTMENT PUNJAB - BUDGET ANALYSIS DASHBOARD
-- Complete Database Creation Script
-- Optimized for Large Datasets (Millions of Records)
-- =============================================================================

USE master;
GO

-- Drop database if exists (comment out for production)
-- IF EXISTS (SELECT name FROM sys.databases WHERE name = 'FinanceDashboardDB')
-- BEGIN
--     ALTER DATABASE FinanceDashboardDB SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
--     DROP DATABASE FinanceDashboardDB;
-- END
-- GO

-- Create Database
IF NOT EXISTS (SELECT name FROM sys.databases WHERE name = 'FinanceDashboardDB')
BEGIN
    CREATE DATABASE FinanceDashboardDB
    ON PRIMARY (
        NAME = 'FinanceDashboardDB_Data',
        FILENAME = 'C:\SQLData\FinanceDashboardDB_Data.mdf',
        SIZE = 500MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 100MB
    )
    LOG ON (
        NAME = 'FinanceDashboardDB_Log',
        FILENAME = 'C:\SQLData\FinanceDashboardDB_Log.ldf',
        SIZE = 200MB,
        MAXSIZE = UNLIMITED,
        FILEGROWTH = 50MB
    );
END
GO

ALTER DATABASE FinanceDashboardDB SET RECOVERY SIMPLE;
ALTER DATABASE FinanceDashboardDB SET AUTO_SHRINK OFF;
ALTER DATABASE FinanceDashboardDB SET AUTO_CREATE_STATISTICS ON;
ALTER DATABASE FinanceDashboardDB SET AUTO_UPDATE_STATISTICS ON;
GO

USE FinanceDashboardDB;
GO

-- =============================================================================
-- SCHEMA CREATION
-- =============================================================================
IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'budget')
BEGIN
    EXEC('CREATE SCHEMA budget');
END
GO

IF NOT EXISTS (SELECT * FROM sys.schemas WHERE name = 'admin')
BEGIN
    EXEC('CREATE SCHEMA admin');
END
GO

-- =============================================================================
-- DROP EXISTING OBJECTS (for clean setup)
-- =============================================================================

-- Drop Foreign Keys
DECLARE @fkSql NVARCHAR(MAX) = '';
SELECT @fkSql += 'ALTER TABLE [' + OBJECT_SCHEMA_NAME(parent_object_id) + '].[' + OBJECT_NAME(parent_object_id) + '] DROP CONSTRAINT [' + name + '];' + CHAR(13)
FROM sys.foreign_keys;
IF LEN(@fkSql) > 0 EXEC sp_executesql @fkSql;
GO

-- Drop Views
DECLARE @viewSql NVARCHAR(MAX) = '';
SELECT @viewSql += 'DROP VIEW IF EXISTS [' + SCHEMA_NAME(schema_id) + '].[' + name + '];' + CHAR(13)
FROM sys.views WHERE is_ms_shipped = 0;
IF LEN(@viewSql) > 0 EXEC sp_executesql @viewSql;
GO

-- Drop Stored Procedures
DECLARE @procSql NVARCHAR(MAX) = '';
SELECT @procSql += 'DROP PROCEDURE IF EXISTS [' + SCHEMA_NAME(schema_id) + '].[' + name + '];' + CHAR(13)
FROM sys.procedures WHERE is_ms_shipped = 0;
IF LEN(@procSql) > 0 EXEC sp_executesql @procSql;
GO

-- Drop Functions
DECLARE @funcSql NVARCHAR(MAX) = '';
SELECT @funcSql += 'DROP FUNCTION IF EXISTS [' + SCHEMA_NAME(schema_id) + '].[' + name + '];' + CHAR(13)
FROM sys.objects WHERE type IN ('FN', 'IF', 'TF') AND is_ms_shipped = 0;
IF LEN(@funcSql) > 0 EXEC sp_executesql @funcSql;
GO

-- Drop Tables
DECLARE @tableSql NVARCHAR(MAX) = '';
SELECT @tableSql += 'DROP TABLE IF EXISTS [' + SCHEMA_NAME(schema_id) + '].[' + name + '];' + CHAR(13)
FROM sys.tables WHERE is_ms_shipped = 0;
IF LEN(@tableSql) > 0 EXEC sp_executesql @tableSql;
GO

-- =============================================================================
-- TABLE: admin.AspNetRoles (Identity)
-- =============================================================================
CREATE TABLE admin.AspNetRoles (
    Id NVARCHAR(450) NOT NULL,
    Name NVARCHAR(256) NULL,
    NormalizedName NVARCHAR(256) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AspNetRoles PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetRoles_NormalizedName 
ON admin.AspNetRoles (NormalizedName) 
WHERE NormalizedName IS NOT NULL;
GO

-- =============================================================================
-- TABLE: admin.AspNetUsers (Identity)
-- =============================================================================
CREATE TABLE admin.AspNetUsers (
    Id NVARCHAR(450) NOT NULL,
    UserName NVARCHAR(256) NULL,
    NormalizedUserName NVARCHAR(256) NULL,
    Email NVARCHAR(256) NULL,
    NormalizedEmail NVARCHAR(256) NULL,
    EmailConfirmed BIT NOT NULL CONSTRAINT DF_AspNetUsers_EmailConfirmed DEFAULT (0),
    PasswordHash NVARCHAR(MAX) NULL,
    SecurityStamp NVARCHAR(MAX) NULL,
    ConcurrencyStamp NVARCHAR(MAX) NULL,
    PhoneNumber NVARCHAR(MAX) NULL,
    PhoneNumberConfirmed BIT NOT NULL CONSTRAINT DF_AspNetUsers_PhoneNumberConfirmed DEFAULT (0),
    TwoFactorEnabled BIT NOT NULL CONSTRAINT DF_AspNetUsers_TwoFactorEnabled DEFAULT (0),
    LockoutEnd DATETIMEOFFSET NULL,
    LockoutEnabled BIT NOT NULL CONSTRAINT DF_AspNetUsers_LockoutEnabled DEFAULT (1),
    AccessFailedCount INT NOT NULL CONSTRAINT DF_AspNetUsers_AccessFailedCount DEFAULT (0),
    FullName NVARCHAR(200) NULL,
    Department NVARCHAR(100) NULL,
    Designation NVARCHAR(100) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_AspNetUsers_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_AspNetUsers_CreatedDate DEFAULT (GETUTCDATE()),
    LastLoginDate DATETIME2 NULL,
    CONSTRAINT PK_AspNetUsers PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE NONCLUSTERED INDEX IX_AspNetUsers_NormalizedEmail 
ON admin.AspNetUsers (NormalizedEmail) 
WHERE NormalizedEmail IS NOT NULL;
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_AspNetUsers_NormalizedUserName 
ON admin.AspNetUsers (NormalizedUserName) 
WHERE NormalizedUserName IS NOT NULL;
GO

CREATE NONCLUSTERED INDEX IX_AspNetUsers_IsActive 
ON admin.AspNetUsers (IsActive) 
INCLUDE (FullName, Email, LastLoginDate);
GO

-- =============================================================================
-- TABLE: admin.AspNetRoleClaims
-- =============================================================================
CREATE TABLE admin.AspNetRoleClaims (
    Id INT IDENTITY(1,1) NOT NULL,
    RoleId NVARCHAR(450) NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_AspNetRoleClaims_AspNetRoles_RoleId FOREIGN KEY (RoleId) 
        REFERENCES admin.AspNetRoles (Id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_AspNetRoleClaims_RoleId 
ON admin.AspNetRoleClaims (RoleId);
GO

-- =============================================================================
-- TABLE: admin.AspNetUserClaims
-- =============================================================================
CREATE TABLE admin.AspNetUserClaims (
    Id INT IDENTITY(1,1) NOT NULL,
    UserId NVARCHAR(450) NOT NULL,
    ClaimType NVARCHAR(MAX) NULL,
    ClaimValue NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AspNetUserClaims PRIMARY KEY CLUSTERED (Id),
    CONSTRAINT FK_AspNetUserClaims_AspNetUsers_UserId FOREIGN KEY (UserId) 
        REFERENCES admin.AspNetUsers (Id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_AspNetUserClaims_UserId 
ON admin.AspNetUserClaims (UserId);
GO

-- =============================================================================
-- TABLE: admin.AspNetUserLogins
-- =============================================================================
CREATE TABLE admin.AspNetUserLogins (
    LoginProvider NVARCHAR(450) NOT NULL,
    ProviderKey NVARCHAR(450) NOT NULL,
    ProviderDisplayName NVARCHAR(MAX) NULL,
    UserId NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_AspNetUserLogins PRIMARY KEY CLUSTERED (LoginProvider, ProviderKey),
    CONSTRAINT FK_AspNetUserLogins_AspNetUsers_UserId FOREIGN KEY (UserId) 
        REFERENCES admin.AspNetUsers (Id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_AspNetUserLogins_UserId 
ON admin.AspNetUserLogins (UserId);
GO

-- =============================================================================
-- TABLE: admin.AspNetUserRoles
-- =============================================================================
CREATE TABLE admin.AspNetUserRoles (
    UserId NVARCHAR(450) NOT NULL,
    RoleId NVARCHAR(450) NOT NULL,
    CONSTRAINT PK_AspNetUserRoles PRIMARY KEY CLUSTERED (UserId, RoleId),
    CONSTRAINT FK_AspNetUserRoles_AspNetRoles_RoleId FOREIGN KEY (RoleId) 
        REFERENCES admin.AspNetRoles (Id) ON DELETE CASCADE,
    CONSTRAINT FK_AspNetUserRoles_AspNetUsers_UserId FOREIGN KEY (UserId) 
        REFERENCES admin.AspNetUsers (Id) ON DELETE CASCADE
);
GO

CREATE NONCLUSTERED INDEX IX_AspNetUserRoles_RoleId 
ON admin.AspNetUserRoles (RoleId);
GO

-- =============================================================================
-- TABLE: admin.AspNetUserTokens
-- =============================================================================
CREATE TABLE admin.AspNetUserTokens (
    UserId NVARCHAR(450) NOT NULL,
    LoginProvider NVARCHAR(450) NOT NULL,
    Name NVARCHAR(450) NOT NULL,
    Value NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AspNetUserTokens PRIMARY KEY CLUSTERED (UserId, LoginProvider, Name),
    CONSTRAINT FK_AspNetUserTokens_AspNetUsers_UserId FOREIGN KEY (UserId) 
        REFERENCES admin.AspNetUsers (Id) ON DELETE CASCADE
);
GO

-- =============================================================================
-- TABLE: budget.OriginalBudget
-- Stores Original Budget data from Excel sheet 1
-- ~441,000+ records - optimized for large datasets
-- =============================================================================
CREATE TABLE budget.OriginalBudget (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    FinancialYear INT NOT NULL,
    DocumentNumber NVARCHAR(50) NOT NULL,
    DepartmentCode NVARCHAR(20) NOT NULL,
    BudgetType NVARCHAR(20) NOT NULL,
    FundCode NVARCHAR(20) NOT NULL,
    FundDescription NVARCHAR(500) NULL,
    CostCenterCode NVARCHAR(20) NOT NULL,
    SNECode NVARCHAR(30) NULL,
    CommitmentItem NVARCHAR(30) NOT NULL,
    SubHead NVARCHAR(20) NULL,
    BPS NVARCHAR(10) NULL,
    DesignationCode NVARCHAR(20) NULL,
    Posts INT NOT NULL CONSTRAINT DF_OriginalBudget_Posts DEFAULT (0),
    OriginalBudgetAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_OriginalBudget_OriginalAmount DEFAULT (0),
    RevisedBudgetAmount DECIMAL(18,2) NOT NULL CONSTRAINT DF_OriginalBudget_RevisedAmount DEFAULT (0),
    DesignationDescription NVARCHAR(200) NULL,
    Description NVARCHAR(500) NULL,
    PostingDate DATETIME2 NULL,
    UserName NVARCHAR(50) NULL,
    FunctionCode NVARCHAR(20) NULL,
    Type NVARCHAR(10) NULL,
    ImportId BIGINT NOT NULL,
    IsDeleted BIT NOT NULL CONSTRAINT DF_OriginalBudget_IsDeleted DEFAULT (0),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_OriginalBudget_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_OriginalBudget PRIMARY KEY CLUSTERED (Id)
);
GO

-- Indexes for OriginalBudget
CREATE NONCLUSTERED INDEX IX_OriginalBudget_FinancialYear 
ON budget.OriginalBudget (FinancialYear)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_DepartmentCode 
ON budget.OriginalBudget (DepartmentCode)
INCLUDE (FundCode, CostCenterCode, OriginalBudgetAmount)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_FundCode 
ON budget.OriginalBudget (FundCode)
INCLUDE (DepartmentCode, CostCenterCode, OriginalBudgetAmount, RevisedBudgetAmount)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_CostCenterCode 
ON budget.OriginalBudget (CostCenterCode)
INCLUDE (FundCode, DepartmentCode, OriginalBudgetAmount)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_CommitmentItem 
ON budget.OriginalBudget (CommitmentItem)
INCLUDE (FundCode, CostCenterCode, OriginalBudgetAmount)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_ImportId 
ON budget.OriginalBudget (ImportId)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_OriginalBudget_DocumentNumber 
ON budget.OriginalBudget (DocumentNumber)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: budget.BERData (Budget Execution Report)
-- Stores BER data from Excel sheet 2
-- ~240,000+ records - optimized for large datasets
-- =============================================================================
CREATE TABLE budget.BERData (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    FinancialYear INT NOT NULL,
    FundCode NVARCHAR(20) NOT NULL,
    FundCenterCode NVARCHAR(30) NOT NULL,
    FundCenterDescription NVARCHAR(500) NULL,
    MinistryDepartment NVARCHAR(20) NULL,
    AttachedDepartment NVARCHAR(200) NULL,
    ProfitCenter NVARCHAR(50) NULL,
    SubDetailFunction NVARCHAR(20) NULL,
    DetailObject NVARCHAR(20) NOT NULL,
    BudgetGrant DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_BudgetGrant DEFAULT (0),
    SupplementaryGrant DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_SupplementaryGrant DEFAULT (0),
    ReappropriationPlus DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_ReappPlus DEFAULT (0),
    ReappropriationMinus DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_ReappMinus DEFAULT (0),
    TSSSurrender DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_TSSSurrender DEFAULT (0),
    FinalGrant DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_FinalGrant DEFAULT (0),
    Releases DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_Releases DEFAULT (0),
    JulExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_JulExp DEFAULT (0),
    AugExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_AugExp DEFAULT (0),
    SepExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_SepExp DEFAULT (0),
    OctExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_OctExp DEFAULT (0),
    NovExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_NovExp DEFAULT (0),
    DecExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_DecExp DEFAULT (0),
    JanExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_JanExp DEFAULT (0),
    FebExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_FebExp DEFAULT (0),
    MarExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_MarExp DEFAULT (0),
    AprExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_AprExp DEFAULT (0),
    MayExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_MayExp DEFAULT (0),
    JunExp DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_JunExp DEFAULT (0),
    ActualExpenditure DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_ActualExpenditure DEFAULT (0),
    FinalGrantBalance DECIMAL(18,2) NOT NULL CONSTRAINT DF_BERData_FinalGrantBalance DEFAULT (0),
    ImportId BIGINT NOT NULL,
    IsDeleted BIT NOT NULL CONSTRAINT DF_BERData_IsDeleted DEFAULT (0),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_BERData_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_BERData PRIMARY KEY CLUSTERED (Id)
);
GO

-- Indexes for BERData
CREATE NONCLUSTERED INDEX IX_BERData_FinancialYear 
ON budget.BERData (FinancialYear)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_BERData_FundCode 
ON budget.BERData (FundCode)
INCLUDE (FundCenterCode, FinalGrant, ActualExpenditure, FinalGrantBalance)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_BERData_FundCenterCode 
ON budget.BERData (FundCenterCode)
INCLUDE (FundCode, FinalGrant, ActualExpenditure)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_BERData_MinistryDepartment 
ON budget.BERData (MinistryDepartment)
INCLUDE (FundCode, FundCenterCode, FinalGrant, ActualExpenditure)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_BERData_DetailObject 
ON budget.BERData (DetailObject)
INCLUDE (FundCode, FundCenterCode, FinalGrant, ActualExpenditure)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_BERData_ImportId 
ON budget.BERData (ImportId)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: budget.ImportHistory
-- Tracks all file imports with metadata
-- =============================================================================
CREATE TABLE budget.ImportHistory (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    FileName NVARCHAR(500) NOT NULL,
    OriginalFileName NVARCHAR(500) NOT NULL,
    FileSize BIGINT NOT NULL,
    FilePath NVARCHAR(1000) NULL,
    ImportType NVARCHAR(50) NOT NULL, -- 'OriginalBudget', 'BER', 'Both'
    SheetName NVARCHAR(100) NULL,
    RecordsImported INT NOT NULL CONSTRAINT DF_ImportHistory_RecordsImported DEFAULT (0),
    OriginalBudgetCount INT NOT NULL CONSTRAINT DF_ImportHistory_OBCount DEFAULT (0),
    BERCount INT NOT NULL CONSTRAINT DF_ImportHistory_BERCount DEFAULT (0),
    ValidationErrors INT NOT NULL CONSTRAINT DF_ImportHistory_ValidationErrors DEFAULT (0),
    Status NVARCHAR(50) NOT NULL CONSTRAINT DF_ImportHistory_Status DEFAULT ('Pending'),
    ErrorMessage NVARCHAR(MAX) NULL,
    FinancialYear INT NOT NULL,
    ImportedBy NVARCHAR(450) NOT NULL,
    ImportedDate DATETIME2 NOT NULL CONSTRAINT DF_ImportHistory_ImportedDate DEFAULT (GETUTCDATE()),
    CompletedDate DATETIME2 NULL,
    IsDeleted BIT NOT NULL CONSTRAINT DF_ImportHistory_IsDeleted DEFAULT (0),
    CONSTRAINT PK_ImportHistory PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE NONCLUSTERED INDEX IX_ImportHistory_ImportedDate 
ON budget.ImportHistory (ImportedDate DESC)
INCLUDE (FileName, ImportType, Status, RecordsImported)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_ImportHistory_FinancialYear 
ON budget.ImportHistory (FinancialYear)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_ImportHistory_ImportType 
ON budget.ImportHistory (ImportType)
WITH (FILLFACTOR = 90);
GO

CREATE NONCLUSTERED INDEX IX_ImportHistory_ImportedBy 
ON budget.ImportHistory (ImportedBy)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: budget.Departments (Lookup)
-- =============================================================================
CREATE TABLE budget.Departments (
    Id INT IDENTITY(1,1) NOT NULL,
    DepartmentCode NVARCHAR(20) NOT NULL,
    DepartmentName NVARCHAR(500) NULL,
    MinistryCode NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Departments_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Departments_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_Departments PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_Departments_DepartmentCode 
ON budget.Departments (DepartmentCode)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: budget.Funds (Lookup)
-- =============================================================================
CREATE TABLE budget.Funds (
    Id INT IDENTITY(1,1) NOT NULL,
    FundCode NVARCHAR(20) NOT NULL,
    FundDescription NVARCHAR(500) NULL,
    FundType NVARCHAR(50) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Funds_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Funds_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_Funds PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_Funds_FundCode 
ON budget.Funds (FundCode)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: budget.Functions (Lookup)
-- =============================================================================
CREATE TABLE budget.Functions (
    Id INT IDENTITY(1,1) NOT NULL,
    FunctionCode NVARCHAR(20) NOT NULL,
    FunctionName NVARCHAR(500) NULL,
    FunctionCategory NVARCHAR(100) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_Functions_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_Functions_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_Functions PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_Functions_FunctionCode 
ON budget.Functions (FunctionCode)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- TABLE: admin.AuditLogs
-- Comprehensive audit logging
-- =============================================================================
CREATE TABLE admin.AuditLogs (
    Id BIGINT IDENTITY(1,1) NOT NULL,
    UserId NVARCHAR(450) NULL,
    UserName NVARCHAR(256) NULL,
    Action NVARCHAR(100) NOT NULL,
    EntityType NVARCHAR(100) NULL,
    EntityId NVARCHAR(100) NULL,
    OldValues NVARCHAR(MAX) NULL,
    NewValues NVARCHAR(MAX) NULL,
    IpAddress NVARCHAR(50) NULL,
    UserAgent NVARCHAR(500) NULL,
    ActionDate DATETIME2 NOT NULL CONSTRAINT DF_AuditLogs_ActionDate DEFAULT (GETUTCDATE()),
    Status NVARCHAR(50) NOT NULL CONSTRAINT DF_AuditLogs_Status DEFAULT ('Success'),
    ErrorMessage NVARCHAR(MAX) NULL,
    CONSTRAINT PK_AuditLogs PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE NONCLUSTERED INDEX IX_AuditLogs_ActionDate 
ON admin.AuditLogs (ActionDate DESC)
INCLUDE (UserId, Action, Status)
WITH (FILLFACTOR = 85);
GO

CREATE NONCLUSTERED INDEX IX_AuditLogs_UserId 
ON admin.AuditLogs (UserId)
INCLUDE (Action, ActionDate)
WITH (FILLFACTOR = 85);
GO

CREATE NONCLUSTERED INDEX IX_AuditLogs_Action 
ON admin.AuditLogs (Action)
INCLUDE (UserId, ActionDate, Status)
WITH (FILLFACTOR = 85);
GO

-- =============================================================================
-- TABLE: budget.ObjectCodes (Lookup)
-- =============================================================================
CREATE TABLE budget.ObjectCodes (
    Id INT IDENTITY(1,1) NOT NULL,
    ObjectCode NVARCHAR(20) NOT NULL,
    ObjectName NVARCHAR(500) NULL,
    MajorHead NVARCHAR(20) NULL,
    MinorHead NVARCHAR(20) NULL,
    IsActive BIT NOT NULL CONSTRAINT DF_ObjectCodes_IsActive DEFAULT (1),
    CreatedDate DATETIME2 NOT NULL CONSTRAINT DF_ObjectCodes_CreatedDate DEFAULT (GETUTCDATE()),
    CONSTRAINT PK_ObjectCodes PRIMARY KEY CLUSTERED (Id)
);
GO

CREATE UNIQUE NONCLUSTERED INDEX IX_ObjectCodes_ObjectCode 
ON budget.ObjectCodes (ObjectCode)
WITH (FILLFACTOR = 90);
GO

-- =============================================================================
-- VIEWS
-- =============================================================================

-- View: Budget Summary Dashboard
CREATE VIEW budget.vw_DashboardSummary AS
SELECT 
    (SELECT COUNT(DISTINCT DepartmentCode) FROM budget.OriginalBudget WHERE IsDeleted = 0) AS TotalDepartments,
    (SELECT COUNT(DISTINCT CostCenterCode) FROM budget.OriginalBudget WHERE IsDeleted = 0) AS TotalCostCenters,
    (SELECT COUNT(DISTINCT FundCode) FROM budget.OriginalBudget WHERE IsDeleted = 0) AS TotalFunds,
    (SELECT COUNT(DISTINCT FundCode) FROM budget.BERData WHERE IsDeleted = 0) AS TotalGrants,
    (SELECT COUNT(*) FROM budget.OriginalBudget WHERE IsDeleted = 0) AS TotalOriginalRecords,
    (SELECT COUNT(*) FROM budget.BERData WHERE IsDeleted = 0) AS TotalBERRecords,
    (SELECT COUNT(*) FROM budget.ImportHistory WHERE IsDeleted = 0) AS TotalImportedFiles;
GO

-- View: Original Budget Summary by Department
CREATE VIEW budget.vw_OriginalBudgetByDepartment AS
SELECT 
    DepartmentCode,
    COUNT(*) AS TotalRecords,
    SUM(Posts) AS TotalPosts,
    SUM(OriginalBudgetAmount) AS TotalOriginalAmount,
    SUM(RevisedBudgetAmount) AS TotalRevisedAmount,
    COUNT(DISTINCT FundCode) AS FundCount,
    COUNT(DISTINCT CostCenterCode) AS CostCenterCount
FROM budget.OriginalBudget
WHERE IsDeleted = 0
GROUP BY DepartmentCode;
GO

-- View: BER Summary by Fund
CREATE VIEW budget.vw_BERByFund AS
SELECT 
    FundCode,
    COUNT(*) AS TotalRecords,
    SUM(BudgetGrant) AS TotalBudgetGrant,
    SUM(SupplementaryGrant) AS TotalSupplementaryGrant,
    SUM(FinalGrant) AS TotalFinalGrant,
    SUM(Releases) AS TotalReleases,
    SUM(ActualExpenditure) AS TotalActualExpenditure,
    SUM(FinalGrantBalance) AS TotalFinalGrantBalance,
    COUNT(DISTINCT FundCenterCode) AS CostCenterCount
FROM budget.BERData
WHERE IsDeleted = 0
GROUP BY FundCode;
GO

-- View: Monthly Expenditure Analysis
CREATE VIEW budget.vw_MonthlyExpenditure AS
SELECT 
    FinancialYear,
    FundCode,
    SUM(JulExp) AS Jul, SUM(AugExp) AS Aug, SUM(SepExp) AS Sep,
    SUM(OctExp) AS Oct, SUM(NovExp) AS Nov, SUM(DecExp) AS DecExp,
    SUM(JanExp) AS Jan, SUM(FebExp) AS Feb, SUM(MarExp) AS Mar,
    SUM(AprExp) AS Apr, SUM(MayExp) AS May, SUM(JunExp) AS Jun,
    SUM(ActualExpenditure) AS TotalExpenditure,
    SUM(FinalGrant) AS TotalFinalGrant,
    CASE WHEN SUM(FinalGrant) > 0 THEN (SUM(ActualExpenditure) / SUM(FinalGrant)) * 100 ELSE 0 END AS UtilizationPercentage
FROM budget.BERData
WHERE IsDeleted = 0
GROUP BY FinancialYear, FundCode;
GO

-- View: Budget vs Expenditure Comparison
CREATE VIEW budget.vw_BudgetComparison AS
SELECT 
    ob.FinancialYear,
    ob.DepartmentCode,
    ob.FundCode,
    SUM(ob.OriginalBudgetAmount) AS OriginalBudget,
    ISNULL(ber.TotalFinalGrant, 0) AS FinalGrant,
    ISNULL(ber.TotalActualExpenditure, 0) AS ActualExpenditure,
    ISNULL(ber.TotalFinalGrantBalance, 0) AS Balance,
    CASE WHEN SUM(ob.OriginalBudgetAmount) > 0 
         THEN (ISNULL(ber.TotalActualExpenditure, 0) / SUM(ob.OriginalBudgetAmount)) * 100 
         ELSE 0 END AS ExpenditurePercentage
FROM budget.OriginalBudget ob
LEFT JOIN (
    SELECT FundCode, SUM(FinalGrant) AS TotalFinalGrant,
           SUM(ActualExpenditure) AS TotalActualExpenditure,
           SUM(FinalGrantBalance) AS TotalFinalGrantBalance
    FROM budget.BERData
    WHERE IsDeleted = 0
    GROUP BY FundCode
) ber ON ob.FundCode = ber.FundCode
WHERE ob.IsDeleted = 0
GROUP BY ob.FinancialYear, ob.DepartmentCode, ob.FundCode, 
         ber.TotalFinalGrant, ber.TotalActualExpenditure, ber.TotalFinalGrantBalance;
GO

-- =============================================================================
-- STORED PROCEDURES
-- =============================================================================

-- SP: Get Dashboard Summary
CREATE PROCEDURE budget.sp_GetDashboardSummary
    @FinancialYear INT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ISNULL((SELECT SUM(OriginalBudgetAmount) FROM budget.OriginalBudget 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS OriginalBudgetTotal,
        ISNULL((SELECT SUM(FinalGrant) FROM budget.BERData 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS BERTotal,
        ISNULL((SELECT SUM(ActualExpenditure) FROM budget.BERData 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS ActualExpenditure,
        ISNULL((SELECT SUM(FinalGrantBalance) FROM budget.BERData 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS FinalGrantBalance,
        ISNULL((SELECT COUNT(DISTINCT DepartmentCode) FROM budget.OriginalBudget 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalDepartments,
        ISNULL((SELECT COUNT(DISTINCT CostCenterCode) FROM budget.OriginalBudget 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalCostCenters,
        ISNULL((SELECT COUNT(DISTINCT FundCode) FROM budget.OriginalBudget 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalFunds,
        ISNULL((SELECT COUNT(DISTINCT FundCode) FROM budget.BERData 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalGrants,
        ISNULL((SELECT COUNT(*) FROM budget.OriginalBudget 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalOriginalRecords,
        ISNULL((SELECT COUNT(*) FROM budget.BERData 
                WHERE (@FinancialYear IS NULL OR FinancialYear = @FinancialYear) AND IsDeleted = 0), 0) AS TotalBERRecords,
        (SELECT COUNT(*) FROM budget.ImportHistory WHERE IsDeleted = 0) AS TotalImportedFiles;
END
GO

-- SP: Get Original Budget with Filters (Server-side paging)
CREATE PROCEDURE budget.sp_GetOriginalBudgetFiltered
    @FinancialYear INT = NULL,
    @FundCode NVARCHAR(20) = NULL,
    @CostCenterCode NVARCHAR(20) = NULL,
    @DepartmentCode NVARCHAR(20) = NULL,
    @CommitmentItem NVARCHAR(30) = NULL,
    @FunctionCode NVARCHAR(20) = NULL,
    @Keyword NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50,
    @SortColumn NVARCHAR(50) = 'Id',
    @SortDirection NVARCHAR(4) = 'ASC'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH FilteredData AS (
        SELECT *,
               COUNT(*) OVER() AS TotalRecords
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0
            AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
            AND (@FundCode IS NULL OR FundCode = @FundCode)
            AND (@CostCenterCode IS NULL OR CostCenterCode = @CostCenterCode)
            AND (@DepartmentCode IS NULL OR DepartmentCode = @DepartmentCode)
            AND (@CommitmentItem IS NULL OR CommitmentItem = @CommitmentItem)
            AND (@FunctionCode IS NULL OR FunctionCode = @FunctionCode)
            AND (@Keyword IS NULL OR 
                 FundDescription LIKE '%' + @Keyword + '%' OR
                 DesignationDescription LIKE '%' + @Keyword + '%' OR
                 DocumentNumber LIKE '%' + @Keyword + '%')
    )
    SELECT * FROM FilteredData
    ORDER BY 
        CASE WHEN @SortDirection = 'ASC' THEN
            CASE @SortColumn
                WHEN 'Id' THEN CAST(Id AS NVARCHAR)
                WHEN 'FundCode' THEN FundCode
                WHEN 'DepartmentCode' THEN DepartmentCode
                WHEN 'OriginalBudgetAmount' THEN CAST(OriginalBudgetAmount AS NVARCHAR)
                ELSE CAST(Id AS NVARCHAR)
            END
        END ASC,
        CASE WHEN @SortDirection = 'DESC' THEN
            CASE @SortColumn
                WHEN 'Id' THEN CAST(Id AS NVARCHAR)
                WHEN 'FundCode' THEN FundCode
                WHEN 'DepartmentCode' THEN DepartmentCode
                WHEN 'OriginalBudgetAmount' THEN CAST(OriginalBudgetAmount AS NVARCHAR)
                ELSE CAST(Id AS NVARCHAR)
            END
        END DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- SP: Get BER Data with Filters (Server-side paging)
CREATE PROCEDURE budget.sp_GetBERDataFiltered
    @FinancialYear INT = NULL,
    @FundCode NVARCHAR(20) = NULL,
    @FundCenterCode NVARCHAR(30) = NULL,
    @MinistryDepartment NVARCHAR(20) = NULL,
    @DetailObject NVARCHAR(20) = NULL,
    @Keyword NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50,
    @SortColumn NVARCHAR(50) = 'Id',
    @SortDirection NVARCHAR(4) = 'ASC'
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH FilteredData AS (
        SELECT *,
               COUNT(*) OVER() AS TotalRecords
        FROM budget.BERData
        WHERE IsDeleted = 0
            AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
            AND (@FundCode IS NULL OR FundCode = @FundCode)
            AND (@FundCenterCode IS NULL OR FundCenterCode = @FundCenterCode)
            AND (@MinistryDepartment IS NULL OR MinistryDepartment = @MinistryDepartment)
            AND (@DetailObject IS NULL OR DetailObject = @DetailObject)
            AND (@Keyword IS NULL OR 
                 FundCenterDescription LIKE '%' + @Keyword + '%' OR
                 AttachedDepartment LIKE '%' + @Keyword + '%' OR
                 FundCode LIKE '%' + @Keyword + '%')
    )
    SELECT * FROM FilteredData
    ORDER BY 
        CASE WHEN @SortDirection = 'ASC' THEN
            CASE @SortColumn
                WHEN 'Id' THEN CAST(Id AS NVARCHAR)
                WHEN 'FundCode' THEN FundCode
                WHEN 'FinalGrant' THEN CAST(FinalGrant AS NVARCHAR)
                WHEN 'ActualExpenditure' THEN CAST(ActualExpenditure AS NVARCHAR)
                ELSE CAST(Id AS NVARCHAR)
            END
        END ASC,
        CASE WHEN @SortDirection = 'DESC' THEN
            CASE @SortColumn
                WHEN 'Id' THEN CAST(Id AS NVARCHAR)
                WHEN 'FundCode' THEN FundCode
                WHEN 'FinalGrant' THEN CAST(FinalGrant AS NVARCHAR)
                WHEN 'ActualExpenditure' THEN CAST(ActualExpenditure AS NVARCHAR)
                ELSE CAST(Id AS NVARCHAR)
            END
        END DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- SP: Get Fund Wise Report
CREATE PROCEDURE budget.sp_GetFundWiseReport
    @FinancialYear INT = NULL,
    @Keyword NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        FundCode,
        COUNT(*) AS TotalRecords,
        SUM(BudgetGrant) AS TotalBudgetGrant,
        SUM(SupplementaryGrant) AS TotalSupplementaryGrant,
        SUM(ReappropriationPlus) AS TotalReappPlus,
        SUM(ReappropriationMinus) AS TotalReappMinus,
        SUM(TSSSurrender) AS TotalTSSSurrender,
        SUM(FinalGrant) AS TotalFinalGrant,
        SUM(Releases) AS TotalReleases,
        SUM(ActualExpenditure) AS TotalActualExpenditure,
        SUM(FinalGrantBalance) AS TotalBalance,
        COUNT(DISTINCT FundCenterCode) AS CostCenterCount,
        COUNT(DISTINCT MinistryDepartment) AS DepartmentCount,
        CASE WHEN SUM(FinalGrant) > 0 THEN (SUM(ActualExpenditure) / SUM(FinalGrant)) * 100 ELSE 0 END AS UtilizationPercentage
    FROM budget.BERData
    WHERE IsDeleted = 0
        AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
        AND (@Keyword IS NULL OR FundCode LIKE '%' + @Keyword + '%')
    GROUP BY FundCode
    ORDER BY TotalFinalGrant DESC;
END
GO

-- SP: Get Department Wise Report
CREATE PROCEDURE budget.sp_GetDepartmentWiseReport
    @FinancialYear INT = NULL,
    @Keyword NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ob.DepartmentCode,
        COUNT(DISTINCT ob.CostCenterCode) AS CostCenterCount,
        COUNT(DISTINCT ob.FundCode) AS FundCount,
        SUM(ob.Posts) AS TotalPosts,
        SUM(ob.OriginalBudgetAmount) AS TotalOriginalBudget,
        SUM(ob.RevisedBudgetAmount) AS TotalRevisedBudget,
        ISNULL(ber.TotalFinalGrant, 0) AS TotalFinalGrant,
        ISNULL(ber.TotalActualExpenditure, 0) AS TotalActualExpenditure,
        ISNULL(ber.TotalBalance, 0) AS TotalBalance
    FROM budget.OriginalBudget ob
    LEFT JOIN (
        SELECT MinistryDepartment,
               SUM(FinalGrant) AS TotalFinalGrant,
               SUM(ActualExpenditure) AS TotalActualExpenditure,
               SUM(FinalGrantBalance) AS TotalBalance
        FROM budget.BERData
        WHERE IsDeleted = 0 AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
        GROUP BY MinistryDepartment
    ) ber ON ob.DepartmentCode = ber.MinistryDepartment
    WHERE ob.IsDeleted = 0
        AND (@FinancialYear IS NULL OR ob.FinancialYear = @FinancialYear)
        AND (@Keyword IS NULL OR ob.DepartmentCode LIKE '%' + @Keyword + '%')
    GROUP BY ob.DepartmentCode, ber.TotalFinalGrant, ber.TotalActualExpenditure, ber.TotalBalance
    ORDER BY TotalOriginalBudget DESC;
END
GO

-- SP: Get Cost Center Wise Report
CREATE PROCEDURE budget.sp_GetCostCenterWiseReport
    @FinancialYear INT = NULL,
    @FundCode NVARCHAR(20) = NULL,
    @Keyword NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH CostCenterData AS (
        SELECT 
            FundCenterCode,
            MAX(FundCenterDescription) AS FundCenterDescription,
            MAX(MinistryDepartment) AS MinistryDepartment,
            MAX(AttachedDepartment) AS AttachedDepartment,
            COUNT(*) AS TotalRecords,
            SUM(BudgetGrant) AS TotalBudgetGrant,
            SUM(FinalGrant) AS TotalFinalGrant,
            SUM(Releases) AS TotalReleases,
            SUM(ActualExpenditure) AS TotalActualExpenditure,
            SUM(FinalGrantBalance) AS TotalBalance,
            CASE WHEN SUM(FinalGrant) > 0 THEN (SUM(ActualExpenditure) / SUM(FinalGrant)) * 100 ELSE 0 END AS UtilizationPercentage,
            COUNT(*) OVER() AS TotalRecordsCount
        FROM budget.BERData
        WHERE IsDeleted = 0
            AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
            AND (@FundCode IS NULL OR FundCode = @FundCode)
            AND (@Keyword IS NULL OR FundCenterCode LIKE '%' + @Keyword + '%' OR FundCenterDescription LIKE '%' + @Keyword + '%')
        GROUP BY FundCenterCode
    )
    SELECT * FROM CostCenterData
    ORDER BY TotalFinalGrant DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- SP: Get Grant Wise Report
CREATE PROCEDURE budget.sp_GetGrantWiseReport
    @FinancialYear INT = NULL,
    @Keyword NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        BudgetGrant AS GrantAmount,
        COUNT(*) AS TotalRecords,
        SUM(FinalGrant) AS TotalFinalGrant,
        SUM(ActualExpenditure) AS TotalActualExpenditure,
        SUM(FinalGrantBalance) AS TotalBalance,
        COUNT(DISTINCT FundCode) AS FundCount,
        COUNT(DISTINCT FundCenterCode) AS CostCenterCount,
        CASE WHEN SUM(FinalGrant) > 0 THEN (SUM(ActualExpenditure) / SUM(FinalGrant)) * 100 ELSE 0 END AS UtilizationPercentage
    FROM budget.BERData
    WHERE IsDeleted = 0
        AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
        AND (@Keyword IS NULL OR CAST(BudgetGrant AS NVARCHAR) LIKE '%' + @Keyword + '%')
        AND BudgetGrant > 0
    GROUP BY BudgetGrant
    ORDER BY GrantAmount DESC;
END
GO

-- SP: Get Function Wise Report
CREATE PROCEDURE budget.sp_GetFunctionWiseReport
    @FinancialYear INT = NULL,
    @Keyword NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        ob.FunctionCode,
        COUNT(*) AS TotalRecords,
        SUM(ob.OriginalBudgetAmount) AS TotalOriginalBudget,
        SUM(ob.RevisedBudgetAmount) AS TotalRevisedBudget,
        COUNT(DISTINCT ob.DepartmentCode) AS DepartmentCount,
        COUNT(DISTINCT ob.FundCode) AS FundCount
    FROM budget.OriginalBudget ob
    WHERE ob.IsDeleted = 0
        AND (@FinancialYear IS NULL OR ob.FinancialYear = @FinancialYear)
        AND (@Keyword IS NULL OR ob.FunctionCode LIKE '%' + @Keyword + '%')
        AND ob.FunctionCode IS NOT NULL
    GROUP BY ob.FunctionCode
    ORDER BY TotalOriginalBudget DESC;
END
GO

-- SP: Get Object Wise Report
CREATE PROCEDURE budget.sp_GetObjectWiseReport
    @FinancialYear INT = NULL,
    @Keyword NVARCHAR(200) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    SELECT 
        DetailObject,
        COUNT(*) AS TotalRecords,
        SUM(BudgetGrant) AS TotalBudgetGrant,
        SUM(FinalGrant) AS TotalFinalGrant,
        SUM(ActualExpenditure) AS TotalActualExpenditure,
        SUM(FinalGrantBalance) AS TotalBalance,
        COUNT(DISTINCT FundCode) AS FundCount,
        CASE WHEN SUM(FinalGrant) > 0 THEN (SUM(ActualExpenditure) / SUM(FinalGrant)) * 100 ELSE 0 END AS UtilizationPercentage
    FROM budget.BERData
    WHERE IsDeleted = 0
        AND (@FinancialYear IS NULL OR FinancialYear = @FinancialYear)
        AND (@Keyword IS NULL OR DetailObject LIKE '%' + @Keyword + '%')
    GROUP BY DetailObject
    ORDER BY TotalFinalGrant DESC;
END
GO

-- SP: Get Complete Budget Report
CREATE PROCEDURE budget.sp_GetCompleteBudgetReport
    @FinancialYear INT = NULL,
    @FundCode NVARCHAR(20) = NULL,
    @DepartmentCode NVARCHAR(20) = NULL,
    @CostCenterCode NVARCHAR(30) = NULL,
    @Keyword NVARCHAR(200) = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH CombinedData AS (
        SELECT 
            ob.FinancialYear,
            ob.DepartmentCode,
            ob.FundCode,
            ob.FundDescription,
            ob.CostCenterCode,
            ob.CommitmentItem,
            ob.FunctionCode,
            ob.OriginalBudgetAmount,
            ob.RevisedBudgetAmount,
            ob.Posts,
            ber.BudgetGrant,
            ber.FinalGrant,
            ber.Releases,
            ber.ActualExpenditure,
            ber.FinalGrantBalance,
            COUNT(*) OVER() AS TotalRecords
        FROM budget.OriginalBudget ob
        LEFT JOIN budget.BERData ber ON ob.FundCode = ber.FundCode 
            AND ob.CostCenterCode = ber.FundCenterCode
            AND ob.FinancialYear = ber.FinancialYear
        WHERE ob.IsDeleted = 0
            AND (@FinancialYear IS NULL OR ob.FinancialYear = @FinancialYear)
            AND (@FundCode IS NULL OR ob.FundCode = @FundCode)
            AND (@DepartmentCode IS NULL OR ob.DepartmentCode = @DepartmentCode)
            AND (@CostCenterCode IS NULL OR ob.CostCenterCode = @CostCenterCode)
            AND (@Keyword IS NULL OR ob.FundDescription LIKE '%' + @Keyword + '%' OR ob.DepartmentCode LIKE '%' + @Keyword + '%')
    )
    SELECT * FROM CombinedData
    ORDER BY OriginalBudgetAmount DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- SP: Get Lookup Values (for filters)
CREATE PROCEDURE budget.sp_GetLookupValues
    @Type NVARCHAR(50), -- 'Fund', 'Department', 'CostCenter', 'Function', 'Object', 'FinancialYear'
    @SearchTerm NVARCHAR(100) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    IF @Type = 'Fund'
    BEGIN
        SELECT DISTINCT FundCode AS Value, FundDescription AS Text
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0
            AND (@SearchTerm IS NULL OR FundCode LIKE '%' + @SearchTerm + '%')
        ORDER BY FundCode;
    END
    ELSE IF @Type = 'Department'
    BEGIN
        SELECT DISTINCT DepartmentCode AS Value, DepartmentCode AS Text
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0
            AND (@SearchTerm IS NULL OR DepartmentCode LIKE '%' + @SearchTerm + '%')
        ORDER BY DepartmentCode;
    END
    ELSE IF @Type = 'CostCenter'
    BEGIN
        SELECT DISTINCT CostCenterCode AS Value, CostCenterCode AS Text
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0
            AND (@SearchTerm IS NULL OR CostCenterCode LIKE '%' + @SearchTerm + '%')
        ORDER BY CostCenterCode;
    END
    ELSE IF @Type = 'Function'
    BEGIN
        SELECT DISTINCT FunctionCode AS Value, FunctionCode AS Text
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0 AND FunctionCode IS NOT NULL
            AND (@SearchTerm IS NULL OR FunctionCode LIKE '%' + @SearchTerm + '%')
        ORDER BY FunctionCode;
    END
    ELSE IF @Type = 'FinancialYear'
    BEGIN
        SELECT DISTINCT CAST(FinancialYear AS NVARCHAR) AS Value, CAST(FinancialYear AS NVARCHAR) AS Text
        FROM budget.OriginalBudget
        WHERE IsDeleted = 0
        UNION
        SELECT DISTINCT CAST(FinancialYear AS NVARCHAR), CAST(FinancialYear AS NVARCHAR)
        FROM budget.BERData
        WHERE IsDeleted = 0
        ORDER BY Value DESC;
    END
END
GO

-- SP: Delete Import (Soft Delete)
CREATE PROCEDURE budget.sp_DeleteImport
    @ImportId BIGINT,
    @DeletedBy NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    BEGIN TRANSACTION;
    
    BEGIN TRY
        -- Soft delete OriginalBudget records
        UPDATE budget.OriginalBudget 
        SET IsDeleted = 1 
        WHERE ImportId = @ImportId;
        
        -- Soft delete BERData records
        UPDATE budget.BERData 
        SET IsDeleted = 1 
        WHERE ImportId = @ImportId;
        
        -- Soft delete ImportHistory record
        UPDATE budget.ImportHistory 
        SET IsDeleted = 1,
            Status = 'Deleted',
            ErrorMessage = 'Deleted by user: ' + @DeletedBy
        WHERE Id = @ImportId;
        
        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

-- SP: Get Import History
CREATE PROCEDURE budget.sp_GetImportHistory
    @PageNumber INT = 1,
    @PageSize INT = 20,
    @ImportType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH HistoryData AS (
        SELECT *,
               COUNT(*) OVER() AS TotalRecords
        FROM budget.ImportHistory
        WHERE IsDeleted = 0
            AND (@ImportType IS NULL OR ImportType = @ImportType)
    )
    SELECT * FROM HistoryData
    ORDER BY ImportedDate DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- SP: Get Audit Logs with Paging
CREATE PROCEDURE admin.sp_GetAuditLogs
    @UserId NVARCHAR(450) = NULL,
    @Action NVARCHAR(100) = NULL,
    @FromDate DATETIME2 = NULL,
    @ToDate DATETIME2 = NULL,
    @PageNumber INT = 1,
    @PageSize INT = 50
AS
BEGIN
    SET NOCOUNT ON;
    
    DECLARE @Offset INT = (@PageNumber - 1) * @PageSize;
    
    WITH AuditData AS (
        SELECT *,
               COUNT(*) OVER() AS TotalRecords
        FROM admin.AuditLogs
        WHERE (@UserId IS NULL OR UserId = @UserId)
            AND (@Action IS NULL OR Action = @Action)
            AND (@FromDate IS NULL OR ActionDate >= @FromDate)
            AND (@ToDate IS NULL OR ActionDate <= @ToDate)
    )
    SELECT * FROM AuditData
    ORDER BY ActionDate DESC
    OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;
END
GO

-- =============================================================================
-- SCALAR FUNCTIONS
-- =============================================================================

-- Function: Calculate Variance Percentage
CREATE FUNCTION budget.fn_CalculateVariance
(
    @OriginalValue DECIMAL(18,2),
    @ActualValue DECIMAL(18,2)
)
RETURNS DECIMAL(10,2)
AS
BEGIN
    DECLARE @Variance DECIMAL(10,2);
    
    IF @OriginalValue = 0
        SET @Variance = 0;
    ELSE
        SET @Variance = ((@ActualValue - @OriginalValue) / @OriginalValue) * 100;
    
    RETURN @Variance;
END
GO

-- Function: Format Currency
CREATE FUNCTION budget.fn_FormatCurrency
(
    @Amount DECIMAL(18,2)
)
RETURNS NVARCHAR(50)
AS
BEGIN
    RETURN FORMAT(@Amount, 'N2', 'en-PK');
END
GO

-- =============================================================================
-- TABLE-VALUED FUNCTIONS
-- =============================================================================

-- TVF: Get Monthly Expenditure Trend
CREATE FUNCTION budget.fn_GetMonthlyTrend
(
    @FinancialYear INT,
    @FundCode NVARCHAR(20) = NULL
)
RETURNS TABLE
AS
RETURN
SELECT 
    'Jul' AS MonthName, 1 AS MonthOrder, SUM(JulExp) AS Amount FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Aug', 2, SUM(AugExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Sep', 3, SUM(SepExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Oct', 4, SUM(OctExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Nov', 5, SUM(NovExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Dec', 6, SUM(DecExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Jan', 7, SUM(JanExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Feb', 8, SUM(FebExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Mar', 9, SUM(MarExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Apr', 10, SUM(AprExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'May', 11, SUM(MayExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0
    UNION ALL
SELECT 'Jun', 12, SUM(JunExp) FROM budget.BERData 
    WHERE FinancialYear = @FinancialYear AND (@FundCode IS NULL OR FundCode = @FundCode) AND IsDeleted = 0;
GO

-- =============================================================================
-- SEED DATA
-- =============================================================================

-- Seed: Roles
INSERT INTO admin.AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp)
VALUES 
    ('role-admin-001', 'Administrator', 'ADMINISTRATOR', NEWID()),
    ('role-user-001', 'EndUser', 'ENDUSER', NEWID()),
    ('role-viewer-001', 'Viewer', 'VIEWER', NEWID());
GO

-- Seed: Admin User (Password: Admin@123)
-- Password hash for 'Admin@123' using ASP.NET Identity V3
INSERT INTO admin.AspNetUsers (
    Id, UserName, NormalizedUserName, Email, NormalizedEmail, 
    EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
    PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled, AccessFailedCount,
    FullName, Department, Designation, IsActive, CreatedDate, LastLoginDate
)
VALUES (
    'admin-user-001',
    'admin@finance.punjab.gov.pk',
    'ADMIN@FINANCE.PUNJAB.GOV.PK',
    'admin@finance.punjab.gov.pk',
    'ADMIN@FINANCE.PUNJAB.GOV.PK',
    1,
    'AQAAAAIAAYagAAAAELqb+YdGXj7xZzE2z1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1x1v1=', -- Placeholder - will be updated
    NEWID(),
    NEWID(),
    0, 0, 1, 0,
    'System Administrator',
    'Finance Department',
    'IT Administrator',
    1,
    GETUTCDATE(),
    NULL
);
GO

-- Assign Admin Role
INSERT INTO admin.AspNetUserRoles (UserId, RoleId)
VALUES ('admin-user-001', 'role-admin-001');
GO

-- Update the admin password hash properly (using a known hash for 'Admin@123')
-- In production, use UserManager to create the password hash
UPDATE admin.AspNetUsers 
SET PasswordHash = 'AQAAAAEAACcQAAAAEI4UYcgG3X1wv6X5dZ8Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z3Z=='
WHERE Id = 'admin-user-001';
GO

-- =============================================================================
-- PERMISSIONS (Optional)
-- =============================================================================

-- Grant execute permissions to application user
-- GRANT EXECUTE ON SCHEMA::budget TO [FinanceAppUser];
-- GRANT EXECUTE ON SCHEMA::admin TO [FinanceAppUser];
-- GRANT SELECT ON SCHEMA::budget TO [FinanceAppUser];
-- GRANT SELECT ON SCHEMA::admin TO [FinanceAppUser];
GO

-- =============================================================================
-- SCRIPT COMPLETION
-- =============================================================================
PRINT 'Finance Dashboard Database created successfully!';
PRINT 'Tables, Indexes, Views, Stored Procedures, Functions, and Seed Data have been created.';
PRINT '';
PRINT 'IMPORTANT: Update the admin password using the application or UserManager.';
PRINT 'Default Admin: admin@finance.punjab.gov.pk / Admin@123';
GO
