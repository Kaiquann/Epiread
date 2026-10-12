namespace EpiRead.ViewModels;

public class SimpleTitleMessageVm
{
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class LoginAlertVm
{
    public string Email { get; set; } = string.Empty;
    public IpInfoResponse IpInfo { get; set; } = null!;
    public DateTime LoginTime { get; set; } = DateTime.Now.ToLocalTime();
    public string Url { get; set; } = string.Empty;
}

public class TokenVm
{
    public string Token { get; set; } = string.Empty;
}

public partial class ResetPasswordVm
{
    public string Url { get; set; } = string.Empty;
}

public class PasswordChangedVm
{
    public string DateTime { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class IllegalLoginVm
{
    public string Email { get; set; } = string.Empty;
    public string Ip { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public class AccountCreatedByAdminVm
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Url { get; set; } = string.Empty;
}

public partial class ResetMfaVm
{
    public string Url { get; set; } = string.Empty;
}

public class AccountChangeVm
{
    public string FieldName { get; set; } = string.Empty;
    public string OldValue { get; set; } = string.Empty;
    public string NewValue { get; set; } = string.Empty;
}

public class AccountUpdatedByAdminVm
{
    public string UserName { get; set; } = string.Empty;
    public string AdminName { get; set; } = string.Empty;
    public List<AccountChangeVm> Changes { get; set; } = new();
    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();
    public string Url { get; set; } = string.Empty;
    public bool IsEmailChanged { get; set; }
    public string? OldEmail { get; set; }
    public string? NewEmail { get; set; }
}