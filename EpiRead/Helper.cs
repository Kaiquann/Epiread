using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead;

public class Helper
{
    private static readonly PasswordHasher<object> PasswordHasher = new();
    private readonly IConfiguration _cf;
    private readonly IHttpContextAccessor _ct;

    private readonly IWebHostEnvironment _en;

    public Helper(IWebHostEnvironment en, IHttpContextAccessor ct, IConfiguration cf)
    {
        _en = en;
        _ct = ct;
        _cf = cf;
    }

    public static string HashPassword(string password)
    {
        return PasswordHasher.HashPassword(0, password);
    }

    public static bool VerifyPassword(string hash, string password)
    {
        return PasswordHasher.VerifyHashedPassword(0, hash, password) == PasswordVerificationResult.Success;
    }

    // Return January (1) to December (12)
    public static SelectList GetMonthList()
    {
        List<object> list = new();

        for (int n = 1; n <= 12; n++)
            list.Add(new
            {
                Id = n,
                Name = new DateTime(1, n, 1).ToString("MMMM")
            });

        return new SelectList(list, "Id", "Name");
    }

    // Return min to max years
    public static SelectList GetYearList(int min, int max, bool reverse = false)
    {
        List<int> list = new();

        for (int n = min; n <= max; n++) list.Add(n);

        if (reverse) list.Reverse();

        return new SelectList(list);
    }

    public static bool IsValidYear(int year, int? min = 1900)
    {
        return year >= (min ?? 1900) && year <= DateTime.Now.Year;
    }

    public static string Encrypt<T>(T data)
    {
        byte[] jsonData = JsonSerializer.SerializeToUtf8Bytes(data);
        return Convert.ToBase64String(jsonData);
    }

    public static T? Decrypt<T>(string data)
    {
        byte[] encrypted = Convert.FromBase64String(data);
        return JsonSerializer.Deserialize<T>(encrypted);
    }

    public static string GetRandomString(int length = 10)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
        string randomString = "";
        Random random = new();
        for (int i = 1; i <= length; i++) randomString += chars[random.Next(chars.Length)];
        return randomString;
    }

    private const int MaxEmailMaskLength = 3;

    /// <summary>
    ///     Masks an email address for logging purposes to protect user privacy.
    ///     Shows first character of local part followed by asterisks (max 3) and full domain.
    ///     Examples: user@example.com becomes u***@example.com, ab@test.com becomes a*@test.com
    /// </summary>
    /// <param name="email">The email address to mask</param>
    /// <returns>Masked email address or original string if not a valid email format</returns>
    public static string MaskEmail(string? email)
    {
        if (string.IsNullOrEmpty(email)) return string.Empty;
        
        int atIndex = email.IndexOf('@');
        if (atIndex <= 0) return email; // Not a valid email format
        
        string localPart = email.Substring(0, atIndex);
        string domain = email.Substring(atIndex);
        
        // Show first character and mask the rest (up to MaxEmailMaskLength asterisks)
        string masked = localPart.Length > 1 
            ? localPart[0] + new string('*', Math.Min(localPart.Length - 1, MaxEmailMaskLength))
            : localPart;
        
        return masked + domain;
    }

    /// <summary>
    ///     Determines whether the given <see cref="BookCategory" /> represents the
    ///     built-in 'General' category. The check returns true when the category
    ///     has a Name equal to "General" or a Slug equal to "general".
    /// </summary>
    /// <param name="category">The <see cref="BookCategory" /> to check. May be null.</param>
    /// <returns>True when the category is recognized as 'General', otherwise false.</returns>
    public static bool IsGeneral(BookCategory? category)
    {
        if (category == null) return false;
        return category.Name == "General" || category.Slug == "general";
    }

    /// <summary>
    ///     Determines whether the specified name and slug identify the
    ///     built-in 'General' category. If either the name is "General" or the
    ///     slug is "general", true is returned.
    /// </summary>
    /// <param name="name">The category name to check. May be null.</param>
    /// <param name="slug">The category slug to check. May be null.</param>
    /// <returns>True when name equals "General" or slug equals "general"; otherwise false.</returns>
    public static bool IsGeneral(string? name, string? slug)
    {
        return name == "General" || slug == "general";
    }
}