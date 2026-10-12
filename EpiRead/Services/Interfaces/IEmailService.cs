namespace EpiRead.Services.Interfaces;

/// <summary>
///     Provides HTML email template generation functionality for various system events
///     such as login alerts, password resets, and account creation. Implementations
///     will convert view models into HTML strings suitable for sending via email.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IEmailService
{
    /// <summary>
    ///     Renders a simple title message email template using the provided view model.
    /// </summary>
    /// <param name="simpleTitleMessageVm">The view model containing the title and message data.</param>
    /// <returns>An HTML string for the simple title message email.</returns>
    Task<string> SimpleTitleMessage(SimpleTitleMessageVm simpleTitleMessageVm);

    /// <summary>
    ///     Renders a login alert email template (e.g., unusual login detected).
    /// </summary>
    /// <param name="loginAlertVm">The view model with login alert details.</param>
    /// <returns>An HTML string for the login alert email.</returns>
    Task<string> LoginAlert(LoginAlertVm loginAlertVm);

    /// <summary>
    ///     Renders a token email template (for example, to send verification tokens).
    /// </summary>
    /// <param name="tokenVm">The view model with token details.</param>
    /// <returns>An HTML string containing the token information.</returns>
    Task<string> Token(TokenVm tokenVm);

    /// <summary>
    ///     Renders a password reset email template containing instructions
    ///     or reset links based on the supplied view model.
    /// </summary>
    /// <param name="resetPasswordVm">The view model with reset instructions.</param>
    /// <returns>An HTML string for the password reset email.</returns>
    Task<string> ResetPassword(ResetPasswordVm resetPasswordVm);

    /// <summary>
    ///     Renders a confirmation email informing the user their password was changed.
    /// </summary>
    /// <param name="passwordChangedVm">The view model containing password change details.</param>
    /// <returns>An HTML string for the password change confirmation email.</returns>
    Task<string> PasswordChanged(PasswordChangedVm passwordChangedVm);

    /// <summary>
    ///     Renders an email notifying the user of a suspected illegal or unauthorized login.
    /// </summary>
    /// <param name="illegalLoginVm">The view model with illegal login details.</param>
    /// <returns>An HTML string for the illegal login notification.</returns>
    Task<string> IllegalLogin(IllegalLoginVm illegalLoginVm);

    /// <summary>
    ///     Renders an email for administrators creating accounts, providing new account details
    ///     and next steps to the created user.
    /// </summary>
    /// <param name="accountCreatedByAdminVm">The view model for admin-created accounts.</param>
    /// <returns>An HTML string for the account creation email.</returns>
    Task<string> AccountCreatedByAdmin(AccountCreatedByAdminVm accountCreatedByAdminVm);

    /// <summary>
    ///     Renders a multi-factor authentication reset email template with instructions
    ///     to re-enable or reset MFA for the user's account.
    /// </summary>
    /// <param name="resetMfaVm">The view model containing MFA reset details.</param>
    /// <returns>An HTML string for the MFA reset email.</returns>
    Task<string> ResetMfa(ResetMfaVm resetMfaVm);

    /// <summary>
    ///     Renders an email notification for when an admin updates a user's account.
    ///     Includes detailed before/after information for all changed fields.
    /// </summary>
    /// <param name="accountUpdatedByAdminVm">The view model containing account change details.</param>
    /// <returns>An HTML string for the account update notification email.</returns>
    Task<string> AccountUpdatedByAdmin(AccountUpdatedByAdminVm accountUpdatedByAdminVm);
}