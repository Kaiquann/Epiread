using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

public class ResetPasswordController : Controller
{
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IIpInfoHelper _ipInfoService;
    private readonly ILoginService _loginService;
    private readonly IMailerHelper _mailerService;
    private readonly IResetPasswordService _resetPasswordService;

    public ResetPasswordController(DB db, ICfCaptchaHelper cfCaptchaHelper, ILoginService loginService,
        IResetPasswordService resetPasswordService, IIpInfoHelper ipInfoService, IMailerHelper mailerService,
        IEmailService emailService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _loginService = loginService ?? throw new ArgumentNullException(nameof(loginService));
        _resetPasswordService = resetPasswordService ?? throw new ArgumentNullException(nameof(resetPasswordService));
        _ipInfoService = ipInfoService ?? throw new ArgumentNullException(nameof(ipInfoService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
    }

    public async Task<IActionResult> Index(string token)
    {
        LoggerUtilities.Info("[Start] Loading reset password page");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("", "ForgotPassword");
        }

        ResetPassword? resetPassword = await _resetPasswordService.GetResetPasswordByTokenAsync(token);
        LoggerUtilities.Info($"[Process] Token lookup tokenValid={resetPassword != null}");
        if (resetPassword == null)
        {
            TempData[SweetAlert.Danger] = "Invalid or expired token";
            return RedirectToAction("", "ForgotPassword");
        }

        ResetPasswordVm resetPasswordVm = new()
        {
            Token = token,
            Email = resetPassword.User.Email
        };

        return View(resetPasswordVm);
    }

    [HttpPost]
    public async Task<IActionResult> Index(ResetPasswordVm resetPasswordVm)
    {
        LoggerUtilities.Info("[Start] Reset password submission");
        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View(resetPasswordVm);
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("", "ResetPassword", new { token = resetPasswordVm.Token });
        }

        ResetPassword? resetPassword = await _resetPasswordService.GetResetPasswordByTokenAsync(resetPasswordVm.Token);
        LoggerUtilities.Info($"[Process] Token validation tokenValid={resetPassword != null}");
        if (resetPassword == null)
        {
            TempData[SweetAlert.Danger] = "Invalid or expired token";
            return RedirectToAction("", "ForgotPassword");
        }

        User? user = await _db.Users.FindAsync(resetPassword.UserId);
        if (user == null)
        {
            TempData[SweetAlert.Danger] = "Invalid User Id";
            return View(resetPasswordVm);
        }

        await _loginService.ClearUserLoginAttemptsAsync(user);

        user.Password = HashPassword(resetPasswordVm.NewPassword);
        user.UpdatedAt = DateTime.Now.ToLocalTime();

        _db.ResetPasswords.Remove(resetPassword);

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[Process] Password updated in database");

        IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(HttpContext);
        LoggerUtilities.Info("[Process] Retrieved IP information");

        string? url = Url.Action("Index", "ForgotPassword", new { email = user.Email }, Request.Scheme);

        PasswordChangedVm passwordChangedVm = new PasswordChangedVm
        {
            DateTime = DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            Email = user.Email,
            Ip = ipInfo?.Ip!,
            Url = url!
        };
        string body = await _emailService.PasswordChanged(passwordChangedVm);

        MailerConfig mail = new MailerConfig
        {
            To = [user.Email],
            Subject = EmailSubject.PasswordChangedSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Password changed email sent");

        TempData[SweetAlert.Success] = "Password reset successfully.";
        return RedirectToAction("", "Login");
    }
}