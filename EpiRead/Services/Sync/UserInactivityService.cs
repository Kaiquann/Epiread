using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services.Sync;

/// <summary>
///     Service responsible for detecting inactive users and auto-deactivating them
///     after a configured inactivity threshold.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IUserInactivityService
{
    /// <summary>
    ///     Synchronize auto-deactivation of users who have been inactive beyond the threshold.
    /// </summary>
    Task SyncAutoDeactivateUserAsync();
}

/// <summary>
///     Implementation of <see cref="IUserInactivityService" /> that runs the inactivity checks.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class UserInactivityService : IUserInactivityService
{
    private readonly DB _db;

    /// <summary>
    ///     Constructs a new instance of <see cref="UserInactivityService" />.
    /// </summary>
    public UserInactivityService(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    ///     Finds users who have been inactive longer than the configured threshold and
    ///     marks them as deactivated. Changes are persisted to the database.
    /// </summary>
    public async Task SyncAutoDeactivateUserAsync()
    {
        LoggerUtilities.Info("[Start] Starting sync of auto-deactivate users at " +
                             DateTime.Now.ToLocalTime());

        DateTime thresholdDate = DateTime.Now.ToLocalTime().AddDays(-30);

        try
        {
            // Get all active users with their login history
            List<User> activeUsers = await _db.Users
                .Where(u => u.Status == nameof(UserStatusType.Active) && u.Role == nameof(UserRoleType.Member))
                .Include(u => u.LoginHistory)
                .ToListAsync();

            LoggerUtilities.Info("[Process] Queried database for active member users.");
            LoggerUtilities.Info("[Process] Found " + activeUsers.Count + " active member users to check.");

            int deactivatedCount = 0;

            foreach (User user in activeUsers)
            {
                // Find the latest login time
                LoginHistory? lastLogin = user.LoginHistory?.OrderByDescending(lh => lh.LoginTime).FirstOrDefault();

                // If user has login history, check if last login is older than threshold
                // If user has no login history, check if account creation date is older than threshold
                bool shouldDeactivate = lastLogin != null
                    ? lastLogin.LoginTime < thresholdDate
                    : user.CreatedAt < thresholdDate;

                if (shouldDeactivate)
                {
                    user.Status = nameof(UserStatusType.Deactivated);
                    user.UpdatedAt = DateTime.Now.ToLocalTime();
                    deactivatedCount++;
                }
            }

            if (deactivatedCount > 0)
            {
                LoggerUtilities.Info("[Process] Marked " + deactivatedCount +
                                     " users as deactivated; persisting to database.");
                await _db.SaveChangesAsync();
                LoggerUtilities.Info("[Process] SaveChanges completed.");
            }
            else
            {
                LoggerUtilities.Info("[Process] No users required deactivation.");
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Error while syncing auto deactivate users: " + ex.Message);
        }
        finally
        {
            LoggerUtilities.Info("[End] Completed sync of auto-deactivate users at " + DateTime.Now.ToLocalTime());
        }
    }
}