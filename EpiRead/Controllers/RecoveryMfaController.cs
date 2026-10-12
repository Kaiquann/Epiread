using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Controllers;

public class RecoveryMfaController : Controller
{
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IMailerHelper _mailerService;
    private readonly IResetMfaService _resetMfaService;

    public RecoveryMfaController(DB db, IEmailService emailService, IMailerHelper mailerService,
        ICfCaptchaHelper cfCaptchaHelper, IResetMfaService resetMfaService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _resetMfaService = resetMfaService ?? throw new ArgumentNullException(nameof(resetMfaService));
    }

    public async Task<IActionResult> Index()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(RecoveryMfaVm recoveryMfaVm)
    {
        LoggerUtilities.Info($"[Start] Recovery MFA request email={MaskEmail(recoveryMfaVm.Email)}");
        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View(recoveryMfaVm);
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View(recoveryMfaVm);
        }

        User? user = await _db.Users.FirstOrDefaultAsync(u => u.Email == recoveryMfaVm.Email);
        LoggerUtilities.Info($"[Process] User lookup userFound={user != null}");
        if (user == null)
        {
            TempData[SweetAlert.Danger] = "Email does not exist.";
            return View(recoveryMfaVm);
        }

        if (!user.MfaEnabled)
        {
            TempData[SweetAlert.Danger] = "MFA is not enabled on this account.";
            return RedirectToAction();
        }

        ResetMfa? resetMfaDb = await _resetMfaService.GetResetMfaByUserIdAsync(user.Id.ToString());
        if (resetMfaDb != null) _db.ResetMfas.Remove(resetMfaDb);

        string token = Guid.NewGuid().ToString();
        ResetMfa resetMfa = new()
        {
            UserId = user.Id,
            Token = token
        };

        await _db.ResetMfas.AddAsync(resetMfa);
        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[Process] Reset MFA token created");

        string resetMfaUrl = Url.Action("", "ResetMfa", null, Request.Scheme) +
                             $"?token={token}";

        ResetMfaVm resetMfaVm = new()
        {
            Url = resetMfaUrl
        };

        string body = await _emailService.ResetMfa(resetMfaVm);
        MailerConfig mail = new()
        {
            To = [recoveryMfaVm.Email],
            Subject = EmailSubject.ResetMfaSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Reset MFA email sent");

        TempData[SweetAlert.Success] = "Check your email to reset your MFA.";
        return RedirectToAction("Index", "Login");
    }
}