using System.Security.Claims;

namespace FinanceDashboard.Services
{
    /// <summary>
    /// Helper extensions used to enforce department-level data restriction.
    /// A user is "restricted" when they are in the "DepartmentUser" role and have one or more
    /// Department claims assigned. Administrators (and any other role) are never restricted.
    /// </summary>
    public static class ClaimsPrincipalExtensions
    {
        public static bool IsAdministrator(this ClaimsPrincipal user)
        {
            return user.IsInRole("Administrator");
        }

        /// <summary>
        /// Returns the list of department codes the current user is restricted to, or null if the
        /// user is not restricted (e.g. Administrator or has no department assigned).
        /// </summary>
        public static List<string>? GetRestrictedDepartments(this ClaimsPrincipal user)
        {
            if (user.IsAdministrator())
                return null;

            if (!user.IsInRole("DepartmentUser"))
                return null;

            var departments = user.FindAll("Department")
                .Select(c => c.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return departments.Count > 0 ? departments : null;
        }

        /// <summary>
        /// Resolves a single department code to use for legacy single-value filters
        /// (lookups/exports keyed by one departmentCode). If the user is restricted:
        /// - No specific department was requested (null/"all"): returns null so the caller
        ///   falls back to filtering by the full <see cref="GetRestrictedDepartments"/> list
        ///   (showing the union of all the user's assigned departments) instead of narrowing
        ///   to just one.
        /// - A specific department was requested: honored only if it's one of their allowed
        ///   departments, otherwise falls back to their first allowed department (deny + narrow).
        /// Unrestricted users (e.g. Administrators) keep whatever value was requested
        /// (which may be null/"all").
        /// </summary>
        public static string? ResolveDepartmentCode(this ClaimsPrincipal user, string? requested)
        {
            var allowed = user.GetRestrictedDepartments();
            if (allowed == null)
                return requested;

            if (string.IsNullOrWhiteSpace(requested))
                return null;

            if (allowed.Any(d => string.Equals(d, requested, StringComparison.OrdinalIgnoreCase)))
                return requested;

            return allowed.FirstOrDefault();
        }

        /// <summary>
        /// Resolves a list of requested department codes for multi-select filters. If the user is
        /// restricted:
        /// - No specific departments were requested (null/empty): returns null so the caller falls
        ///   back to filtering by the full <see cref="GetRestrictedDepartments"/> list.
        /// - Specific departments were requested: only those that are also in the user's allowed
        ///   list are kept. If none match, falls back to the user's full allowed list (deny + narrow).
        /// Unrestricted users (e.g. Administrators) keep whatever list was requested.
        /// </summary>
        public static List<string>? ResolveDepartmentCodes(this ClaimsPrincipal user, List<string>? requested)
        {
            var allowed = user.GetRestrictedDepartments();
            if (allowed == null)
                return requested;

            if (requested == null || requested.Count == 0)
                return null;

            var filtered = requested
                .Where(r => allowed.Any(d => string.Equals(d, r, StringComparison.OrdinalIgnoreCase)))
                .ToList();

            return filtered.Count > 0 ? filtered : allowed;
        }

        /// <summary>
        /// Returns the single department code assigned to the current SNE department user,
        /// or null if the user is not in the "SNEDepartmentUser" role or has no department assigned.
        /// For users with multiple departments assigned, returns the first one.
        /// </summary>
        public static string? GetSneDepartmentCode(this ClaimsPrincipal user)
        {
            return user.GetSneDepartmentCodes()?.FirstOrDefault();
        }

        /// <summary>
        /// Returns all department codes assigned to the current SNE department user
        /// (e.g. a "Budget Officer" who is both DepartmentUser and SNEDepartmentUser with
        /// multiple departments), or null if the user is not in the "SNEDepartmentUser" role
        /// or has no department assigned.
        /// </summary>
        public static List<string>? GetSneDepartmentCodes(this ClaimsPrincipal user)
        {
            if (!user.IsInRole("SNEDepartmentUser"))
                return null;

            var departments = user.FindAll("Department")
                .Select(c => c.Value?.Trim())
                .Where(v => !string.IsNullOrWhiteSpace(v))
                .Select(v => v!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            return departments.Count > 0 ? departments : null;
        }

        /// <summary>
        /// Returns true if the user holds the SNEDepartmentUser role (regardless of any other
        /// roles they may also hold, e.g. a "Budget Officer" who is also a DepartmentUser).
        /// </summary>
        public static bool IsSneUser(this ClaimsPrincipal user)
        {
            return user.IsInRole("SNEDepartmentUser");
        }
    }
}

