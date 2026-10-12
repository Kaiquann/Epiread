using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EpiRead;

public static class Extensions
{
    // ===================================================================================
    // Library For All Project
    // ===================================================================================

    public static bool IsAjax(this HttpRequest request)
    {
        return request.Headers.XRequestedWith == "XMLHttpRequest";
    }

    public static bool IsValid(this ModelStateDictionary ms, string key)
    {
        return ms.GetFieldValidationState(key) == ModelValidationState.Valid;
    }

    public static DateOnly ToDateOnly(this DateTime dt)
    {
        return DateOnly.FromDateTime(dt);
    }

    public static TimeOnly ToTimeOnly(this DateTime dt)
    {
        return TimeOnly.FromDateTime(dt);
    }

    public static void Set<T>(this ISession session, string key, T value)
    {
        session.SetString(key, JsonSerializer.Serialize(value));
    }

    public static T? Get<T>(this ISession session, string key)
    {
        string? value = session.GetString(key);
        return value == null ? default : JsonSerializer.Deserialize<T>(value);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Sign in user
     */
    public static async Task SignInAsync(this HttpContext httpContext, string name, string role, bool rememberMe)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.Name, name),
            new(ClaimTypes.Role, role)
        ];

        ClaimsIdentity identity = new(claims,
            CookieAuthenticationDefaults.AuthenticationScheme);

        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties authProperties = new()
        {
            IsPersistent = rememberMe,
            AllowRefresh = true
        };

        await httpContext.SignInAsync(principal, authProperties);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Sign out user
     */
    public static async Task SignOutAsync(this HttpContext httpContext)
    {
        await httpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Redirect to referer
     */
    public static RedirectResult RedirectToReferer(this HttpRequest request)
    {
        string referer = request.Headers["Referer"].ToString();
        return !string.IsNullOrEmpty(referer)
            ? new RedirectResult(referer)
            : new RedirectResult("/");
    }

    /**
     * @author: Chong Jun Xiang
     * @description: AJAX-aware redirect to action
     * Returns JSON response for AJAX requests, redirect for regular requests
     */
    public static IActionResult RedirectToReferer(this HttpRequest request, Controller controller, string action,
        string? area = null)
    {
        string url = controller.Url.Action(action, new { area })!;

        if (request.IsAjax()) return controller.Json(new { redirect = url });

        return new RedirectResult(url);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: AJAX-aware redirect to action with route values
     * Returns JSON response for AJAX requests, redirect for regular requests
     */
    public static IActionResult RedirectToReferer(this HttpRequest request, Controller controller, string action,
        string? area = null, object? routeValues = null)
    {
        string url = controller.Url.Action(action, area, routeValues)!;

        if (request.IsAjax()) return controller.Json(new { redirect = url });

        return new RedirectResult(url);
    }

    // ===================================================================================
    // Internal Project Use Only
    // ===================================================================================

    public static string IsActive(this IHtmlHelper html, string controller, string? action = null)
    {
        RouteData routeData = html.ViewContext.RouteData;

        string? routeController = routeData.Values["controller"]?.ToString();
        string? routeAction = routeData.Values["action"]?.ToString();

        // Check if the controller matches and optionally the action
        if (string.Equals(controller, routeController, StringComparison.OrdinalIgnoreCase) &&
            (action == null || string.Equals(action, routeAction, StringComparison.OrdinalIgnoreCase)))
            return "active";

        // NEW: Check if only the controller matches for all child actions
        if (action == null && string.Equals(controller, routeController, StringComparison.OrdinalIgnoreCase))
            return "active";

        return string.Empty;
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if user is logged in
     */
    public static bool IsLoggedIn(this ClaimsPrincipal user)
    {
        return user.Identity is { IsAuthenticated: true };
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if user has admin access
     */
    public static bool HasAdminAccess(this ClaimsPrincipal user)
    {
        return user.IsLoggedIn() &&
               (user.IsInRole(nameof(UserRoleType.Admin)) || user.IsInRole(nameof(UserRoleType.SuperAdmin)));
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Get current logged in user
     */
    public static async Task<User?> GetCurrentUser(this ClaimsPrincipal user, DB db)
    {
        if (!user.IsLoggedIn()) return null;
        return await db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == user.Identity!.Name);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Get all roles except SuperAdmin
     */
    public static async Task<List<Role>> GetRolesAsync(this DB db, User user)
    {
        // Always exclude SuperAdmin from returned roles
        List<Role> roles = await db.Roles
            .Where(r => r.Name != nameof(UserRoleType.SuperAdmin))
            .ToListAsync();

        // If the current user is an Admin, do not show the Admin role in the list.
        // This provides an extra safety check on the UI side in addition to server-side
        // validation which prevents admin users from assigning Admin/SuperAdmin roles.
        if (string.Equals(user.Role?.Trim(), nameof(UserRoleType.Admin), StringComparison.OrdinalIgnoreCase))
            roles = roles.Where(r =>
                    !string.Equals(r.Name?.Trim(), nameof(UserRoleType.Admin), StringComparison.OrdinalIgnoreCase))
                .ToList();

        return roles;
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Get roles for filtering in UI. This returns all roles except SuperAdmin.
     * Unlike GetRolesAsync, this method does not remove Admin for Admin users because
     * administrators should be able to filter the list to show Admins.
     */
    public static async Task<List<Role>> GetRolesForFilterAsync(this DB db)
    {
        return await db.Roles
            .Where(r => r.Name != nameof(UserRoleType.SuperAdmin))
            .ToListAsync();
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Get role by name
     */
    public static async Task<Role?> GetRoleAsync(this DB db, string roleName)
    {
        return await db.Roles.FirstOrDefaultAsync(r => r.Name == roleName);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if role is valid
     */
    public static async Task<bool> IsValidRole(this DB db, string roleName)
    {
        return await db.Roles.AnyAsync(r => r.Name == roleName);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Server-side check whether the current user is allowed to create the specified role.
     * Only a SuperAdmin may create Admin/SuperAdmin; Admin and SuperAdmin may create Author/Member.
     */
    public static bool CanCreateRole(this User user, string? roleToCreate)
    {
        if (user == null) return false;
        if (string.IsNullOrWhiteSpace(roleToCreate)) return false;

        if (!Enum.TryParse<UserRoleType>(roleToCreate, true, out var parsedRole))
            return false;

        switch (parsedRole)
        {
            //case UserRoleType.SuperAdmin:
                //return user.Role == nameof(UserRoleType.SuperAdmin);
            case UserRoleType.Admin:
                return user.Role == nameof(UserRoleType.SuperAdmin);
            case UserRoleType.Author:
            case UserRoleType.Member:
                return user.Role == nameof(UserRoleType.Admin) || user.Role == nameof(UserRoleType.SuperAdmin);
            default:
                return false;
        }
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if the email exists in the database.
     */
    public static async Task<bool> IsEmailExists(this DB db, string? email)
    {
        if (email == null) return false;
        return await db.Users.AnyAsync(u => u.Email == email);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if the email is unique (does not exist in the database).
     */
    public static async Task<bool> IsEmailUnique(this DB db, string? email, string? userId = null)
    {
        if (email == null) return false;

        // If userId is provided, exclude that user from the uniqueness check
        if (!string.IsNullOrEmpty(userId))
            return !await db.Users.AnyAsync(u => u.Email == email && u.Id.ToString() != userId);

        return !await db.Users.AnyAsync(u => u.Email == email);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if the username exists in the database.
     */
    public static async Task<bool> IsUserNameExists(this DB db, string? userName)
    {
        if (userName == null) return false;
        return await db.Users.AnyAsync(u => u.UserName == userName);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if the username is unique (does not exist in the database).
     * @param db: Database context
     * @param userName: Username to check for uniqueness
     * @param userId: Optional user ID to exclude from the check (for editing own username)
     */
    public static async Task<bool> IsUserNameUnique(this DB db, string? userName, string? userId = null)
    {
        if (userName == null) return false;

        // If userId is provided, exclude that user from the uniqueness check
        if (!string.IsNullOrEmpty(userId))
            return !await db.Users.AnyAsync(u => u.UserName == userName && u.Id.ToString() != userId);

        return !await db.Users.AnyAsync(u => u.UserName == userName);
    }

    /**
     * @author: Chong Jun Xiang
     * @description: Check if the book category slug is valid and unique.
     * Slug must be lowercase, no spaces, and match the pattern: letters, numbers, hyphens.
     */
    public static async Task<bool> IsBookCategorySlugValid(this DB db, string? slug, int? id = null)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return false;

        // Slug pattern: lowercase letters, numbers, hyphens, no spaces
        var slugPattern = @"^[a-z0-9-]+$";
        if (!Regex.IsMatch(slug, slugPattern))
            return false;

        // Check uniqueness (exclude current id if editing)
        return !await db.BookCategories
            .AnyAsync(bc => bc.Slug == slug && (!id.HasValue || bc.Id != id.Value));
    }
}