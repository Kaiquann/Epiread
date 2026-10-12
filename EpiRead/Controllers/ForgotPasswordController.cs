using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

public class ForgotPasswordController : Controller
{
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IMailerHelper _mailerService;
    private readonly IResetPasswordService _resetPasswordService;

    public ForgotPasswordController(DB db, IEmailService emailService, IMailerHelper mailerService,
        ICfCaptchaHelper cfCaptchaHelper, IResetPasswordService resetPasswordService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _resetPasswordService = resetPasswordService ?? throw new ArgumentNullException(nameof(resetPasswordService));
    }

    public async Task<IActionResult> Index(string? email)
    {
        LoggerUtilities.Info("[Start] Loading forgot password page");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");

        DateTime now = DateTime.Now.ToLocalTime();
        await _db.ResetPasswords
            .Where(rp => rp.ExpiresAt < now)
            .ExecuteDeleteAsync();
        LoggerUtilities.Info("[Process] Cleaned up expired reset tokens");

        ForgotPasswordVm forgotPasswordVm = new()
        {
            Email = email ?? string.Empty
        };

        return View(forgotPasswordVm);
    }

    [HttpPost]
    public async Task<IActionResult> Index(ForgotPasswordVm forgotPasswordVm)
    {
        LoggerUtilities.Info($"[Start] Forgot password request email={MaskEmail(forgotPasswordVm.Email)}");
        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View(forgotPasswordVm);
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View(forgotPasswordVm);
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == forgotPasswordVm.Email);
        LoggerUtilities.Info($"[Process] User lookup userFound={user != null}");
        if (user == null)
        {
            TempData[SweetAlert.Danger] = "Email does not exist.";
            return View(forgotPasswordVm);
        }

        string token = Guid.NewGuid().ToString();

        ResetPassword? resetPassword = await _resetPasswordService.GetResetPasswordByUserIdAsync(user.Id.ToString());
        if (resetPassword != null)
        {
            resetPassword.UserId = user.Id;
            resetPassword.Token = token;
            LoggerUtilities.Info("[Process] Updated existing reset token");
        }
        else
        {
            resetPassword = new ResetPassword
            {
                UserId = user.Id,
                Token = token
            };
            await _db.ResetPasswords.AddAsync(resetPassword);
            LoggerUtilities.Info("[Process] Created new reset token");
        }

        await _db.SaveChangesAsync();

        string? url = Url.Action("", "ResetPassword", new { token }, Request.Scheme);

        ResetPasswordVm resetPasswordVm = new()
        {
            Url = url!
        };

        string body = await _emailService.ResetPassword(resetPasswordVm);
        MailerConfig mail = new()
        {
            To = [forgotPasswordVm.Email],
            Subject = EmailSubject.ResetPasswordSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Reset password email sent");

        TempData[SweetAlert.Success] = "Check your email to reset your password.";
        return RedirectToAction("Index", "Login");
    }
}