using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace FinanceDashboard.Services
{
    /// <summary>
    /// Ensures the authentication principal always reflects the latest database roles.
    /// This fixes stale cookies where a user was assigned a role after signing in.
    /// </summary>
    public class IdentityClaimsTransformer : IClaimsTransformation
    {
        private readonly UserManager<Models.ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public IdentityClaimsTransformer(
            UserManager<Models.ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _userManager = userManager;
            _roleManager = roleManager;
        }

        public async Task<ClaimsPrincipal> TransformAsync(ClaimsPrincipal principal)
        {
            if (principal.Identity?.IsAuthenticated != true)
                return principal;

            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
                return principal;

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
                return principal;

            var sourceIdentity = principal.Identity as ClaimsIdentity;
            if (sourceIdentity == null)
                return principal;

            // Clone the identity into a writable one so we can safely mutate claims
            var identity = new ClaimsIdentity(
                sourceIdentity.Claims,
                sourceIdentity.AuthenticationType,
                ClaimTypes.Name,
                ClaimTypes.Role);

            // Ensure Name claim is present so User.Identity.Name works reliably
            if (string.IsNullOrEmpty(identity.Name) && !string.IsNullOrEmpty(user.UserName))
            {
                identity.AddClaim(new Claim(ClaimTypes.Name, user.UserName));
            }

            // Synchronize role claims from the database
            var dbRoles = await _userManager.GetRolesAsync(user);
            var existingRoleValues = identity.FindAll(ClaimTypes.Role)
                .Select(c => c.Value)
                .ToHashSet();

            // Remove stale roles
            foreach (var claim in identity.FindAll(ClaimTypes.Role)
                .Where(c => !dbRoles.Contains(c.Value)).ToList())
            {
                identity.RemoveClaim(claim);
            }

            // Add missing roles
            foreach (var role in dbRoles.Where(r => !existingRoleValues.Contains(r)))
            {
                identity.AddClaim(new Claim(ClaimTypes.Role, role));
            }

            return new ClaimsPrincipal(identity);
        }
    }
}
