namespace EpiRead.Services.Interfaces;

/// <summary>
///     Service interface for reset-password token lookup and cleanup operations.
///     This interface defines methods to fetch a password reset token by the token
///     string or by user id, as well as a method to remove expired tokens.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IResetPasswordService
{
    /// <summary>
    ///     Gets a <see cref="ResetPassword" /> record matching the provided token
    ///     if the token has not yet expired.
    /// </summary>
    /// <param name="token">The reset token to look up.</param>
    /// <returns>
    ///     A <see cref="ResetPassword" /> if a valid token is found; otherwise <c>null</c>.
    /// </returns>
    Task<ResetPassword?> GetResetPasswordByTokenAsync(string token);

    /// <summary>
    ///     Gets the latest <see cref="ResetPassword" /> record for the given user id.
    /// </summary>
    /// <param name="userId">The id of the user to look up tokens for.</param>
    /// <returns>
    ///     A <see cref="ResetPassword" /> if a token exists for the user; otherwise <c>null</c>.
    /// </returns>
    Task<ResetPassword?> GetResetPasswordByUserIdAsync(string userId);

    /// <summary>
    ///     Removes any expired <see cref="ResetPassword" /> records from the database.
    /// </summary>
    Task SyncExpiredResetPasswordAsync();
}