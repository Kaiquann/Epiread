using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services;

/// <summary>
///     Default implementation of <see cref="ILoginService" />.
///     This class handles user sign-in, login attempt tracking and cleanup, and
///     sends notification emails for important events such as illegal login attempts.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class LoginService : ILoginService
{
    private const string ForgotPasswordUrl = "/ForgotPassword/Index?email=";
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IIpInfoHelper _ipInfoService;
    private readonly IMailerHelper _mailerService;

    public LoginService(DB db, IEmailService emailService, IIpInfoHelper ipInfoService, IMailerHelper mailerService,
        IHttpContextAccessor httpContextAccessor)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _ipInfoService = ipInfoService ?? throw new ArgumentNullException(nameof(ipInfoService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
    }

    /// <inheritdoc />
    public async Task LoginAsync(User user, bool rememberMe)
    {
        await _httpContextAccessor.HttpContext!.SignInAsync(user.Id.ToString(), user.Role, rememberMe);
        LoggerUtilities.Info($"[Start] Retrieving IP info for {MaskEmail(user.Email)}");
        IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(_httpContextAccessor.HttpContext!);
        LoggerUtilities.Info($"[Process] Retrieved IP {ipInfo?.Ip} for {MaskEmail(user.Email)}");
        LoginHistory loginHistory = new LoginHistory
        {
            Email = user.Email,
            Ip = ipInfo?.Ip,
            City = ipInfo?.City,
            Region = ipInfo?.Region,
            Country = ipInfo?.Country,
            Loc = ipInfo?.Loc,
            Org = ipInfo?.Org,
            Postal = ipInfo?.Postal,
            Timezone = ipInfo?.Timezone,
            UserId = user.Id
        };
        LoggerUtilities.Info($"[Start] Saving login history for {MaskEmail(user.Email)}");
        await _db.LoginHistories.AddAsync(loginHistory);
        await _db.SaveChangesAsync();
        LoggerUtilities.Info($"[End] Saved login history for {MaskEmail(user.Email)} at {loginHistory.LoginTime}");
        string encodedEmail = Uri.EscapeDataString(user.Email);
        string url = ForgotPasswordUrl + encodedEmail;
        LoginAlertVm loginAlertVm = new LoginAlertVm
        {
            Email = user.Email,
            IpInfo = ipInfo!,
            LoginTime = loginHistory.LoginTime,
            Url = url
        };
        try
        {
            string body = await _emailService.LoginAlert(loginAlertVm);
            MailerConfig mail = new MailerConfig
            {
                To = [user.Email],
                Subject = EmailSubject.LoginAlertSubject,
                Body = body
            };
            LoggerUtilities.Info($"[Process] Sending login alert email to {MaskEmail(user.Email)}");
            await _mailerService.SendEmail(mail);
            LoggerUtilities.Info($"[End] Login alert email sent to {MaskEmail(user.Email)}");
        }
        catch (Exception ex)
        {
            // A notification is not part of authentication itself. Do not deny a
            // valid login when the external email provider is unavailable or
            // temporarily misconfigured.
            LoggerUtilities.Error(ex,
                $"[End] Login succeeded, but the login alert could not be sent to {MaskEmail(user.Email)}");
        }
    }

    /// <inheritdoc />
    public async Task<LoginAttempt?> FindLoginAttemptsAsync(User user)
    {
        LoggerUtilities.Info($"[Start] Retrieving IP for {MaskEmail(user.Email)}");
        IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(_httpContextAccessor.HttpContext!);
        LoggerUtilities.Info($"[Process] IP for lookup: {ipInfo?.Ip}");
        LoggerUtilities.Info($"[Start] Querying DB for login attempts for {MaskEmail(user.Email)}");
        LoginAttempt? loginAttempt = await _db.LoginAttempts
            .FirstOrDefaultAsync(la => la.UserId == user.Id || la.Ip == ipInfo!.Ip);
        if (loginAttempt == null)
            LoggerUtilities.Info($"[End] No login attempt record found for {MaskEmail(user.Email)} or IP {ipInfo?.Ip}");
        else
            LoggerUtilities.Info(
                $"[End] Found login attempt for {MaskEmail(user.Email)}: TryAttempt={loginAttempt.TryAttempt}, IsBlocked={loginAttempt.IsBlocked}");
        return loginAttempt;
    }

    /// <inheritdoc />
    public async Task<LoginAttempt?> UpdateLoginAttemptAsync(User user)
    {
        try
        {
            LoggerUtilities.Info($"[Start] Processing login attempt for {MaskEmail(user.Email)}");
            LoggerUtilities.Info($"[Start] Retrieving IP for {MaskEmail(user.Email)}");
            IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(_httpContextAccessor.HttpContext!);
            LoggerUtilities.Info($"[Process] IP for update: {ipInfo?.Ip}");
            LoggerUtilities.Info($"[Start] Querying DB for existing login attempt for {MaskEmail(user.Email)}");
            LoginAttempt? loginAttempt = await _db.LoginAttempts
                .FirstOrDefaultAsync(la => la.UserId == user.Id || la.Ip == ipInfo!.Ip);
            if (loginAttempt == null)
            {
                loginAttempt = new LoginAttempt
                {
                    TryAttempt = 1,
                    Ip = ipInfo?.Ip,
                    UserId = user.Id
                };
                _db.LoginAttempts.Add(loginAttempt);
                LoggerUtilities.Info(
                    $"[Process] Created new LoginAttempt for {MaskEmail(user.Email)} with IP {ipInfo?.Ip}");
            }
            else
            {
                loginAttempt.TryAttempt++;
                LoggerUtilities.Info(
                    $"[Process] Incremented TryAttempt for {MaskEmail(user.Email)} to {loginAttempt.TryAttempt}");
                if (loginAttempt.TryAttempt >= 3)
                {
                    loginAttempt.IsBlocked = true;
                    loginAttempt.ExpiredAt = DateTime.Now.ToLocalTime().AddMinutes(15);
                    LoggerUtilities.Warning(
                        $"[Process] User {MaskEmail(user.Email)} is blocked due to {loginAttempt.TryAttempt} failed attempts. Block expires at {loginAttempt.ExpiredAt}");
                    string encodedEmail = Uri.EscapeDataString(user.Email);
                    string url = ForgotPasswordUrl + encodedEmail;
                    IllegalLoginVm illegalLoginVm = new IllegalLoginVm
                    {
                        Email = user.Email,
                        Ip = ipInfo?.Ip!,
                        Url = url
                    };
                    string body = await _emailService.IllegalLogin(illegalLoginVm);
                    MailerConfig mail = new MailerConfig
                    {
                        To = [user.Email],
                        Subject = EmailSubject.IllegalLoginSubject,
                        Body = body
                    };
                    LoggerUtilities.Info($"[Process] Sending illegal login notification to {MaskEmail(user.Email)}");
                    await _mailerService.SendEmail(mail);
                    LoggerUtilities.Info($"[End] Illegal login notification sent to {MaskEmail(user.Email)}");
                }
            }

            LoggerUtilities.Info($"[Start] Saving login attempt changes for {MaskEmail(user.Email)}");
            await _db.SaveChangesAsync();
            LoggerUtilities.Info(
                $"[End] Saved login attempt for {MaskEmail(user.Email)}: TryAttempt={loginAttempt.TryAttempt}, IsBlocked={loginAttempt.IsBlocked}");
            return loginAttempt;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error checking login attempts for {MaskEmail(user.Email)}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task ClearUserLoginAttemptsAsync(User user)
    {
        LoggerUtilities.Info($"[Start] Clearing login attempts for {MaskEmail(user.Email)}");
        await _db.LoginAttempts
            .Where(la => la.UserId == user.Id)
            .ExecuteDeleteAsync();
        LoggerUtilities.Info($"[End] Cleared login attempts for {MaskEmail(user.Email)}");
    }

    /// <inheritdoc />
    public async Task SyncExpiredLoginAttemptsAsync()
    {
        LoggerUtilities.Info("[Start] Removing expired login attempts");
        DateTime now = DateTime.Now.ToLocalTime();
        await _db.LoginAttempts
            .Where(la => la.ExpiredAt < now)
            .ExecuteDeleteAsync();
        LoggerUtilities.Info("[End] Removed expired login attempts");
    }
}
