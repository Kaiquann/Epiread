using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services;

/// <summary>
///     Concrete implementation of <see cref="IResetMfaService" />.
///     This class handles operations to find and clean up ResetMfa records used for multi-factor authentication reset
///     flows.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class ResetMfaService : IResetMfaService
{
    private readonly DB _db;

    public ResetMfaService(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <inheritdoc />
    public async Task<ResetMfa?> GetResetMfaByTokenAsync(string token)
    {
        string mask = MaskToken(token);
        LoggerUtilities.Info($"[Start] token={mask}");
        try
        {
            LoggerUtilities.Info($"[Process] Querying ResetMfas for token={mask}");
            DateTime now = DateTime.Now.ToLocalTime();
            ResetMfa? result = await _db.ResetMfas
                .Include(rp => rp.User)
                .SingleOrDefaultAsync(rp => rp.Token == token && rp.ExpiresAt > now);
            if (result == null)
                LoggerUtilities.Warning($"[End] No valid ResetMfa found for token={mask}");
            else
                LoggerUtilities.Info(
                    $"[End] Found ResetMfa Id={result.Id} UserId={result.UserId} ExpiresAt={result.ExpiresAt}");
            return result;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error for token={mask}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<ResetMfa?> GetResetMfaByUserIdAsync(string userId)
    {
        LoggerUtilities.Info($"[Start] UserId={userId}");
        try
        {
            LoggerUtilities.Info($"[Process] Querying ResetMfas for UserId={userId}");
            ResetMfa? result = await _db.ResetMfas
                .Include(rp => rp.User)
                .FirstOrDefaultAsync(rp => rp.UserId.ToString() == userId);
            if (result == null)
                LoggerUtilities.Warning($"[End] No ResetMfa found for UserId={userId}");
            else
                LoggerUtilities.Info(
                    $"[End] Found ResetMfa Id={result.Id} UserId={result.UserId} ExpiresAt={result.ExpiresAt}");
            return result;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error for UserId={userId}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task SyncExpiredResetMfaAsync()
    {
        LoggerUtilities.Info("[Start]");
        try
        {
            DateTime now = DateTime.Now.ToLocalTime();
            LoggerUtilities.Info("[Process] Querying expired ResetMfas");
            List<ResetMfa> expired = await _db.ResetMfas
                .Where(rp => rp.ExpiresAt < now)
                .ToListAsync();
            if (expired.Count > 0)
            {
                LoggerUtilities.Info($"[Process] Deleting {expired.Count} expired ResetMfa records");
                _db.ResetMfas.RemoveRange(expired);
                await _db.SaveChangesAsync();
                LoggerUtilities.Info($"[End] Removed {expired.Count} expired ResetMfa records");
            }
            else
            {
                LoggerUtilities.Info("[End] No expired ResetMfa records to remove");
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, "[End] Error while syncing expired ResetMfa records");
            throw;
        }
    }

    /// <summary>
    ///     Return a masked representation of a token string for safe logging.
    ///     Preserves the start and end of long tokens and replaces the middle with '*' characters.
    /// </summary>
    /// <param name="token">Token to mask.</param>
    /// <returns>Masked token.</returns>
    private static string MaskToken(string token)
    {
        if (string.IsNullOrEmpty(token))
            return token;
        if (token.Length <= 8)
            return new string('*', token.Length);
        int keep = 4;
        string prefix = token.Substring(0, keep);
        string suffix = token.Substring(token.Length - keep, keep);
        string middle = new('*', Math.Max(0, token.Length - keep * 2));
        return prefix + middle + suffix;
    }
}