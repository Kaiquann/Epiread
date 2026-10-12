using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

/// <summary>
///     Controller that handles user-related requests such as validating email uniqueness
///     and other user management endpoints used across the web app.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class UserController : Controller
{
    private readonly DB _db;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserController" /> class.
    /// </summary>
    /// <param name="db">Database context used to access and query user data.</param>
    public UserController(DB db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    /// <summary>
    ///     Checks whether the specified email is unique in the system.
    ///     Returns a JSON result containing a boolean where <c>true</c> indicates
    ///     the email is not used by any other user and <c>false</c> indicates the
    ///     email already exists.
    /// </summary>
    /// <param name="email">Email address to validate for uniqueness.</param>
    /// <returns>A JSON result containing a boolean that indicates uniqueness.</returns>
    [HttpPost]
    public async Task<IActionResult> IsEmailUnique(string? email, string? id = null)
    {
        LoggerUtilities.Info($"[Start] Checking email uniqueness email={MaskEmail(email)} id={id}");
        bool result = await _db.IsEmailUnique(email, id);
        LoggerUtilities.Info($"[End] Email uniqueness check result={result}");
        return Json(result);
    }

    /// <summary>
    ///     Checks whether the specified username is unique in the system.
    ///     Returns a JSON result containing a boolean where <c>true</c> indicates
    ///     the username is not used by any other user and <c>false</c> indicates the
    ///     username already exists.
    /// </summary>
    /// <param name="userName">Username to validate for uniqueness.</param>
    /// <param name="id">Optional user ID to exclude from uniqueness check (for editing own username).</param>
    /// <returns>A JSON result containing a boolean that indicates uniqueness.</returns>
    [HttpPost]
    public async Task<IActionResult> IsUserNameUnique(string? userName, string? id = null)
    {
        LoggerUtilities.Info($"[Start] Checking username uniqueness userName={userName} id={id}");
        bool result = await _db.IsUserNameUnique(userName, id);
        LoggerUtilities.Info($"[End] Username uniqueness check result={result}");
        return Json(result);
    }
}