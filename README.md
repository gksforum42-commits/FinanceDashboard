# Finance Department Punjab - Budget Analysis Dashboard

## Overview

A complete enterprise-grade Budget Analysis Dashboard built with ASP.NET Core MVC (.NET 8) and MS SQL Server. This application allows administrators to upload budget Excel files, import them into SQL Server, analyze Original Budget and BER (Budget Execution Report) data, generate reports, and allow end users to view reports.

## Technology Stack

- **Backend**: ASP.NET Core MVC (.NET 8), C#
- **Database**: MS SQL Server (manual script creation - no EF Migrations)
- **ORM**: Entity Framework Core (Database-First approach)
- **Data Access**: Dapper for stored procedure execution
- **Frontend**: Bootstrap 5, HTML5, CSS3, JavaScript (ES6)
- **Charts**: Chart.js
- **Tables**: DataTables with server-side processing
- **Excel**: EPPlus
- **PDF**: QuestPDF
- **Auth**: ASP.NET Identity with role-based authorization

## Features

### Administrator
- Login / Change Password
- Manage Users and Roles
- Upload Excel files (Original Budget + BER)
- Import data with preview and validation
- View Import History
- View Dashboard with summary cards and charts
- Generate and export reports (PDF/Excel)
- View Audit Logs

### End User
- Login
- View Dashboard
- View all Reports
- Apply filters
- Export PDF and Excel

### Dashboard Cards
- Original Budget Total
- BER Final Grant Total
- Actual Expenditure
- Final Grant Balance
- Variance %
- Utilization %
- Total Departments, Cost Centers, Funds, Grants
- Total Records, Imported Files

### Reports
- Fund Wise Report
- Cost Center Wise Report
- Department Wise Report
- Grant Wise Report
- Function Wise Report
- Object Wise Report
- Complete Budget Report

### Charts
- Pie/Doughnut Chart (Budget Distribution)
- Bar Chart (Monthly Expenditure Trend)
- Bar Chart (Comparison: Budget vs Expenditure)

## Project Structure

```
FinanceDashboard/
├── FinanceDashboard.sln           # Solution file
├── SQL/
│   └── CreateDatabase.sql         # Complete database script
└── FinanceDashboard/
    ├── FinanceDashboard.csproj    # Project file
    ├── Program.cs                 # App configuration & DI
    ├── appsettings.json           # Connection strings & config
    ├── Controllers/               # MVC Controllers
    │   ├── DashboardController.cs
    │   ├── BudgetAnalysisController.cs
    │   ├── ReportsController.cs
    │   ├── ImportController.cs
    │   ├── AdministrationController.cs
    │   ├── AccountController.cs
    │   └── HomeController.cs
    ├── Models/                    # Entity Models
    │   ├── ApplicationUser.cs
    │   ├── OriginalBudget.cs
    │   ├── BERData.cs
    │   ├── ImportHistory.cs
    │   ├── AuditLog.cs
    │   ├── Department.cs
    │   ├── Fund.cs
    │   ├── Function.cs
    │   └── ObjectCode.cs
    ├── ViewModels/                # View Models
    │   ├── DashboardViewModel.cs
    │   ├── ReportViewModel.cs
    │   └── FilterViewModel.cs
    ├── Data/
    │   └── ApplicationDbContext.cs
    ├── Interfaces/                # Repository Interfaces
    │   ├── IBudgetRepository.cs
    │   ├── IAuditRepository.cs
    │   ├── IExcelService.cs
    │   └── IPdfService.cs
    ├── Repositories/              # Repository Implementations
    │   ├── BudgetRepository.cs
    │   └── AuditRepository.cs
    ├── Services/                  # Business Services
    │   ├── ExcelService.cs
    │   ├── PdfService.cs
    │   └── AuditService.cs
    ├── Views/                     # Razor Views
    │   ├── Shared/_Layout.cshtml
    │   ├── Dashboard/Index.cshtml
    │   ├── BudgetAnalysis/
    │   │   ├── OriginalBudget.cshtml
    │   │   ├── BERAnalysis.cshtml
    │   │   └── Comparison.cshtml
    │   ├── Reports/
    │   │   ├── FundWise.cshtml
    │   │   ├── CostCenterWise.cshtml
    │   │   ├── DepartmentWise.cshtml
    │   │   ├── GrantWise.cshtml
    │   │   ├── FunctionWise.cshtml
    │   │   ├── ObjectWise.cshtml
    │   │   └── CompleteBudget.cshtml
    │   ├── Import/
    │   │   ├── UploadOriginalBudget.cshtml
    │   │   ├── UploadBER.cshtml
    │   │   └── History.cshtml
    │   ├── Administration/
    │   │   ├── Users.cshtml
    │   │   ├── Roles.cshtml
    │   │   └── AuditLogs.cshtml
    │   └── Account/
    │       ├── Login.cshtml
    │       ├── ChangePassword.cshtml
    │       └── AccessDenied.cshtml
    └── wwwroot/
        ├── css/
        │   ├── site.css
        │   └── dashboard.css
        └── js/
            ├── site.js
            └── dashboard.js
```

## Setup Instructions

### Prerequisites

- .NET 8 SDK
- SQL Server 2019 or later
- Visual Studio 2022 or VS Code

### Step 1: Create the Database

1. Open SQL Server Management Studio (SSMS)
2. Open the file `SQL/CreateDatabase.sql`
3. Update the database file paths if needed (lines 19 and 25):
   ```sql
   FILENAME = 'C:\SQLData\FinanceDashboardDB_Data.mdf'
   FILENAME = 'C:\SQLData\FinanceDashboardDB_Log.ldf'
   ```
4. Execute the entire script
5. The script creates:
   - Database with optimized settings
   - All tables with proper schemas (`budget` and `admin`)
   - Primary and Foreign Keys
   - Clustered and Non-Clustered Indexes
   - Default and Check Constraints
   - 12 Stored Procedures
   - 5 SQL Views
   - 2 SQL Functions
   - Seed data for Roles and Admin User

### Step 2: Update Connection String

In `appsettings.json`, update the connection string if needed:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=FinanceDashboardDB;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False;"
}
```

For SQL Server Authentication, use:
```json
"ConnectionStrings": {
  "DefaultConnection": "Server=.;Database=FinanceDashboardDB;User Id=your_username;Password=your_password;TrustServerCertificate=True;MultipleActiveResultSets=true;Encrypt=False;"
}
```

### Step 3: Build and Run

```bash
# Navigate to project folder
cd FinanceDashboard/FinanceDashboard

# Restore packages
dotnet restore

# Build
dotnet build

# Run
dotnet run
```

### Step 4: Login

- URL: `https://localhost:5001` or `http://localhost:5000`
- Default Admin: `admin@finance.punjab.gov.pk`
- Default Password: `Admin@123`

**Important**: Change the default admin password after first login!

## Database Objects

### Tables
| Table | Schema | Purpose |
|-------|--------|---------|
| OriginalBudget | budget | Stores Original Budget data from Excel |
| BERData | budget | Stores BER data from Excel |
| ImportHistory | budget | Tracks all import activities |
| Departments | budget | Department lookup |
| Funds | budget | Fund lookup |
| Functions | budget | Function lookup |
| ObjectCodes | budget | Object code lookup |
| AspNetUsers | admin | Identity users |
| AspNetRoles | admin | Identity roles |
| AuditLogs | admin | System audit trail |

### Stored Procedures
- `sp_GetDashboardSummary` - Dashboard summary data
- `sp_GetOriginalBudgetFiltered` - Filtered Original Budget with paging
- `sp_GetBERDataFiltered` - Filtered BER data with paging
- `sp_GetFundWiseReport` - Fund-wise aggregation
- `sp_GetDepartmentWiseReport` - Department-wise aggregation
- `sp_GetCostCenterWiseReport` - Cost Center-wise aggregation
- `sp_GetGrantWiseReport` - Grant-wise aggregation
- `sp_GetFunctionWiseReport` - Function-wise aggregation
- `sp_GetObjectWiseReport` - Object-wise aggregation
- `sp_GetCompleteBudgetReport` - Combined budget report
- `sp_GetLookupValues` - Filter dropdown values
- `sp_DeleteImport` - Soft-delete import and related data
- `sp_GetImportHistory` - Import history with paging
- `sp_GetAuditLogs` - Audit logs with paging

### Views
- `vw_DashboardSummary` - Dashboard metrics
- `vw_OriginalBudgetByDepartment` - OB aggregated by department
- `vw_BERByFund` - BER aggregated by fund
- `vw_MonthlyExpenditure` - Monthly expenditure analysis
- `vw_BudgetComparison` - Budget vs expenditure comparison

### Functions
- `fn_CalculateVariance` - Calculate variance percentage
- `fn_FormatCurrency` - Format as currency
- `fn_GetMonthlyTrend` - Table-valued function for monthly trends

## Performance Optimizations

- **SqlBulkCopy** for importing large datasets (10,000 row batches)
- **Stored Procedures** for all aggregate queries
- **Non-Clustered Indexes** on frequently filtered columns
- **Server-Side Pagination** via DataTables
- **AsNoTracking()** for read-only queries
- **Response Compression** enabled
- **Fill Factor 90%** on indexes for large datasets
- **Query Filters** for soft-delete (IsDeleted = 0)

## Security Features

- ASP.NET Identity with password policies
- Role-based Authorization
- Anti-forgery tokens on all forms
- XSS Protection headers
- CSRF Protection
- Content Security Policy
- Secure cookie settings
- Session timeout
- Account lockout after failed attempts

## Default Credentials

| Role | Email | Password |
|------|-------|----------|
| Administrator | admin@finance.punjab.gov.pk | Admin@123 |

## License

This project is proprietary software developed for the Finance Department, Government of Punjab.
