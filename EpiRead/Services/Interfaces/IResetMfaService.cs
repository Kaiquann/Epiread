namespace EpiRead.Services.Interfaces;

/// <summary>
///     Provides operations to query and manage ResetMfa records used for multi-factor authentication resets.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IResetMfaService
{
    /// <summary>
    ///     Gets a <see cref="ResetMfa" /> record using the provided token, if it exists and is not expired.
    /// </summary>
    /// <param name="token">The reset MFA token string to look up.</param>
    /// <returns>A <see cref="ResetMfa" /> object when a valid record is found; otherwise <c>null</c>.</returns>
    Task<ResetMfa?> GetResetMfaByTokenAsync(string token);

    /// <summary>
    ///     Gets the most recent <see cref="ResetMfa" /> record for a user id.
    /// </summary>
    /// <param name="userId">User identifier.</param>
    /// <returns>A <see cref="ResetMfa" /> object for the given user; otherwise <c>null</c>.</returns>
    Task<ResetMfa?> GetResetMfaByUserIdAsync(string userId);

    /// <summary>
    ///     Synchronizes expired reset MFA records by removing stale entries from persistence.
    /// </summary>
    Task SyncExpiredResetMfaAsync();
}