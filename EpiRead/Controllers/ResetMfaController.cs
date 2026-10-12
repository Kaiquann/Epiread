using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

public class ResetMfaController : Controller
{
    private const string GoogleMfaData = "GoogleMfaData";
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IResetMfaService _resetMfaService;

    public ResetMfaController(DB db, ICfCaptchaHelper cfCaptchaHelper, IResetMfaService resetMfaService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _resetMfaService = resetMfaService ?? throw new ArgumentNullException(nameof(resetMfaService));
    }

    public async Task<IActionResult> Index(string token)
    {
        LoggerUtilities.Info("[Start] Loading reset MFA page");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("", "RecoveryMfa");
        }

        ResetMfa? resetMfa = await _resetMfaService.GetResetMfaByTokenAsync(token);
        LoggerUtilities.Info($"[Process] Token lookup tokenValid={resetMfa != null}");
        if (resetMfa == null)
        {
            TempData[SweetAlert.Danger] = "Invalid or expired token";
            return RedirectToAction("", "RecoveryMfa");
        }

        GoogleMfaConfig googleMfa = GoogleMfaHelper.GenerateMfa(Common.CompanyName, resetMfa.User.Email);

        HttpContext.Session.Set(GoogleMfaData, googleMfa);

        ResetMfaVm resetMfaVm = new()
        {
            Email = resetMfa.User.Email,
            QrCodeUrl = googleMfa.QrCodeUrl,
            ManualEntryCode = googleMfa.ManualEntryCode
        };

        return View(resetMfaVm);
    }

    [HttpPost]
    public async Task<IActionResult> Index(ResetMfaVm resetMfaVm)
    {
        LoggerUtilities.Info("[Start] Reset MFA submission");
        GoogleMfaConfig? googleMfa = HttpContext.Session.Get<GoogleMfaConfig>(GoogleMfaData);
        if (googleMfa == null)
        {
            TempData[SweetAlert.Danger] = "Invalid Session";
            return RedirectToAction("", "RecoveryMfa");
        }

        ResetMfa? resetMfa = await _resetMfaService.GetResetMfaByTokenAsync(resetMfaVm.Token);
        LoggerUtilities.Info($"[Process] Token validation tokenValid={resetMfa != null}");
        if (resetMfa == null)
        {
            TempData[SweetAlert.Danger] = "Invalid or expired token";
            return RedirectToAction("", "RecoveryMfa");
        }

        resetMfaVm.Email = resetMfa.User.Email;
        resetMfaVm.QrCodeUrl = googleMfa.QrCodeUrl;
        resetMfaVm.ManualEntryCode = googleMfa.ManualEntryCode;

        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View(resetMfaVm);
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View();
        }

        bool isValid2Fa = GoogleMfaHelper.ValidateMfa(googleMfa.SecretKey, resetMfaVm.Code);
        if (!isValid2Fa)
        {
            TempData[SweetAlert.Danger] = "Invalid Code";
            return View(resetMfaVm);
        }

        resetMfa.User.GoogleMfaSecretKey = googleMfa.SecretKey;
        resetMfa.User.UpdatedAt = DateTime.Now.ToLocalTime();
        _db.ResetMfas.Remove(resetMfa);

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[End] MFA reset successfully");

        ClearData();
        TempData[SweetAlert.Success] = "MFA reset successfully";
        return RedirectToAction("", "Login");
    }

    private void ClearData()
    {
        HttpContext.Session.Remove(GoogleMfaData);
    }
}