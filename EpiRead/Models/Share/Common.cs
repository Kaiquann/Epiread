using System.Reflection;

namespace EpiRead.Models.Share;

public static class Common
{
    // --------- Constant Section ---------
    public const string CompanyName = "EpiRead";

    // Format
    public const string PhoneNumber = @"^[0-9]{10,11}$";
    public const int PasswordLength = 8;

    // Error Message
    public const string ErrorMessage = "Invalid {0}.";
    public const string InvalidRequestErrorMessage = "Invalid request.";

    public const string PasswordErrorMessage = "Password must be at least 8 characters long.";
    public const string PasswordDoesNotMatchErrorMessage = "Password does not match.";

    public const string EmailExistsErrorMessage = "Email already exists.";
    public const string PhoneNumberErrorMessage = "Invalid {0} within, 10 to 11 digit only , without '-' ";

    public const string CaptchaErrorMessage = "Captcha is required.";

    public const string PleaseLoginToContinueErrorMessage = "Please login to continue.";

    // Book Constants
    public const int MinPublicationYear = 1950;
    public static readonly int MaxPublicationYear = DateTime.Now.Year;

    // WWWRoot Path
    public const string ImagePath = "images/";
    public const string UploadPath = "uploads/";
    public const string EbookPath = "ebooks/";
    public const string DiscussionPath = "discussion/";

    // --------- Static Section ---------
    public static readonly string ProjectName = Assembly.GetEntryAssembly()?.GetName().Name ?? string.Empty;
}