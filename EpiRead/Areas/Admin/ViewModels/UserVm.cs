using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead.Areas.Admin.ViewModels;

public class CreateUserVm
{
    [DisplayName("First Name")]
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "First Name can only contain letters and spaces.")]
    public string FirstName { get; set; } = string.Empty;

    [DisplayName("Last Name")]
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Last Name can only contain letters and spaces.")]
    public string LastName { get; set; } = string.Empty;

    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [Remote("IsEmailUnique", "User", "", ErrorMessage = Common.EmailExistsErrorMessage, HttpMethod = "POST")]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    public IEnumerable<SelectListItem>? Roles { get; set; }
}

public class EditUserVm
{
    public string Id { get; set; } = string.Empty;

    [DisplayName("First Name")]
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "First Name can only contain letters and spaces.")]
    public string FirstName { get; set; } = string.Empty;

    [DisplayName("Last Name")]
    [RegularExpression("^[a-zA-Z ]+$", ErrorMessage = "Last Name can only contain letters and spaces.")]
    public string LastName { get; set; } = string.Empty;

    [DisplayName("Username")]
    // only allow letters, numbers, and underscores
    [RegularExpression("^[a-zA-Z0-9_]+$",
        ErrorMessage = "Username can only contain letters, numbers, and underscores.")]
    [StringLength(15, ErrorMessage = Common.ErrorMessage)]
    [Remote("IsUserNameUnique", "User", "", ErrorMessage = "Username already exists. Please choose another username.",
        HttpMethod = "POST", AdditionalFields = nameof(Id))]
    public string UserName { get; set; } = string.Empty;

    [EmailAddress]
    [DataType(DataType.EmailAddress)]
    [Remote("IsEmailUnique", "User", "", ErrorMessage = Common.EmailExistsErrorMessage, HttpMethod = "POST",
        AdditionalFields = nameof(Id))]
    public string Email { get; set; } = string.Empty;

    public string Role { get; set; } = string.Empty;

    [DisplayName("Date of Birth")]
    [DataType(DataType.Date)]
    public DateOnly? DateOfBirth { get; set; }

    [DisplayName("Language Preference")] public required string Language { get; set; }

    public string? ProfileImagePath { get; set; }

    public string? ProfileImageLink { get; set; }

    public IFormFile? ProfileImage { get; set; }

    public string Status { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();
}