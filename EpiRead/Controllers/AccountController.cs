using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

[Authorize(Policy = AuthorizationPolicies.AuthenticatedUsersOnly)]
public class AccountController : Controller
{
    private const string ChangeEmailData = "ChangeEmailData";
    private const string VerificationToken = "VerificationToken";
    private const string GoogleMfaData = "GoogleMfaData";
    private readonly ICfCaptchaHelper _cfCaptchaHelper;
    private readonly DB _db;
    private readonly IEmailService _emailService;
    private readonly IIpInfoHelper _ipInfoService;
    private readonly IMailerHelper _mailerService;
    private readonly IUserService _userService;

    public AccountController(DB db, IEmailService emailService, IMailerHelper mailerService,
        IIpInfoHelper ipInfoService, ICfCaptchaHelper cfCaptchaHelper, IUserService userService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _mailerService = mailerService ?? throw new ArgumentNullException(nameof(mailerService));
        _ipInfoService = ipInfoService ?? throw new ArgumentNullException(nameof(ipInfoService));
        _cfCaptchaHelper = cfCaptchaHelper ?? throw new ArgumentNullException(nameof(cfCaptchaHelper));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    public async Task<IActionResult> Profile()
    {
        LoggerUtilities.Info("[Start] Loading user profile");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();
        LoggerUtilities.Info("[End] Profile data prepared");
        return View(currentUser);
    }

    public async Task<IActionResult> EditProfile()
    {
        LoggerUtilities.Info("[Start] Loading edit profile page");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        ProfileVm profileVm = new ProfileVm
        {
            Id = currentUser.Id.ToString(),
            FirstName = currentUser.FirstName,
            LastName = currentUser.LastName,
            UserName = currentUser.UserName,
            ProfileImagePath = currentUser.ProfileImagePath,
            ProfileImageLink = currentUser.ProfileImageLink,
            Birthday = currentUser.DateOfBirth,
            Language = currentUser.Language
        };

        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        if (Request.IsAjax()) return PartialView("_EditProfile", profileVm);

        return View(profileVm);
    }

    [HttpPost]
    public async Task<IActionResult> EditProfile(ProfileVm profileVm)
    {
        LoggerUtilities.Info("[Start] Updating user profile");
        try
        {
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            // Validate username if changed
            if (currentUser.UserName != profileVm.UserName)
            {
                bool isUserNameExists = await _db.IsUserNameExists(profileVm.UserName);
                LoggerUtilities.Info($"[Process] Username check usernameExists={isUserNameExists}");
                if (isUserNameExists)
                    throw new Exception("Username already exists. Please choose another username.");
            }

            // Handle profile image update
            if (profileVm.ProfileImage != null)
            {
                (bool success, string? imageError) =
                    await _userService.UpdateUserProfileImageAsync(currentUser, profileVm.ProfileImage,
                        Common.ImagePath);

                if (!success)
                    throw new Exception(imageError);
            }

            // Validate language (if provided)
            if (!LanguageHelper.IsValidLanguage(profileVm.Language)) throw new Exception("Invalid language!");

            currentUser.FirstName = profileVm.FirstName;
            currentUser.LastName = profileVm.LastName;
            currentUser.UserName = profileVm.UserName;
            currentUser.DateOfBirth = profileVm.Birthday;
            currentUser.Language = profileVm.Language;
            currentUser.UpdatedAt = DateTime.Now.ToLocalTime();

            await _db.SaveChangesAsync();
            LoggerUtilities.Info("[End] Profile updated successfully");

            TempData[SweetAlert.Success] = "Profile updated successfully";
        }
        catch (Exception ex)
        {
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    [HttpPost]
    public async Task<IActionResult> RemoveProfileImage()
    {
        LoggerUtilities.Info("[Start] Removing profile image");
        try
        {
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            (bool success, string? error) = await _userService.RemoveUserProfileImageAsync(currentUser);

            if (!success)
            {
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                await _db.SaveChangesAsync();
                LoggerUtilities.Info("[End] Profile image removed successfully");
                TempData[SweetAlert.Success] = "User profile image removed successfully";
            }
        }
        catch (Exception ex)
        {
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    public async Task<IActionResult> ChangeEmail()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        ChangeEmailVm changeEmailVm = new ChangeEmailVm
        {
            OldEmail = currentUser.Email
        };

        return View(changeEmailVm);
    }

    [HttpPost]
    public async Task<IActionResult> ChangeEmail(ChangeEmailVm changeEmailVm)
    {
        LoggerUtilities.Info($"[Start] Change email request newEmail={MaskEmail(changeEmailVm.Email)}");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("ChangeEmail");
        }

        if (!VerifyPassword(currentUser.Password, changeEmailVm.Password))
        {
            TempData[SweetAlert.Danger] = "Invalid password";
            return RedirectToAction("ChangeEmail");
        }

        if (currentUser.Email == changeEmailVm.Email)
        {
            TempData[SweetAlert.Danger] = "New email is the same as the old email";
            return RedirectToAction("ChangeEmail");
        }

        bool isEmailExists = await _db.IsEmailExists(changeEmailVm.Email);
        LoggerUtilities.Info($"[Process] Email uniqueness check emailExists={isEmailExists}");
        if (isEmailExists)
        {
            TempData[SweetAlert.Danger] = Common.EmailExistsErrorMessage;
            return RedirectToAction("ChangeEmail");
        }

        string verificationToken = GetRandomString(8);

        HttpContext.Session.SetString(ChangeEmailData, JsonSerializer.Serialize(changeEmailVm));
        HttpContext.Session.SetString(VerificationToken, verificationToken);

        TokenVm tokenVm = new TokenVm
        {
            Token = verificationToken
        };
        string body = await _emailService.Token(tokenVm);

        MailerConfig mail = new MailerConfig
        {
            To = [changeEmailVm.Email],
            Subject = EmailSubject.TokenSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Verification email sent");

        return RedirectToAction("Verification");
    }

    public async Task<IActionResult> Verification()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        string? changeEmailData = HttpContext.Session.GetString(ChangeEmailData);
        string? verificationToken = HttpContext.Session.GetString(VerificationToken);

        if (changeEmailData == null || verificationToken == null)
        {
            TempData[SweetAlert.Danger] = "Session expired. Please register again.";
            return RedirectToAction("ChangeEmail");
        }

        return View();
    }

    [HttpPost]
    public async Task<IActionResult> Verification(string token)
    {
        LoggerUtilities.Info("[Start] Email change verification");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!_cfCaptchaHelper.IsCaptchaResponseValid(Request))
        {
            TempData[SweetAlert.Danger] = Common.CaptchaErrorMessage;
            return View();
        }

        string? changeEmailData = HttpContext.Session.GetString(ChangeEmailData);
        string? verificationToken = HttpContext.Session.GetString(VerificationToken);

        if (changeEmailData == null || verificationToken == null)
        {
            TempData[SweetAlert.Danger] = "Session expired. Please register again.";
            return RedirectToAction("ChangeEmail");
        }

        if (token != verificationToken)
        {
            TempData[SweetAlert.Danger] = "Invalid verification token.";
            return View("Verification");
        }

        ChangeEmailVm? changeEmailVm = JsonSerializer.Deserialize<ChangeEmailVm>(changeEmailData);

        if (changeEmailVm == null)
        {
            ClearData();
            TempData[SweetAlert.Danger] = "Invalid verification token.";
            return RedirectToAction("ChangeEmail");
        }

        currentUser.Email = changeEmailVm.Email;
        currentUser.UpdatedAt = DateTime.Now.ToLocalTime();
        await _db.SaveChangesAsync();
        LoggerUtilities.Info($"[Process] Email updated in database newEmail={MaskEmail(changeEmailVm.Email)}");

        IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(HttpContext);
        LoggerUtilities.Info("[Process] Retrieved IP information");

        string subject = "Email Changed Successfully";
        string message = "You have changed your email from " + changeEmailVm.OldEmail + " to " + changeEmailVm.Email +
                         ". IP: " + ipInfo?.Ip! + " on " + DateTime.Now.ToLocalTime();

        SimpleTitleMessageVm simpleTitleMessageVm = new SimpleTitleMessageVm
        {
            Title = subject,
            Message = message
        };
        string simpleTitleMessage = await _emailService.SimpleTitleMessage(simpleTitleMessageVm);

        MailerConfig mail = new MailerConfig
        {
            To = [currentUser.Email],
            Cc = [changeEmailVm.OldEmail],
            Subject = subject,
            Body = simpleTitleMessage
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Email change notification sent");

        ClearData();
        TempData[SweetAlert.Success] = subject;
        return RedirectToAction("Profile");
    }

    public IActionResult ChangePassword()
    {
        return View();
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm changePasswordVm)
    {
        LoggerUtilities.Info("[Start] Change password request");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return View(changePasswordVm);
        }

        bool isValidPassword = VerifyPassword(currentUser.Password, changePasswordVm.OldPassword);

        if (!isValidPassword)
        {
            TempData[SweetAlert.Danger] = "Old password is incorrect";
            return View(changePasswordVm);
        }

        currentUser.Password = HashPassword(changePasswordVm.NewPassword);
        currentUser.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[Process] Password updated in database");

        IpInfoResponse? ipInfo = await _ipInfoService.GetIpInfo(HttpContext);
        LoggerUtilities.Info("[Process] Retrieved IP information");

        string? url = Url.Action("Index", "ForgotPassword", new { email = currentUser.Email }, Request.Scheme);

        PasswordChangedVm passwordChangedVm = new PasswordChangedVm
        {
            DateTime = DateTime.Now.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss"),
            Email = currentUser.Email,
            Ip = ipInfo?.Ip!,
            Url = url!
        };
        string body = await _emailService.PasswordChanged(passwordChangedVm);

        MailerConfig mail = new MailerConfig
        {
            To = [currentUser.Email],
            Subject = EmailSubject.PasswordChangedSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
        LoggerUtilities.Info("[End] Password change notification sent");

        TempData[SweetAlert.Success] = "Password changed successfully";
        return RedirectToAction("Profile");
    }

    public async Task<IActionResult> EnableMfa()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (currentUser.MfaEnabled)
        {
            TempData[SweetAlert.Danger] = "MFA is already enabled";
            return RedirectToAction("Profile");
        }

        GoogleMfaConfig googleMfa = GoogleMfaHelper.GenerateMfa(Common.CompanyName, currentUser.Email);
        HttpContext.Session.Set(GoogleMfaData, googleMfa);

        EnableMfaVm multiFactorAuthVm = new EnableMfaVm
        {
            Email = currentUser.Email,
            QrCodeUrl = googleMfa.QrCodeUrl,
            ManualEntryCode = googleMfa.ManualEntryCode
        };

        return View(multiFactorAuthVm);
    }

    [HttpPost]
    public async Task<IActionResult> EnableMfa(EnableMfaVm enableMfaVm)
    {
        LoggerUtilities.Info("[Start] Enable MFA request");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (currentUser.MfaEnabled)
        {
            TempData[SweetAlert.Danger] = "MFA is already enabled";
            return RedirectToAction("Profile");
        }

        GoogleMfaConfig? googleMfa = HttpContext.Session.Get<GoogleMfaConfig>(GoogleMfaData);
        if (googleMfa == null)
        {
            TempData[SweetAlert.Danger] = "Session expired. Please enable MFA again.";
            return RedirectToAction("EnableMfa");
        }

        enableMfaVm.Email = currentUser.Email;
        enableMfaVm.QrCodeUrl = googleMfa.QrCodeUrl;
        enableMfaVm.ManualEntryCode = googleMfa.ManualEntryCode;

        bool isValid2Fa = GoogleMfaHelper.ValidateMfa(googleMfa.SecretKey, enableMfaVm.Code);
        if (!isValid2Fa)
        {
            TempData[SweetAlert.Danger] = "Invalid Code";
            ModelState.AddModelError("Code", "Invalid Code");
            return View(enableMfaVm);
        }

        currentUser.MfaEnabled = true;
        currentUser.GoogleMfaSecretKey = googleMfa.SecretKey;
        currentUser.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[Process] MFA enabled in database");

        string message = "MFA enabled successfully on " + DateTime.Now.ToLocalTime();
        await SendMfaAlert(currentUser, message);
        LoggerUtilities.Info("[End] MFA alert email sent");

        TempData[SweetAlert.Success] = "MFA Enabled Successfully";
        return RedirectToAction("Profile");
    }

    public async Task<IActionResult> DisableMfa()
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (currentUser.MfaEnabled) return View();

        TempData[SweetAlert.Danger] = "MFA is not enabled";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public async Task<IActionResult> DisableMfa(DisableMfaVm disableMfaVm)
    {
        LoggerUtilities.Info("[Start] Disable MFA request");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!currentUser.MfaEnabled)
        {
            TempData[SweetAlert.Danger] = "MFA is not enabled";
            return RedirectToAction("Profile");
        }

        bool isValidPassword = VerifyPassword(currentUser.Password, disableMfaVm.Password);
        if (!isValidPassword)
        {
            TempData[SweetAlert.Danger] = "Invalid Password";
            return View();
        }

        bool isValidCode =
            GoogleMfaHelper.ValidateMfa(currentUser.GoogleMfaSecretKey ?? string.Empty, disableMfaVm.Code);
        if (!isValidCode)
        {
            TempData[SweetAlert.Danger] = "Invalid Code";
            return View();
        }

        currentUser.MfaEnabled = false;
        currentUser.GoogleMfaSecretKey = string.Empty;
        currentUser.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();
        LoggerUtilities.Info("[Process] MFA disabled in database");

        string message = "MFA disabled successfully on " + DateTime.Now.ToLocalTime();
        await SendMfaAlert(currentUser, message);
        LoggerUtilities.Info("[End] MFA alert email sent");

        TempData[SweetAlert.Success] = "MFA Disabled Successfully";
        return RedirectToAction("Profile");
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync();
        TempData[SweetAlert.Success] = "Log out successful";
        return RedirectToAction("Index", "Home");
    }

    private async Task SendMfaAlert(User user, string message)
    {
        SimpleTitleMessageVm simpleTitleMessageVm = new SimpleTitleMessageVm
        {
            Title = EmailSubject.MfaAlertSubject,
            Message = message
        };
        string body = await _emailService.SimpleTitleMessage(simpleTitleMessageVm);

        MailerConfig mail = new MailerConfig
        {
            To = [user.Email],
            Subject = EmailSubject.MfaAlertSubject,
            Body = body
        };
        await _mailerService.SendEmail(mail);
    }

    private void ClearData()
    {
        HttpContext.Session.Remove(ChangeEmailData);
        HttpContext.Session.Remove(VerificationToken);
    }
}