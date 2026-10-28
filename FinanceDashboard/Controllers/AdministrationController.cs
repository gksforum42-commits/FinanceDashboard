using FinanceDashboard.Data;
using FinanceDashboard.Interfaces;
using FinanceDashboard.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FinanceDashboard.Controllers
{
    [Authorize(Roles = "Administrator")]
    public class AdministrationController : Controller
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IAuditRepository _auditRepository;
        private readonly ILogger<AdministrationController> _logger;
        private readonly ApplicationDbContext _context;

        public AdministrationController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditRepository auditRepository,
            ILogger<AdministrationController> logger,
            ApplicationDbContext context)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditRepository = auditRepository;
            _logger = logger;
            _context = context;
        }

        // Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();

            var roleMap = new Dictionary<string, string>();
            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                roleMap[user.Id] = roles.Count > 0 ? string.Join(", ", roles) : "User";
            }
            ViewBag.UserRoles = roleMap;

            return View(users);
        }

        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            try
            {
                var users = await _userManager.Users
                    .Select(u => new
                    {
                        u.Id,
                        u.UserName,
                        u.Email,
                        u.FullName,
                        u.Department,
                        u.Designation,
                        u.IsActive,
                        u.LastLoginDate,
                        u.CreatedDate
                    })
                    .ToListAsync();

                return Json(new { success = true, data = users });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting users");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                var roles = await _userManager.GetRolesAsync(user);
                return Json(new { success = true, data = new { user, roles } });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting user");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateUser([FromBody] CreateUserModel model)
        {
            try
            {
                var user = new ApplicationUser
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FullName = model.FullName,
                    Department = model.Department,
                    Designation = model.Designation,
                    IsActive = model.IsActive,
                    CreatedDate = DateTime.UtcNow,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var rolesToAssign = (model.Roles != null && model.Roles.Count > 0)
                        ? model.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList()
                        : (!string.IsNullOrEmpty(model.Role) ? new List<string> { model.Role } : new List<string>());

                    if (rolesToAssign.Count > 0)
                    {
                        await _userManager.AddToRolesAsync(user, rolesToAssign);
                    }

                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = currentUserId,
                        UserName = User.Identity?.Name,
                        Action = "Create",
                        EntityType = "User",
                        EntityId = user.Id,
                        NewValues = $"Created user {model.Email} with role(s) {string.Join(", ", rolesToAssign)}",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Status = "Success"
                    });

                    return Json(new { success = true, message = "User created successfully." });
                }

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating user");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> UpdateUser([FromBody] UpdateUserModel model)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(model.Id);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                user.FullName = model.FullName;
                user.Department = model.Department;
                user.Designation = model.Designation;
                user.IsActive = model.IsActive;

                var result = await _userManager.UpdateAsync(user);
                if (result.Succeeded)
                {
                    // Update roles
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);

                    var rolesToAssign = (model.Roles != null && model.Roles.Count > 0)
                        ? model.Roles.Where(r => !string.IsNullOrWhiteSpace(r)).Distinct().ToList()
                        : (!string.IsNullOrEmpty(model.Role) ? new List<string> { model.Role } : new List<string>());

                    if (rolesToAssign.Count > 0)
                    {
                        await _userManager.AddToRolesAsync(user, rolesToAssign);
                    }

                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = currentUserId,
                        UserName = User.Identity?.Name,
                        Action = "Update",
                        EntityType = "User",
                        EntityId = user.Id,
                        NewValues = $"Updated user {user.Email}",
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Status = "Success"
                    });

                    return Json(new { success = true, message = "User updated successfully." });
                }

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating user");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(id);
                if (user == null)
                    return Json(new { success = false, message = "User not found." });

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = currentUserId,
                        UserName = User.Identity?.Name,
                        Action = "Delete",
                        EntityType = "User",
                        EntityId = id,
                        IpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                        Status = "Success"
                    });

                    return Json(new { success = true, message = "User deleted successfully." });
                }

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting user");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Roles
        public async Task<IActionResult> Roles()
        {
            var roles = await _roleManager.Roles.ToListAsync();
            return View(roles);
        }

        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            try
            {
                var roles = await _roleManager.Roles
                    .Select(r => new { r.Id, r.Name, r.NormalizedName })
                    .ToListAsync();
                return Json(new { success = true, data = roles });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting roles");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> CreateRole([FromBody] CreateRoleModel model)
        {
            try
            {
                var role = new IdentityRole { Name = model.Name, NormalizedName = model.Name.ToUpper() };
                var result = await _roleManager.CreateAsync(role);

                if (result.Succeeded)
                    return Json(new { success = true, message = "Role created successfully." });

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating role");
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> DeleteRole(string id)
        {
            try
            {
                var role = await _roleManager.FindByIdAsync(id);
                if (role == null)
                    return Json(new { success = false, message = "Role not found." });

                var result = await _roleManager.DeleteAsync(role);
                if (result.Succeeded)
                    return Json(new { success = true, message = "Role deleted successfully." });

                return Json(new { success = false, message = string.Join(", ", result.Errors.Select(e => e.Description)) });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting role");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Audit Logs
        public IActionResult AuditLogs()
        {
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetAuditLogs(string? userId, string? auditAction, string? fromDate, string? toDate, int page = 1, int pageSize = 50)
        {
            try
            {
                // Normalize empty strings to null
                auditAction = string.IsNullOrWhiteSpace(auditAction) ? null : auditAction.Trim();
                fromDate = string.IsNullOrWhiteSpace(fromDate) ? null : fromDate.Trim();
                toDate = string.IsNullOrWhiteSpace(toDate) ? null : toDate.Trim();

                // Parse dates from string format (YYYY-MM-DD)
                DateTime? parsedFromDate = null;
                DateTime? parsedToDate = null;

                if (!string.IsNullOrEmpty(fromDate) && DateTime.TryParse(fromDate, out var from))
                {
                    parsedFromDate = from;
                }

                if (!string.IsNullOrEmpty(toDate) && DateTime.TryParse(toDate, out var to))
                {
                    parsedToDate = to;
                }

                var logs = await _auditRepository.GetAuditLogsAsync(userId, auditAction, parsedFromDate, parsedToDate, page, pageSize);
                var count = await _auditRepository.GetAuditLogsCountAsync(userId, auditAction, parsedFromDate, parsedToDate);

                return Json(new
                {
                    success = true,
                    data = logs,
                    totalRecords = count,
                    page,
                    pageSize,
                    totalPages = (int)Math.Ceiling((double)count / pageSize)
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting audit logs");
                return Json(new { success = false, message = ex.Message });
            }
        }

        // ViewModels for Admin
        public class CreateUserModel
        {
            public string Email { get; set; } = string.Empty;
            public string Password { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string? Designation { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Role { get; set; }
            // Supports assigning multiple roles at once (e.g. Budget Officer = DepartmentUser + SNEDepartmentUser).
            // If provided, this takes precedence over the single Role property.
            public List<string>? Roles { get; set; }
        }

        public class UpdateUserModel
        {
            public string Id { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string? Designation { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Role { get; set; }
            // Supports assigning multiple roles at once (e.g. Budget Officer = DepartmentUser + SNEDepartmentUser).
            // If provided, this takes precedence over the single Role property.
            public List<string>? Roles { get; set; }
        }

        public class CreateRoleModel
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
