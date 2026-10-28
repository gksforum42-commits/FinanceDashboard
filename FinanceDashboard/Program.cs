using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using FinanceDashboard.Repositories;
using FinanceDashboard.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.EntityFrameworkCore;
using System.IO.Compression;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);

// Increase Kestrel request body size to support up to 50 MB file uploads
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 52_428_800; // 50 MB
});

// Raise IIS / IIS Express request size limit to 50 MB
builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 52_428_800;
});

// Add services to the container.
builder.Services.AddControllersWithViews(options =>
{
    // Anti-forgery token validation (GET requests are excluded automatically)
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());

    // Global authorization filter: require authentication by default on all controllers/actions
    // unless explicitly decorated with [AllowAnonymous]
    options.Filters.Add(new Microsoft.AspNetCore.Mvc.Authorization.AuthorizeFilter());
})
.AddJsonOptions(options =>
{
    // Serialize all JSON responses as camelCase so JS can access data.nonDevelopmentBudget etc.
    options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
    options.JsonSerializerOptions.DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull;
});

// Database Context - Windows Authentication
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.MigrationsAssembly("FinanceDashboard");
            sqlOptions.EnableRetryOnFailure(3, TimeSpan.FromSeconds(10), null);
        }));

// Identity Configuration
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    // Password settings
    options.Password.RequireDigit = true;
    options.Password.RequireLowercase = true;
    options.Password.RequireUppercase = true;
    options.Password.RequireNonAlphanumeric = true;
    options.Password.RequiredLength = 8;

    // Lockout settings
    options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(30);
    options.Lockout.MaxFailedAccessAttempts = 5;
    options.Lockout.AllowedForNewUsers = true;

    // User settings
    options.User.RequireUniqueEmail = true;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Ensure role claims are correctly emitted and recognized by User.IsInRole
builder.Services.Configure<IdentityOptions>(options =>
{
    options.ClaimsIdentity.RoleClaimType = ClaimTypes.Role;
    options.ClaimsIdentity.UserNameClaimType = ClaimTypes.Name;
    options.ClaimsIdentity.UserIdClaimType = ClaimTypes.NameIdentifier;
});

// Sync database roles into the authentication principal on every request
builder.Services.AddScoped<IClaimsTransformation, IdentityClaimsTransformer>();

// Authentication & Authorization
builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.ExpireTimeSpan    = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
    options.LoginPath         = "/Account/Login";
    options.LogoutPath        = "/Account/Logout";
    options.AccessDeniedPath  = "/Account/AccessDenied";
});

// Authorization policies
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("Administrator"));
    options.AddPolicy("UserAccess", policy => policy.RequireRole("Administrator", "EndUser"));

    // Require authenticated users for assertion-based policies so anonymous users are not allowed.
    options.AddPolicy("NotDepartmentUser", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx => !ctx.User.IsInRole("DepartmentUser")));

    // Blocks users whose ONLY role is SNEDepartmentUser (pure SNE users) from the regular dashboards.
    // Budget Officer users (DepartmentUser + SNEDepartmentUser) and Administrators are not blocked.
    options.AddPolicy("NotSneDepartmentUser", policy =>
        policy.RequireAuthenticatedUser()
              .RequireAssertion(ctx =>
                  !ctx.User.IsInRole("SNEDepartmentUser") || ctx.User.IsInRole("DepartmentUser") || ctx.User.IsInRole("Administrator")));
});

// Session for temp file storage during import
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Response Compression
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<GzipCompressionProvider>();
});

builder.Services.Configure<GzipCompressionProviderOptions>(options =>
{
    options.Level = CompressionLevel.Optimal;
});

// Persist DataProtection keys to disk so auth cookies survive Azure app pool recycles
// Without this, every recycle invalidates all cookies and logs everyone out.
var keysFolder = Path.Combine(builder.Environment.ContentRootPath, "DataProtection-Keys");
Directory.CreateDirectory(keysFolder);
builder.Services.AddDataProtection()
    .PersistKeysToFileSystem(new System.IO.DirectoryInfo(keysFolder))
    .SetApplicationName("FinanceDashboard");

// Memory Cache
builder.Services.AddMemoryCache();

// Repository Pattern - Dependency Injection
builder.Services.AddScoped<IBudgetRepository, BudgetRepository>();
builder.Services.AddScoped<IAuditRepository, AuditRepository>();
builder.Services.AddScoped<ISneRepository, SneRepository>();
builder.Services.AddScoped<FinanceDashboard.Interfaces.IPivotRepository, FinanceDashboard.Repositories.PivotRepository>();

// Services
builder.Services.AddScoped<IExcelService, ExcelService>();
builder.Services.AddScoped<IPdfService, PdfService>();
builder.Services.AddScoped<AuditService>();
builder.Services.AddScoped<FinanceDashboard.Interfaces.IPivotService, FinanceDashboard.Services.PivotService>();
builder.Services.AddScoped<FinanceDashboard.Interfaces.IPivotExportService, FinanceDashboard.Services.PivotExportService>();
builder.Services.AddSingleton<FinanceDashboard.Services.ImportJobService>();

// EPPlus License
OfficeOpenXml.ExcelPackage.LicenseContext = OfficeOpenXml.LicenseContext.NonCommercial;

// Logging
builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();
builder.Logging.AddEventSourceLogger();

// Build the app
var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseResponseCompression();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

// Security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Content-Security-Policy", "default-src 'self'; script-src 'self' 'unsafe-inline' cdn.jsdelivr.net cdnjs.cloudflare.com code.jquery.com cdn.datatables.net; style-src 'self' 'unsafe-inline' cdn.jsdelivr.net cdnjs.cloudflare.com fonts.googleapis.com cdn.datatables.net; font-src 'self' fonts.gstatic.com cdnjs.cloudflare.com cdn.jsdelivr.net; img-src 'self' data:; connect-src 'self';");
    await next();
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Ensure upload directory exists
var uploadPath = Path.Combine(app.Environment.WebRootPath, "uploads");
if (!Directory.Exists(uploadPath))
    Directory.CreateDirectory(uploadPath);

// Seed database — fixes any manually-inserted invalid password hashes
await SeedDatabaseAsync(app);

app.Run();

static async Task SeedDatabaseAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    try
    {
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

        // Ensure roles exist with correct normalized names
        foreach (var roleName in new[] { "Administrator", "EndUser", "Viewer", "DepartmentUser", "SNEDepartmentUser" })
        {
            if (await roleManager.FindByNameAsync(roleName) == null)
                await roleManager.CreateAsync(new IdentityRole(roleName));
        }

        const string adminEmail = "admin@finance.punjab.gov.pk";
        const string defaultPassword = "Admin@2024!";

        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser != null)
        {
            // Use the built-in reset-token flow so UserManager produces a valid V3 hash
            var token = await userManager.GeneratePasswordResetTokenAsync(adminUser);
            var result = await userManager.ResetPasswordAsync(adminUser, token, defaultPassword);
            if (result.Succeeded)
                logger.LogWarning("Admin password initialised. Login with: {Email} / {Password} — change after first login.", adminEmail, defaultPassword);
            else
                logger.LogError("Failed to reset admin password: {Errors}", string.Join(", ", result.Errors.Select(e => e.Description)));
        }
        else
        {
            adminUser = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true,
                CreatedDate = DateTime.UtcNow
            };
            var createResult = await userManager.CreateAsync(adminUser, defaultPassword);
            if (createResult.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, "Administrator");
                logger.LogWarning("Admin user created. Login with: {Email} / {Password} — change after first login.", adminEmail, defaultPassword);
            }
        }

        // Ensure admin is in the Administrator role
        if (!await userManager.IsInRoleAsync(adminUser!, "Administrator"))
            await userManager.AddToRoleAsync(adminUser!, "Administrator");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Database seeding failed.");
    }
}
