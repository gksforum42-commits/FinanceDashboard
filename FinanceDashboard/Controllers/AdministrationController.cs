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

        public AdministrationController(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IAuditRepository auditRepository,
            ILogger<AdministrationController> logger)
        {
            _userManager = userManager;
            _roleManager = roleManager;
            _auditRepository = auditRepository;
            _logger = logger;
        }

        // Users
        public async Task<IActionResult> Users()
        {
            var users = await _userManager.Users.ToListAsync();
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
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
                    }

                    var currentUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "system";
                    await _auditRepository.LogAsync(new AuditLog
                    {
                        UserId = currentUserId,
                        UserName = User.Identity?.Name,
                        Action = "Create",
                        EntityType = "User",
                        EntityId = user.Id,
                        NewValues = $"Created user {model.Email} with role {model.Role}",
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
                    // Update role
                    var currentRoles = await _userManager.GetRolesAsync(user);
                    await _userManager.RemoveFromRolesAsync(user, currentRoles);
                    if (!string.IsNullOrEmpty(model.Role))
                    {
                        await _userManager.AddToRoleAsync(user, model.Role);
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
        public async Task<IActionResult> GetAuditLogs(string? userId, string? action, DateTime? fromDate, DateTime? toDate, int page = 1, int pageSize = 50)
        {
            try
            {
                var logs = await _auditRepository.GetAuditLogsAsync(userId, action, fromDate, toDate, page, pageSize);
                var count = await _auditRepository.GetAuditLogsCountAsync(userId, action, fromDate, toDate);

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
        }

        public class UpdateUserModel
        {
            public string Id { get; set; } = string.Empty;
            public string FullName { get; set; } = string.Empty;
            public string? Department { get; set; }
            public string? Designation { get; set; }
            public bool IsActive { get; set; } = true;
            public string? Role { get; set; }
        }

        public class CreateRoleModel
        {
            public string Name { get; set; } = string.Empty;
        }
    }
}
