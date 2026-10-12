namespace EpiRead.Services.Interfaces;

/// <summary>
///     Interface providing login related operations such as signing in a user, tracking
///     failed login attempts, clearing attempts and removing expired attempt records.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface ILoginService
{
    /// <summary>
    ///     Signs in the specified <paramref name="user" /> and establishes the authentication
    ///     cookie. If <paramref name="rememberMe" /> is true a persistent cookie is issued.
    ///     This method will also log the login event and send notification emails when required.
    /// </summary>
    /// <param name="user">The <see cref="User" /> to sign in.</param>
    /// <param name="rememberMe">If true create a persistent cookie.</param>
    Task LoginAsync(User user, bool rememberMe);

    /// <summary>
    ///     Finds the login attempt record for the specified <paramref name="user" /> or
    ///     the caller's IP address. Returns the associated <see cref="LoginAttempt" />
    ///     when present; otherwise returns null.
    /// </summary>
    /// <param name="user">The user to lookup attempts for.</param>
    /// <returns>A <see cref="LoginAttempt" /> when a record exists; otherwise null.</returns>
    Task<LoginAttempt?> FindLoginAttemptsAsync(User user);

    /// <summary>
    ///     Updates the login attempt for the specified <paramref name="user" />. If no
    ///     attempt record exists it will be created. Failed attempts increment a
    ///     counter and a user may become blocked after repeated failures (this
    ///     function also sets the expiration time and triggers alert emails when required).
    /// </summary>
    /// <param name="user">The user for whom to update the login attempt.</param>
    /// <returns>The updated <see cref="LoginAttempt" />.</returns>
    Task<LoginAttempt?> UpdateLoginAttemptAsync(User user);

    /// <summary>
    ///     Clears all stored login attempt records for the specified <paramref name="user" />.
    ///     This is typically called after a successful login to reset failure counts.
    /// </summary>
    /// <param name="user">The user whose login attempts will be removed.</param>
    Task ClearUserLoginAttemptsAsync(User user);

    /// <summary>
    ///     Removes login attempt records that have expired. This cleans up old blocked
    ///     records whose expiry time has passed so attempts can start fresh.
    /// </summary>
    Task SyncExpiredLoginAttemptsAsync();
}