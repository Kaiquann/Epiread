using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services;

/// <summary>
///     ResetPasswordService.cs
///     Implements the <see cref="IResetPasswordService" /> interface to manage
///     reset-password tokens and related operations for the application. This
///     file contains the implementation details used to fetch reset tokens by
///     token or user id and to purge expired tokens from the database.
/// </summary>
/// <author>Chong Jun Xiang</author>
/// <inheritdoc cref="IResetPasswordService" />
public class ResetPasswordService : IResetPasswordService
{
    private readonly DB _db;

    public ResetPasswordService(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task<ResetPassword?> GetResetPasswordByTokenAsync(string token)
    {
        DateTime now = DateTime.Now.ToLocalTime();
        string maskedToken = token;
        if (!string.IsNullOrEmpty(token) && token.Length > 8)
            maskedToken = $"{token.Substring(0, 4)}...{token.Substring(token.Length - 4)}";

        // Start: database operation
        LoggerUtilities.Info($"[Start] token={maskedToken}");
        try
        {
            LoggerUtilities.Info($"[Process] Querying ResetPasswords for token={maskedToken}");

            ResetPassword? result = await _db.ResetPasswords
                .Include(rp => rp.User)
                .SingleOrDefaultAsync(rp => rp.Token == token && rp.ExpiresAt > now);

            if (result is null)
                LoggerUtilities.Warning($"[End] No valid reset password found for token={maskedToken}");
            else
                LoggerUtilities.Info($"[End] Found reset password UserId={result.UserId} ExpiresAt={result.ExpiresAt}");

            return result;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error for token={maskedToken}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ResetPassword?> GetResetPasswordByUserIdAsync(string userId)
    {
        // Start: database operation
        LoggerUtilities.Info($"[Start] UserId={userId}");
        try
        {
            LoggerUtilities.Info($"[Process] Querying ResetPasswords for UserId={userId}");

            ResetPassword? result = await _db.ResetPasswords
                .Include(rp => rp.User)
                .FirstOrDefaultAsync(rp => rp.UserId.ToString() == userId);

            if (result is null)
                LoggerUtilities.Warning($"[End] No reset password record found for UserId={userId}");
            else
                LoggerUtilities.Info($"[End] Found token for UserId={userId} ExpiresAt={result.ExpiresAt}");

            return result;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error for UserId={userId}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SyncExpiredResetPasswordAsync()
    {
        DateTime now = DateTime.Now.ToLocalTime();
        // Start: database operation
        LoggerUtilities.Info($"[Start] now={now}");
        try
        {
            LoggerUtilities.Info("[Process] Querying expired ResetPasswords");

            List<ResetPassword> expired = await _db.ResetPasswords
                .Where(rp => rp.ExpiresAt < now)
                .ToListAsync();

            if (expired.Count == 0)
            {
                LoggerUtilities.Info("[End] No expired reset passwords found");
                return;
            }

            LoggerUtilities.Info($"[Process] Deleting {expired.Count} expired reset passwords");
            _db.ResetPasswords.RemoveRange(expired);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] Removed {expired.Count} expired reset passwords");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[End] Failed to sync expired reset passwords");
            throw;
        }
    }
}