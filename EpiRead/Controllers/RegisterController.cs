using System.Text.Json;
using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

public class RegisterController : Controller
{
    private const string RegisterData = "RegisterData";
    private const string VerificationToken = "VerificationToken";
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IMailerHelper _mailerService;

    public RegisterController(DB db, IEmailService emailService, IMailerHelper mailerService,
        ICfCaptchaHelper cfCaptchaHelper)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
    }

    public async Task<IActionResult> Index()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser != null) return RedirectToAction("Index", "Home");
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Index(RegisterVm registerVm)
    {
        LoggerUtilities.Info($"[Start] Registration attempt email={MaskEmail(registerVm.Email)}");
        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View(registerVm);
        }

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View(registerVm);
        }

        bool isEmailExists = await _db.IsEmailExists(registerVm.Email);
        LoggerUtilities.Info($"[Process] Email uniqueness check emailExists={isEmailExists}");
        if (isEmailExists)
        {
            TempData[SweetAlert.Danger] = Common.EmailExistsErrorMessage;
            return View(registerVm);
        }

        bool isUserNameExists = await _db.IsUserNameExists(registerVm.Username);
        LoggerUtilities.Info($"[Process] Username uniqueness check usernameExists={isUserNameExists}");
        if (isUserNameExists)
        {
            TempData[SweetAlert.Danger] = "Username already exists. Please choose another username.";
            return View(registerVm);
        }

        string verificationToken = GetRandomString(8);

        HttpContext.Session.SetString(RegisterData, JsonSerializer.Serialize(registerVm));
        HttpContext.Session.SetString(VerificationToken, verificationToken);

        TokenVm tokenVm = new()
        {
            Token = verificationToken
        };
        string body = await _emailService.Token(tokenVm);

        MailerConfig mail = new()
        {
            To = [registerVm.Email],
            Subject = EmailSubject.TokenSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Verification email sent");

        return RedirectToAction("Verification");
    }

    public IActionResult Verification()
    {
        string? registerData = HttpContext.Session.GetString(RegisterData);
        string? verificationToken = HttpContext.Session.GetString(VerificationToken);

        if (registerData != null || verificationToken != null) return View();

        TempData[SweetAlert.Danger] = "Session expired. Please register again.";
        return RedirectToAction("Index");
    }

    [HttpPost]
    public async Task<IActionResult> Verification(string token)
    {
        LoggerUtilities.Info("[Start] Email verification attempt");
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

        string? registerData = HttpContext.Session.GetString(RegisterData);
        string? verificationToken = HttpContext.Session.GetString(VerificationToken);

        if (registerData == null || verificationToken == null)
        {
            TempData[SweetAlert.Danger] = "Session expired. Please register again.";
            return RedirectToAction("Index");
        }

        if (token != verificationToken)
        {
            TempData[SweetAlert.Danger] = "Invalid verification token.";
            return View();
        }

        RegisterVm? registerVm = JsonSerializer.Deserialize<RegisterVm>(registerData!);
        if (registerVm == null)
        {
            ClearData();
            TempData[SweetAlert.Danger] = "Invalid verification token.";
            return View();
        }

        User user = new()
        {
            FirstName = registerVm.FirstName,
            LastName = registerVm.LastName,
            UserName = registerVm.Username,
            Email = registerVm.Email,
            Password = HashPassword(registerVm.Password)
        };

        await _db.Users.AddAsync(user);
        await _db.SaveChangesAsync();
        LoggerUtilities.Info($"[End] User registered successfully userId={user.Id}");

        ClearData();
        TempData[SweetAlert.Success] = "Registered successfully.";
        return RedirectToAction("", "Login");
    }

    private void ClearData()
    {
        HttpContext.Session.Remove(RegisterData);
        HttpContext.Session.Remove(VerificationToken);
    }
}