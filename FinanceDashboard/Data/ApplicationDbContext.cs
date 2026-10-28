using FinanceDashboard.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FinanceDashboard.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Budget Tables
        public DbSet<OriginalBudget> OriginalBudgets { get; set; }
        public DbSet<BERData> BERData { get; set; }
        public DbSet<YpProject> YpProjects { get; set; }
        public DbSet<ImportHistory> ImportHistories { get; set; }
        public DbSet<Department> Departments { get; set; }
        public DbSet<Fund> Funds { get; set; }
        public DbSet<Function> Functions { get; set; }
        public DbSet<ObjectCode> ObjectCodes { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<PnDFile> PnDFiles { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Configure schemas
            modelBuilder.HasDefaultSchema("admin");

            // Configure Identity tables to use admin schema
            modelBuilder.Entity<ApplicationUser>().ToTable("AspNetUsers", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRole>().ToTable("AspNetRoles", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserRole<string>>().ToTable("AspNetUserRoles", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserClaim<string>>().ToTable("AspNetUserClaims", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserLogin<string>>().ToTable("AspNetUserLogins", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityRoleClaim<string>>().ToTable("AspNetRoleClaims", "admin");
            modelBuilder.Entity<Microsoft.AspNetCore.Identity.IdentityUserToken<string>>().ToTable("AspNetUserTokens", "admin");

            // Configure OriginalBudget
            modelBuilder.Entity<OriginalBudget>(entity =>
            {
                entity.ToTable("OriginalBudget", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FinancialYear);
                entity.HasIndex(e => e.DepartmentCode);
                entity.HasIndex(e => e.FundCode);
                entity.HasIndex(e => e.CostCenterCode);
                entity.HasIndex(e => e.CommitmentItem);
                entity.HasIndex(e => e.ImportId);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure BERData
            modelBuilder.Entity<BERData>(entity =>
            {
                entity.ToTable("BerSapData", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FinancialYear);
                entity.HasIndex(e => e.FundCode);
                entity.HasIndex(e => e.FundCenterCode);
                entity.HasIndex(e => e.MinistryDepartment);
                entity.HasIndex(e => e.DetailObject);
            });

            // Configure YpProject
            modelBuilder.Entity<YpProject>(entity =>
            {
                entity.ToTable("ypProject", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FiscalYear);
                entity.HasIndex(e => e.ProjectID);
                entity.HasIndex(e => e.WBSElement);
            });

            // Configure ImportHistory
            modelBuilder.Entity<ImportHistory>(entity =>
            {
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ImportedDate);
                entity.HasIndex(e => e.FinancialYear);
                entity.HasQueryFilter(e => !e.IsDeleted);
            });

            // Configure AuditLog
            modelBuilder.Entity<AuditLog>(entity =>
            {
                entity.ToTable("AuditLogs", "admin");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ActionDate);
                entity.HasIndex(e => e.UserId);
                entity.HasIndex(e => e.Action);
            });

            // Configure PnDFile
            modelBuilder.Entity<PnDFile>(entity =>
            {
                entity.ToTable("pndFile", "budget");
                entity.HasKey(e => e.Id);
            });

            // Configure lookup tables
            modelBuilder.Entity<Department>(entity =>
            {
                entity.ToTable("Departments", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.DepartmentCode).IsUnique();
            });

            modelBuilder.Entity<Fund>(entity =>
            {
                entity.ToTable("Funds", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FundCode).IsUnique();
            });

            modelBuilder.Entity<Function>(entity =>
            {
                entity.ToTable("Functions", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.FunctionCode).IsUnique();
            });

            modelBuilder.Entity<ObjectCode>(entity =>
            {
                entity.ToTable("ObjectCodes", "budget");
                entity.HasKey(e => e.Id);
                entity.HasIndex(e => e.ObjectCodeValue).IsUnique();
            });
        }
    }
}
