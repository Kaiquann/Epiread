using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace EpiRead.ViewModels;

// Register Page
public class RegisterVm
{
    [DisplayName("First Name")]
    // only allow letters and spaces
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "First Name can only contain letters and spaces.")]
    public string FirstName { get; set; } = string.Empty;

    [DisplayName("Last Name")]
    // only allow letters and spaces
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Last Name can only contain letters and spaces.")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [Remote("IsEmailUnique", "User", ErrorMessage = Common.EmailExistsErrorMessage, HttpMethod = "POST")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("ConfirmPassword", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("Password", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [DisplayName("Username")]
    // only allow letters, numbers, and underscores
    [RegularExpression("^[a-zA-Z0-9_]+$",
        ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
    [StringLength(15, ErrorMessage = Common.ErrorMessage)]
    [Remote("IsUserNameUnique", "User", ErrorMessage = "Username already exists. Please choose another username.",
        HttpMethod = "POST")]
    public string Username { get; set; } = string.Empty;
}

// Login Page
public class LoginVm
{
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    public string Password { get; set; } = string.Empty;

    [DisplayName("Remember me?")] public bool RememberMe { get; set; } = false;
}

// MFA Page
public class LoginMfaVm
{
    [StringLength(6, ErrorMessage = "Invalid {0}")]
    public string Code { get; set; } = string.Empty;
}

// Email Verification Page
public class EmailVerificationVm
{
    public string Token { get; set; } = string.Empty;
}

// Forgot Password Page
public class ForgotPasswordVm
{
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;
}

// Reset Password Page
public partial class ResetPasswordVm
{
    public string Token { get; set; } = string.Empty;

    [EmailAddress] public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [DisplayName("New Password")]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("ConfirmNewPassword", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [DisplayName("Confirm New Password")]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("NewPassword", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string ConfirmNewPassword { get; set; } = string.Empty;
}

// Recovery MFA Page
public class RecoveryMfaVm
{
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    public string Email { get; set; } = string.Empty;
}

// Reset MFA Page
public partial class ResetMfaVm
{
    // Form
    public string Token { get; set; } = string.Empty;

    [StringLength(6, ErrorMessage = "Invalid {0}")]
    public string Code { get; set; } = string.Empty;

    // Vm
    public string? Email { get; set; }

    public string? QrCodeUrl { get; set; }

    public string? ManualEntryCode { get; set; }
}