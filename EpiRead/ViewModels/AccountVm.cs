using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

namespace EpiRead.ViewModels;

// Profile Page
public class ProfileVm
{
    public string Id { get; set; } = string.Empty;

    [DisplayName("First Name")] public string FirstName { get; set; } = string.Empty;

    [DisplayName("Last Name")] public string LastName { get; set; } = string.Empty;

    [DisplayName("Username")]
    [Remote("IsUserNameUnique", "User", ErrorMessage = "Username already exists. Please choose another username.",
        HttpMethod = "POST", AdditionalFields = nameof(Id))]
    public string UserName { get; set; } = string.Empty;

    [DataType(DataType.PhoneNumber)]
    [DisplayName("Phone Number")]
    [MaxLength(15, ErrorMessage = Common.ErrorMessage)]
    [RegularExpression(Common.PhoneNumber, ErrorMessage = Common.PhoneNumberErrorMessage)]

    public string? ProfileImagePath { get; set; }

    public string? ProfileImageLink { get; set; }

    [DataType(DataType.Date)]
    [DisplayName("Birthday")]
    public DateOnly? Birthday { get; set; }

    [DisplayName("Preferred Language")] public required string Language { get; set; }

    public IFormFile? ProfileImage { get; set; }
}

// Change Email Page
public class ChangeEmailVm
{
    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [DisplayName("Old Email")]
    [ReadOnly(true)]
    public string OldEmail { get; set; } = string.Empty;

    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [DisplayName("New Email")]
    [Remote("IsEmailUnique", "User", ErrorMessage = Common.EmailExistsErrorMessage, HttpMethod = "POST")]
    public string Email { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [DisplayName("Password Verification")]
    public string Password { get; set; } = string.Empty;
}

// Change Password Page
public class ChangePasswordVm
{
    [DataType(DataType.Password)]
    [DisplayName("Old Password")]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    public string OldPassword { get; set; } = string.Empty;

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

// Enable MFA Page
public class EnableMfaVm
{
    public string? Email { get; set; } = string.Empty;

    public string? QrCodeUrl { get; set; } = string.Empty;

    public string? ManualEntryCode { get; set; } = string.Empty;

    [StringLength(6, ErrorMessage = "Invalid {0}")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Invalid {0}")]
    public string Code { get; set; } = string.Empty;
}

// Disable MFA Page
public class DisableMfaVm
{
    [DataType(DataType.Password)]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("ConfirmPassword", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [DisplayName("Confirm Password")]
    [MinLength(Common.PasswordLength, ErrorMessage = Common.PasswordErrorMessage)]
    [Compare("Password", ErrorMessage = Common.PasswordDoesNotMatchErrorMessage)]
    public string ConfirmPassword { get; set; } = string.Empty;

    [StringLength(6, ErrorMessage = "Invalid {0}")]
    [RegularExpression(@"^[0-9]{6}$", ErrorMessage = "Invalid {0}")]
    public string Code { get; set; } = string.Empty;
}