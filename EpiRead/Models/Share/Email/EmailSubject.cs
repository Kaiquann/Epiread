namespace EpiRead.Models.Share.Email;

/*
 * @author: Chong Jun Xiang
 * @description: Email design for OTP, Reset Password, Illegal Login, Password Changed
 */
public static class EmailSubject
{
    public const string TokenSubject = "Token";
    public const string ResetPasswordSubject = "Reset Password";
    public const string IllegalLoginSubject = "Illegal Login";
    public const string PasswordChangedSubject = "Password Changed";
    public const string AccountCreatedSubject = "Account Created";
    public const string LoginAlertSubject = "Login Alert";
    public const string MfaAlertSubject = "MFA Alert";
    public const string ResetMfaSubject = "Reset MFA";
}