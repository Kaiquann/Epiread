using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Areas.Admin.Controllers;

/// <summary>
///     Controller for admin user management: viewing, creating, editing,
///     and performing bulk actions (block, deactivate, reactivate, delete) on users.
///     This controller contains actions used by administrators to manage user accounts.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.RequireAdminOrHigher)]
[Area("Admin")]
public class UserController : Controller
{
    private readonly DB _db;
    private readonly IUserService _userService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UserController" /> class.
    /// </summary>
    /// <param name="db">Database context instance.</param>
    /// <param name="userService">Service for user-related operations.</param>
    public UserController(DB db, IUserService userService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _userService = userService ?? throw new ArgumentNullException(nameof(userService));
    }

    // ==================== READ ====================

    /// <summary>
    ///     List active users with optional search, sort, filter and pagination.
    /// </summary>
    /// <param name="search">Search query for user fields.</param>
    /// <param name="sort">Sort field name.</param>
    /// <param name="dir">Sort direction (asc/desc).</param>
    /// <param name="filter">Filter string.</param>
    /// <param name="page">Page number for pagination.</param>
    public async Task<IActionResult> Index(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        // DB/service op: fetch roles for the current user
        LoggerUtilities.Info("[Start] fetching available roles for userId=" + currentUser.Id);
        LoggerUtilities.Info("[Process] calling user service to get available roles for userId=" + currentUser.Id);

        List<Role> roles = await _userService.GetAvailableRolesAsync(currentUser);
        List<Role> filterRoles = await _db.GetRolesForFilterAsync();
        SelectList roleSelectList = new(roles, "Name", "Name", filter);
        SelectList filterRoleSelectList = new(filterRoles, "Name", "Name", filter);

        ViewBag.Roles = roleSelectList;
        ViewBag.FilterRoles = filterRoleSelectList;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        LoggerUtilities.Info("[Process] fetching filtered users. search=" + search + ", filter=" + filter);
        List<User> searched = await _userService.GetFilteredUsersAsync(
            search, filter, UserStatusType.Active, currentUser.Id);
        LoggerUtilities.Info("[End] fetched " + (searched?.Count ?? 0) + " users");
        if (page < 1)
        {
            LoggerUtilities.Warning("Index corrected invalid page value " + page + " to 1");
            return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });
        }

        IPagedList<User> model = _userService.SortAndPaginateUsers(searched ?? [], sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
        {
            LoggerUtilities.Warning("Index requested page " + page + " greater than pageCount " + model.PageCount +
                                    ", redirecting");
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });
        }

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for page " + page);
            return PartialView("_Index", model);
        }

        LoggerUtilities.Info("[End] completed for page " + page);
        return View(model);
    }

    /// <summary>
    ///     List blocked users with optional search, sort, filter and pagination.
    /// </summary>
    /// <param name="search">Search query for user fields.</param>
    /// <param name="sort">Sort field name.</param>
    /// <param name="dir">Sort direction (asc/desc).</param>
    /// <param name="filter">Filter string.</param>
    /// <param name="page">Page number for pagination.</param>
    public async Task<IActionResult> Blocked(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        LoggerUtilities.Info("[Start] fetching blocked users - search=" + search + ", sort=" + sort + ", dir=" + dir +
                             ", filter=" + filter + ", page=" + page);
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        LoggerUtilities.Info("[Process] calling user service to get available roles for userId=" + currentUser.Id);

        List<Role> roles = await _userService.GetAvailableRolesAsync(currentUser);
        List<Role> filterRoles = await _db.GetRolesForFilterAsync();
        SelectList roleSelectList = new(roles, "Name", "Name", filter);
        SelectList filterRoleSelectList = new(filterRoles, "Name", "Name", filter);

        ViewBag.Roles = roleSelectList;
        ViewBag.FilterRoles = filterRoleSelectList;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        LoggerUtilities.Info("[Process] fetching filtered blocked users. search=" + search + ", filter=" + filter);
        List<User> searched = await _userService.GetFilteredUsersAsync(
            search, filter, UserStatusType.Blocked, currentUser.Id);

        if (page < 1)
        {
            LoggerUtilities.Warning("Blocked corrected invalid page " + page + " to 1");
            return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });
        }

        IPagedList<User> model = _userService.SortAndPaginateUsers(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
        {
            LoggerUtilities.Warning("Blocked requested page " + page + " greater than pageCount " + model.PageCount +
                                    ", redirecting");
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });
        }

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for page " + page);
            return PartialView("_Index", model);
        }

        LoggerUtilities.Info("[End] completed for page " + page);
        return View(model);
    }

    /// <summary>
    ///     List deactivated users with optional search, sort, filter and pagination.
    /// </summary>
    /// <param name="search">Search query for user fields.</param>
    /// <param name="sort">Sort field name.</param>
    /// <param name="dir">Sort direction (asc/desc).</param>
    /// <param name="filter">Filter string.</param>
    /// <param name="page">Page number for pagination.</param>
    public async Task<IActionResult> Deactivated(string? search, string? sort, string? dir, string? filter,
        int page = 1)
    {
        LoggerUtilities.Info("[Start] fetching deactivated users - search=" + search + ", sort=" + sort + ", dir=" +
                             dir + ", filter=" + filter + ", page=" + page);
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        LoggerUtilities.Info("[Process] calling user service to get available roles for userId=" + currentUser.Id);

        List<Role> roles = await _userService.GetAvailableRolesAsync(currentUser);
        List<Role> filterRoles = await _db.GetRolesForFilterAsync();
        SelectList roleSelectList = new(roles, "Name", "Name", filter);
        SelectList filterRoleSelectList = new(filterRoles, "Name", "Name", filter);

        ViewBag.Roles = roleSelectList;
        ViewBag.FilterRoles = filterRoleSelectList;
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;
        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        LoggerUtilities.Info("[Process] fetching filtered deactivated users. search=" + search + ", filter=" + filter);
        List<User> searched = await _userService.GetFilteredUsersAsync(
            search, filter, UserStatusType.Deactivated, currentUser.Id);

        if (page < 1)
        {
            LoggerUtilities.Warning("Deactivated corrected invalid page " + page + " to 1");
            return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });
        }

        IPagedList<User> model = _userService.SortAndPaginateUsers(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
        {
            LoggerUtilities.Warning("Deactivated requested page " + page + " greater than pageCount " +
                                    model.PageCount + ", redirecting");
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });
        }

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for page " + page);
            return PartialView("_Index", model);
        }

        LoggerUtilities.Info("[End] completed for page " + page);
        return View(model);
    }

    /// <summary>
    ///     Display details for a specific user identified by <paramref name="id" />.
    ///     Only accessible to administrators and will return a partial view for AJAX requests.
    /// </summary>
    /// <param name="id">The identifier of the user to display.</param>
    public async Task<IActionResult> Show(string id)
    {
        LoggerUtilities.Info("[Start] fetching current user");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            LoggerUtilities.Warning("[End] invalid model state. id=" + id + ", user=" + currentUser.Id);
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return RedirectToAction("Index");
        }

        if (currentUser.Id.ToString() == id)
        {
            LoggerUtilities.Warning("[End] attempt to view own account blocked. id=" + id + ", user=" + currentUser.Id);
            TempData[SweetAlert.Danger] = "You Cannot View Your Own Account";
            return RedirectToAction("Index");
        }

        // DB/service op: fetch requested user
        LoggerUtilities.Info("[Process] fetching requested user id=" + id);
        User? user = await _userService.FindUserAsync(id);
        if (user == null)
        {
            LoggerUtilities.Warning("[End] user not found. id=" + id + ", requestedBy=" + currentUser.Id);
            TempData[SweetAlert.Danger] = "User Not Found";
            return RedirectToAction("Index");
        }

        LoggerUtilities.Info("[End] completed for id=" + id);

        if (Request.IsAjax()) return PartialView("_Show", user);
        return View(user);
    }

    // ==================== CREATE ====================

    /// <summary>
    ///     Show the create user form populated with available roles.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        LoggerUtilities.Info("[Start] fetching current user");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        LoggerUtilities.Info("[Process] fetching available roles for userId=" + currentUser.Id);
        List<Role> roles = await _userService.GetAvailableRolesAsync(currentUser);
        SelectList roleSelectList = new(roles, "Name", "Name");

        CreateUserVm createUserVm = new()
        {
            Roles = roleSelectList
        };

        LoggerUtilities.Info("[End] prepared view model with " + (roles?.Count ?? 0) + " roles");
        return View(createUserVm);
    }

    /// <summary>
    ///     Store a newly created user. Sends invitation email if required.
    /// </summary>
    /// <param name="createUserVm">View model containing new user details.</param>
    [HttpPost]
    public async Task<IActionResult> Store(CreateUserVm createUserVm)
    {
        LoggerUtilities.Info("[Start] storing new user. present=" + (createUserVm != null));
        LoggerUtilities.Info("[Process] fetching current user");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            LoggerUtilities.Warning("[End] invalid model state for CreateUserVm. present=" + (createUserVm != null));
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer();
        }

        // Security check: ensure currentUser is allowed to create requested role - prevents role injection
        if (!currentUser.CanCreateRole(createUserVm!.Role))
        {
            LoggerUtilities.Warning("[Process] current user not allowed to create role - " + createUserVm.Role);
            TempData[SweetAlert.Danger] = "You are not permitted to create a user with the selected role.";
            return Request.RedirectToReferer();
        }

        string? url = Url.Action("Index", "Login", new { area = "" }, Request.Scheme);

        LoggerUtilities.Info("[Process] calling user service to create user and send notification");
        (User? newUser, string? error) = await _userService.CreateUserWithEmailNotificationAsync(
            createUserVm!, url!);

        if (error != null)
        {
            LoggerUtilities.Warning("[End] create user failed: " + error);
            TempData[SweetAlert.Danger] = error;
            return Request.RedirectToReferer();
        }

        LoggerUtilities.Info("[End] completed - user created with id=" + newUser!.Id);
        TempData[SweetAlert.Success] = "User Created Successfully";
        return RedirectToAction("Show", new { id = newUser!.Id });
    }

    // ==================== UPDATE ====================

    /// <summary>
    ///     Show the edit form for the specified user.
    /// </summary>
    /// <param name="id">The identifier of the user to edit.</param>
    public async Task<IActionResult> Edit(string id)
    {
        LoggerUtilities.Info("[Start] fetching current user for edit id=" + id);
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        if (!ModelState.IsValid)
        {
            LoggerUtilities.Warning("[End] invalid model state. id=" + id + ", user=" + currentUser.Id);
            TempData[SweetAlert.Danger] = Common.InvalidRequestErrorMessage;
            return Request.RedirectToReferer();
        }

        if (currentUser.Id.ToString() == id)
        {
            LoggerUtilities.Warning("[End] user attempted to edit their own account. id=" + id + ", user=" +
                                    currentUser.Id);
            TempData[SweetAlert.Danger] = "You Cannot Edit Your Own Account";
            return RedirectToAction("Index");
        }

        // Find the pass in user id
        LoggerUtilities.Info("[Process] fetching target user id=" + id);
        User? user = await _userService.FindUserAsync(id);
        if (user == null)
        {
            LoggerUtilities.Warning("[End] user not found. id=" + id + ", requestedBy=" + currentUser.Id);
            TempData[SweetAlert.Danger] = "User Not Found";
            return RedirectToAction("Index");
        }

        if (currentUser.Role == nameof(UserRoleType.Admin) &&
            await _db.Users.AnyAsync(u => u.Id == user.Id &&
                                          (u.Role == nameof(UserRoleType.Admin) ||
                                           u.Role == nameof(UserRoleType.SuperAdmin))))
        {
            LoggerUtilities.Warning("[End] admin cannot edit admin account. targetId=" + user.Id + ", requestedBy=" +
                                    currentUser.Id);
            TempData[SweetAlert.Danger] = "You Cannot Edit Admin or SuperAdmin Account";
            return RedirectToAction("Index");
        }

        LoggerUtilities.Info("[Process] fetching available roles for userId=" + currentUser.Id);
        List<Role> roles = await _userService.GetAvailableRolesAsync(currentUser);
        SelectList roleSelectList = new(roles, "Name", "Name", user.Role);

        EditUserVm editUserVm = new()
        {
            Id = user.Id.ToString(),
            FirstName = user.FirstName,
            LastName = user.LastName,
            UserName = user.UserName,
            Email = user.Email,
            DateOfBirth = user.DateOfBirth,
            Language = user.Language,
            Role = user.Role,
            ProfileImagePath = user.ProfileImagePath,
            ProfileImageLink = user.ProfileImageLink,
            Status = user.Status,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };

        ViewBag.Roles = roleSelectList;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for id=" + id);
            return PartialView("_Edit", editUserVm);
        }

        LoggerUtilities.Info("[End] prepared view model for id=" + id);
        return View(editUserVm);
    }

    /// <summary>
    ///     Update a user's details based on the provided view model.
    /// </summary>
    /// <param name="editUserVm">View model containing updated user information.</param>
    [HttpPost]
    public async Task<IActionResult> Update(EditUserVm editUserVm)
    {
        try
        {
            LoggerUtilities.Info("[Start] started for id=" + editUserVm?.Id);
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid)
            {
                LoggerUtilities.Warning("[Process] invalid model state for id=" + editUserVm?.Id);
                throw new Exception(Common.InvalidRequestErrorMessage);
            }

            // Security: ensure current user can assign the given role
            if (!currentUser.CanCreateRole(editUserVm!.Role))
            {
                LoggerUtilities.Warning("[Process] current user not allowed to assign role - " + editUserVm.Role);
                throw new Exception("You are not permitted to assign the selected role.");
            }

            LoggerUtilities.Info("[Process] calling user service to update user id=" + editUserVm?.Id);
            string profileUrl = Url.Action("Profile", "Account", new { area = string.Empty }, Request.Scheme) ??
                                string.Empty;
            (User? updatedUser, string? error) = await _userService.UpdateUserAsync(
                editUserVm!, currentUser, Common.ImagePath, profileUrl);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] encountered error for id=" + editUserVm?.Id + " by user=" +
                                        currentUser.Id + ": " + error);
                throw new Exception(error);
            }

            LoggerUtilities.Info("[End] succeeded for id=" + editUserVm?.Id + " by user=" + currentUser.Id);
            TempData[SweetAlert.Success] = "User updated successfully";
            return Request.RedirectToReferer();
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] Update error for id=" + editUserVm?.Id + ": " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
            return Request.RedirectToReferer();
        }
    }

    /// <summary>
    ///     Bulk update the role for a set of users.
    ///     Accepts an array of user identifiers and the target role name.
    /// </summary>
    /// <param name="ids">Array of user identifiers to update.</param>
    /// <param name="role">The role to assign to the specified users.</param>
    [HttpPost]
    public async Task<IActionResult> UpdateRole(string[] ids, string role)
    {
        try
        {
            LoggerUtilities.Info("[Start] bulk update for role=" + role + ", idsCount=" + (ids?.Length ?? 0));
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            (int count, string? error) =
                await _userService.BulkUpdateUserRoleAsync(ids ?? [], role, currentUser);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] encountered error for role=" + role + " by user=" + currentUser.Id +
                                        ": " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] completed for role=" + role + ". totalUpdated=" + count + " by user=" +
                                     currentUser.Id);
                TempData[SweetAlert.Success] = $"Total {count} user role updated successfully!";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] UpdateRole error for role=" + role + ": " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Remove the profile image for the specified user.
    ///     This action is POST-only and will return to the referrer after completion.
    /// </summary>
    /// <param name="id">The identifier of the user whose profile image will be removed.</param>
    [HttpPost]
    public async Task<IActionResult> RemoveProfileImage(string id)
    {
        try
        {
            LoggerUtilities.Info("[Start] started for id=" + id);
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            if (currentUser.Id.ToString() == id) throw new Exception("You cannot remove your own profile image");

            // Find the pass in user id
            LoggerUtilities.Info("[Process] fetching target user id=" + id);
            User? user = await _userService.FindUserAsync(id);
            if (user == null) throw new Exception("User not found!");

            (bool success, string? error) = await _userService.RemoveUserProfileImageAsync(user);

            if (!success)
            {
                LoggerUtilities.Warning("[End] failed for targetId=" + id + " by user=" + currentUser.Id + ": " +
                                        error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[Process] saving changes to DB for targetId=" + user.Id);
                await _db.SaveChangesAsync();
                LoggerUtilities.Info("[End] succeeded for targetId=" + user.Id + " by user=" + currentUser.Id);
                TempData[SweetAlert.Success] = "User profile image removed successfully";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] RemoveProfileImage error for id=" + id + ": " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Send a password reset link to the specified user via email.
    ///     This is a POST-action used by administrators to trigger password resets.
    /// </summary>
    /// <param name="id">The identifier of the user to send the reset link to.</param>
    [HttpPost]
    public async Task<IActionResult> SendResetPasswordLink(string id)
    {
        try
        {
            LoggerUtilities.Info("[Start] started for id=" + id);
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string? url = Url.Action("", "ResetPassword", new { area = "", token = "PutTokenHere" }, Request.Scheme);

            LoggerUtilities.Info("[Process] sending reset link to targetId=" + id);
            string? error = await _userService.SendPasswordResetLinkAsync(id, url!, currentUser);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] failed for targetId=" + id + " by user=" + currentUser.Id + ": " +
                                        error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] sent successfully for targetId=" + id + " by user=" + currentUser.Id);
                TempData[SweetAlert.Success] = "Reset password link sent successfully";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] SendResetPasswordLink error for id=" + id + ": " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Block one or more users. Supports bulk operation via array of identifiers.
    /// </summary>
    /// <param name="id">The identifier of the user to block.</param>
    /// <param name="reason">Optional reason for blocking the user(s).</param>
    [HttpPost]
    public async Task<IActionResult> Block(string id, string? reason)
    {
        try
        {
            LoggerUtilities.Info("[Start] initiating block. id=" + id);
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string subject = "Account Blocked";
            string message = "Your account has been blocked by admin on date " + DateTime.Now.ToLocalTime() + ".";

            // Include block reason in email if provided
            // Note: No need to HTML encode here as Razor will auto-encode when rendering the email template
            if (!string.IsNullOrEmpty(reason)) message += $" Reason: {reason}";

            LoggerUtilities.Info("[Process] calling user service to update status to Blocked for id=" + id);
            (int count, string? error) = await _userService.BulkUpdateUserStatusAsync(
                new[] { id ?? string.Empty }, UserStatusType.Blocked, currentUser, subject, message, reason);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] bulk update encountered error: " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] block succeeded. totalBlocked=" + count);
                TempData[SweetAlert.Success] = $"Total {count} user blocked successfully";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] Block: error during bulk update: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Unblock one or more users. Supports bulk operation via array of identifiers.
    /// </summary>
    /// <param name="ids">Array of user identifiers to unblock.</param>
    [HttpPost]
    public async Task<IActionResult> Unblock(string[] ids)
    {
        try
        {
            LoggerUtilities.Info("[Start] initiating bulk unblock. idsCount=" + (ids?.Length ?? 0));
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string subject = "Account Unblocked";
            string message = "Your account has been unblocked by admin on date " + DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[Process] calling user service to update status to Active");
            (int count, string? error) = await _userService.BulkUpdateUserStatusAsync(
                ids ?? [], UserStatusType.Active, currentUser, subject, message);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] bulk update encountered error: " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] bulk update succeeded. totalUnblocked=" + count);
                TempData[SweetAlert.Success] = $"Total {count} user unblocked successfully!";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] Unblock: error during bulk update: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Deactivate one or more users. Supports bulk operation via array of identifiers.
    /// </summary>
    /// <param name="ids">Array of user identifiers to deactivate.</param>
    [HttpPost]
    public async Task<IActionResult> Deactivate(string[] ids)
    {
        try
        {
            LoggerUtilities.Info("[Start] initiating bulk deactivate. idsCount=" + (ids?.Length ?? 0));
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string subject = "Account Deactivated By Admin";
            string message = "Your account has been deactivated by admin on date " + DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[Process] calling user service to update status to Deactivated");
            (int count, string? error) = await _userService.BulkUpdateUserStatusAsync(
                ids ?? [], UserStatusType.Deactivated, currentUser, subject, message);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] bulk update encountered error: " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] bulk update succeeded. totalDeactivated=" + count);
                TempData[SweetAlert.Success] = $"Total {count} user deactivated successfully";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] Deactivate: error during bulk update: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Reactivate one or more users. Supports bulk operation via array of identifiers.
    /// </summary>
    /// <param name="ids">Array of user identifiers to reactivate.</param>
    [HttpPost]
    public async Task<IActionResult> Reactivate(string[] ids)
    {
        try
        {
            LoggerUtilities.Info("[Start] initiating bulk reactivate. idsCount=" + (ids?.Length ?? 0));
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string subject = "Account Reactivated";
            string message = "Your account has been reactivated by admin on date " + DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[Process] calling user service to update status to Active");
            (int count, string? error) = await _userService.BulkUpdateUserStatusAsync(
                ids ?? [], UserStatusType.Active, currentUser, subject, message);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] bulk update encountered error: " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] bulk update succeeded. totalReactivated=" + count);
                TempData[SweetAlert.Success] = $"Total {count} user reactivated successfully!";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] Reactivate: error during bulk update: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    // ==================== DELETE ====================

    /// <summary>
    ///     Permanently delete one or more users. This is irreversible.
    /// </summary>
    /// <param name="ids">Array of user identifiers to permanently delete.</param>
    [HttpPost]
    public async Task<IActionResult> PermanentlyDelete(string[] ids)
    {
        try
        {
            LoggerUtilities.Info("[Start] initiating bulk permanent delete. idsCount=" + (ids?.Length ?? 0));
            LoggerUtilities.Info("[Process] fetching current user");
            User? currentUser = await User.GetCurrentUser(_db);
            if (currentUser == null) throw new Exception(Common.PleaseLoginToContinueErrorMessage);

            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            string subject = "Account Permanently Deleted";
            string message = "Your account has been permanently deleted by admin on date " + DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[Process] calling user service to update status to PermanentlyDeleted");
            (int count, string? error) = await _userService.BulkUpdateUserStatusAsync(
                ids ?? [], UserStatusType.PermanentlyDeleted, currentUser, subject, message);

            if (error != null)
            {
                LoggerUtilities.Warning("[End] bulk update encountered error: " + error);
                TempData[SweetAlert.Danger] = error;
            }
            else
            {
                LoggerUtilities.Info("[End] bulk update succeeded. totalDeleted=" + count);
                TempData[SweetAlert.Success] = $"Total {count} user permanently deleted successfully!";
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Error] PermanentlyDelete: error during bulk update: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer(this, "Index", "Admin");
    }
}