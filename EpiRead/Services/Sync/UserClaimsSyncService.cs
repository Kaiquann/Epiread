using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services.Sync;

/// <summary>
///     Service to synchronize user claims and roles from the database into the current
///     authentication principal. Used by middleware to ensure the authenticated user's
///     role and status are up-to-date for authorization decisions.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IUserClaimsSyncService
{
    /// <summary>
    ///     Retrieve a user by their string identifier from the database.
    /// </summary>
    Task<User?> GetUserAsync(string userId);
}

/// <summary>
///     Implementation of <see cref="IUserClaimsSyncService" /> that reads user data from the DB.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class UserClaimsSyncService : IUserClaimsSyncService
{
    private readonly DB _db;

    /// <summary>
    ///     Constructs a new <see cref="UserClaimsSyncService" />.
    /// </summary>
    public UserClaimsSyncService(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    ///     Get the user entity for the provided user id string, or null if not found.
    /// </summary>
    public async Task<User?> GetUserAsync(string userId)
    {
        LoggerUtilities.Info("[Start] Querying DB for userId=" + userId);
        try
        {
            User? user = await _db.Users.FirstOrDefaultAsync(u => u.Id.ToString() == userId);

            if (user == null)
                LoggerUtilities.Warning("[Process] User not found in DB userId=" + userId);
            else
                LoggerUtilities.Info("[Process] User found userId=" + userId + " Status=" + user.Status + " Role=" +
                                     user.Role);
            LoggerUtilities.Info("[End] Completed for userId=" + userId);
            return user;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Danger] Error fetching user " + userId + " from DB: " + ex.Message);
            throw;
        }
    }
}

/// <summary>
///     Middleware that ensures authenticated users' roles and status are synchronized
///     with the database on each request.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class UserClaimsSyncMiddleware
{
    private readonly RequestDelegate _next;

    /// <summary>
    ///     Creates a new instance of the middleware.
    /// </summary>
    public UserClaimsSyncMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    /// <summary>
    ///     Middleware entry point. Ensures the user's claims reflect the latest DB state
    ///     and logs the user out if their account is missing or inactive.
    /// </summary>
    public async Task InvokeAsync(HttpContext context, IUserClaimsSyncService userSyncService)
    {
        LoggerUtilities.Info("[Start] Path=" + context.Request.Path + " Authenticated=" +
                             (context.User.Identity?.IsAuthenticated ?? false));

        if (context.User.Identity?.IsAuthenticated ?? false)
            try
            {
                string? userId = context.User.FindFirst(ClaimTypes.Name)?.Value;

                if (!string.IsNullOrEmpty(userId))
                {
                    LoggerUtilities.Info("[Process] Fetching user from DB for userId=" + userId);
                    User? user = await userSyncService.GetUserAsync(userId);

                    if (user == null)
                    {
                        LoggerUtilities.Warning("[Process] User " + userId + " not found - signing out.");
                        await LogoutUserAsync(context);
                        context.Response.Redirect("/Login");
                        return;
                    }

                    if (user.Status != nameof(UserStatusType.Active))
                    {
                        LoggerUtilities.Warning("[Process] User " + userId + " not active (Status=" + user.Status +
                                                ") - signing out.");
                        await LogoutUserAsync(context);
                        context.Response.Redirect("/Login");
                        return;
                    }

                    if (context.User.Identity is ClaimsIdentity identity)
                    {
                        await UpdateUserRoleIfNeededAsync(context, identity, user, userId);
                    }
                    else
                    {
                        LoggerUtilities.Warning("[Process] Invalid claims identity for user " + userId +
                                                " - signing out.");
                        await LogoutUserAsync(context);
                    }
                }
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Danger] Error during role synchronization for Path=" + context.Request.Path +
                                      ": " + ex.Message);
            }

        await _next(context);
    }

    private async Task UpdateUserRoleIfNeededAsync(HttpContext context, ClaimsIdentity identity, User user,
        string userId)
    {
        string latestRole = user.Role;
        string? currentRole = identity.FindFirst(ClaimTypes.Role)?.Value;

        if (currentRole != latestRole)
        {
            LoggerUtilities.Info("[Start] Updating role for user " + userId + " from " + currentRole + " to " +
                                 latestRole);

            UpdateUserRoleClaim(identity, latestRole);

            // Re-authenticate the user with updated claims
            ClaimsPrincipal newPrincipal = new(identity);
            LoggerUtilities.Info("[Process] Re-signing in user " + userId + " with updated claims");
            try
            {
                await context.SignInAsync(newPrincipal);
                LoggerUtilities.Info("[End] Successfully re-signed in user " + userId + " with role " + latestRole);
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[Danger] Failed to re-sign-in user " + userId + ": " + ex.Message);
            }
        }
        // No DB/network operations required when there's no role change; skip logging to reduce noise
    }

    private static void UpdateUserRoleClaim(ClaimsIdentity identity, string latestRole)
    {
        Claim? roleClaim = identity.FindFirst(ClaimTypes.Role);
        if (roleClaim != null) identity.RemoveClaim(roleClaim);
        identity.AddClaim(new Claim(ClaimTypes.Role, latestRole));
    }

    private async Task LogoutUserAsync(HttpContext context)
    {
        try
        {
            LoggerUtilities.Info("[Start] Signing out via cookie authentication");
            await context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            LoggerUtilities.Info("[End] Signed out successfully");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Danger] Error while signing out user: " + ex.Message);
        }
    }
}