using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

public class LoginController : Controller
{
    private const string LoginData = "LoginData";
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly ILoginService _loginService;

    public LoginController(ICfCaptchaHelper cfCaptchaHelper, DB db, ILoginService loginService)
    {
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _loginService = loginService ?? throw new ArgumentNullException(nameof(loginService));
    }

    public async Task<IActionResult> Index(string? returnUrl)
    {
        LoggerUtilities.Info("[Start] Loading login page");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");

        ViewBag.ReturnUrl = returnUrl;
        LoggerUtilities.Info("[End] Login page loaded");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(LoginVm loginVm, string? returnUrl)
    {
        LoggerUtilities.Info($"[Start] Login attempt email={MaskEmail(loginVm.Email)}");
        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return RedirectToAction();
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View(loginVm);
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == loginVm.Email);
        LoggerUtilities.Info($"[Process] User lookup completed userFound={user != null}");
        if (user == null)
        {
            TempData[SweetAlert.Danger] = "Email or password is incorrect.";
            return View(loginVm);
        }

        if (user.Status == nameof(UserStatusType.Blocked))
        {
            string blockMessage = "Account has been blocked.";
            if (!string.IsNullOrEmpty(user.BlockReason))
            {
                // Encode the BlockReason to prevent XSS attacks
                string encodedReason = HtmlEncoder.Default.Encode(user.BlockReason);
                blockMessage += $" Reason: {encodedReason}";
            }

            TempData[SweetAlert.Danger] = blockMessage;
            return View(loginVm);
        }

        if (user.Status == nameof(UserStatusType.Deactivated))
        {
            TempData[SweetAlert.Danger] = "Account has been deactivated.";
            return View(loginVm);
        }

        LoginAttempt? loginAttempt = await _loginService.FindLoginAttemptsAsync(user);
        LoggerUtilities.Info($"[Process] Login attempt check blocked={loginAttempt?.IsBlocked ?? false}");
        if (loginAttempt != null && loginAttempt.IsBlocked)
        {
            TempData[SweetAlert.Danger] = "IP and account has been blocked for 15 minutes.";
            return View(loginVm);
        }

        if (!VerifyPassword(user.Password, loginVm.Password))
        {
            LoggerUtilities.Info("[Process] Password verification failed");
            await _loginService.UpdateLoginAttemptAsync(user);
            TempData[SweetAlert.Danger] = "Email or password is incorrect.";
            return View(loginVm);
        }

        if (user.MfaEnabled)
        {
            HttpContext.Session.Set(LoginData, loginVm);
            return RedirectToAction("Mfa", new { returnUrl });
        }

        await _loginService.LoginAsync(user, loginVm.RememberMe);
        await _loginService.ClearUserLoginAttemptsAsync(user);
        LoggerUtilities.Info("[End] Login successful");

        if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult Mfa(string? returnUrl)
    {
        LoginVm? loginVm = HttpContext.Session.Get<LoginVm>(LoginData);
        if (loginVm == null)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("Index");
        }

        ViewBag.ReturnUrl = returnUrl;
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Mfa(LoginMfaVm loginMfaVm, string? returnUrl)
    {
        LoggerUtilities.Info("[Start] MFA verification attempt");
        LoginVm? loginVm = HttpContext.Session.Get<LoginVm>(LoginData);
        if (loginVm == null)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("Index");
        }

        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View();
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View();
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == loginVm.Email);
        LoggerUtilities.Info($"[Process] User lookup for MFA userFound={user != null}");
        if (user == null)
        {
            TempData[SweetAlert.Danger] = "Email or password is incorrect.";
            return RedirectToAction("Index");
        }

        bool isValid2Fa = GoogleMfaHelper.ValidateMfa(user.GoogleMfaSecretKey, loginMfaVm.Code);
        if (!isValid2Fa)
        {
            TempData[SweetAlert.Danger] = "Invalid Code";
            return View();
        }

        await _loginService.LoginAsync(user, loginVm.RememberMe);
        await _loginService.ClearUserLoginAttemptsAsync(user);
        LoggerUtilities.Info("[End] MFA login successful");

        HttpContext.Session.Remove(LoginData);

        if (!string.IsNullOrEmpty(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction("Index", "Home");
    }
}