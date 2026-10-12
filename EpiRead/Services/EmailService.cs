using Razor.Templating.Core;

namespace EpiRead.Services;

/// <summary>
///     Service responsible for rendering Razor email templates into HTML strings.
///     This class wraps a Razor templating engine and provides template-to-string
///     generation for a variety of email notifications present in the system.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class EmailService : IEmailService
{
    private const string TemplatePath = "~/Views/Email/";
    private readonly IRazorTemplateEngine _engine;

    public EmailService(IRazorTemplateEngine engine)
    {
        _engine = engine ?? throw new ArgumentNullException(nameof(engine));
    }

    /// <inheritdoc />
    public async Task<string> SimpleTitleMessage(SimpleTitleMessageVm simpleTitleMessageVm)
    {
        return await RenderPartialAsync("_SimpleTitleMessage", simpleTitleMessageVm);
    }

    /// <inheritdoc />
    public async Task<string> LoginAlert(LoginAlertVm loginAlertVm)
    {
        return await RenderPartialAsync("_LoginAlert", loginAlertVm);
    }

    /// <inheritdoc />
    public async Task<string> Token(TokenVm tokenVm)
    {
        return await RenderPartialAsync("_Token", tokenVm);
    }

    /// <inheritdoc />
    public async Task<string> ResetPassword(ResetPasswordVm resetPasswordVm)
    {
        return await RenderPartialAsync("_ResetPassword", resetPasswordVm);
    }

    /// <inheritdoc />
    public async Task<string> PasswordChanged(PasswordChangedVm passwordChangedVm)
    {
        return await RenderPartialAsync("_PasswordChanged", passwordChangedVm);
    }

    /// <inheritdoc />
    public async Task<string> IllegalLogin(IllegalLoginVm illegalLoginVm)
    {
        return await RenderPartialAsync("_IllegalLogin", illegalLoginVm);
    }

    /// <inheritdoc />
    public async Task<string> AccountCreatedByAdmin(AccountCreatedByAdminVm accountCreatedByAdminVm)
    {
        return await RenderPartialAsync("_AccountCreatedByAdmin", accountCreatedByAdminVm);
    }

    /// <inheritdoc />
    public async Task<string> ResetMfa(ResetMfaVm resetMfaVm)
    {
        return await RenderPartialAsync("_ResetMfa", resetMfaVm);
    }

    /// <inheritdoc />
    public async Task<string> AccountUpdatedByAdmin(AccountUpdatedByAdminVm accountUpdatedByAdminVm)
    {
        return await RenderPartialAsync("_AccountUpdatedByAdmin", accountUpdatedByAdminVm);
    }

    /// <summary>
    ///     Helper method to render a Razor partial view with the supplied model into
    ///     an HTML string. This is a private helper used by the public template methods.
    /// </summary>
    /// <param name="viewName">Partial view name (without extension).</param>
    /// <param name="model">View model to use when rendering the view.</param>
    /// <returns>A string containing the rendered HTML.</returns>
    private async Task<string> RenderPartialAsync(string viewName, object model)
    {
        return await _engine.RenderPartialAsync(TemplatePath + viewName + ".cshtml", model);
    }
}