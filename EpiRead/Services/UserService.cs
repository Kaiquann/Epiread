using Microsoft.EntityFrameworkCore;

namespace EpiRead.Services;

/// <summary>
///     Implementation of <see cref="IUserService" />. Contains user management business logic
///     such as creating users with notification, password resets, profile image handling,
///     updating user details, and bulk role/status operations.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class UserService : IUserService
{
    private readonly DB _db;
    private readonly IDiscussionService _discussionService;
    private readonly IEmailService _emailService;
    private readonly IImageDropboxHelper _imageDropboxHelper;
    private readonly IImageHelper _imageHelper;
    private readonly IMailerHelper _mailerHelper;
    private readonly IResetPasswordService _resetPasswordService;

    public UserService(DB db, IEmailService emailService, IImageDropboxHelper imageDropboxHelper,
        IImageHelper imageHelper, IMailerHelper mailerHelper, IResetPasswordService resetPasswordService,
        IDiscussionService discussionService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _imageDropboxHelper = imageDropboxHelper ?? throw new ArgumentNullException(nameof(imageDropboxHelper));
        _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
        _mailerHelper = mailerHelper ?? throw new ArgumentNullException(nameof(mailerHelper));
        _resetPasswordService = resetPasswordService ?? throw new ArgumentNullException(nameof(resetPasswordService));
        _discussionService = discussionService ?? throw new ArgumentNullException(nameof(discussionService));
    }

    /// <inheritdoc />
    public async Task<(User? user, string? error)> CreateUserWithEmailNotificationAsync(
        CreateUserVm createUserVm, string loginUrl)
    {
        LoggerUtilities.Info("[Start] Email=" + MaskEmail(createUserVm.Email) + ", Role=" + createUserVm.Role);
        try
        {
            // Validate email
            bool isEmailExists = await _db.IsEmailExists(createUserVm.Email);
            if (isEmailExists)
            {
                LoggerUtilities.Warning("[Process] Email already exists - " + MaskEmail(createUserVm.Email));
                return (null, Common.EmailExistsErrorMessage);
            }

            // Validate role
            bool isValidRole = await _db.IsValidRole(createUserVm.Role);
            if (!isValidRole)
            {
                LoggerUtilities.Warning("[Process] Invalid role - " + createUserVm.Role);
                return (null, "Invalid Role");
            }

            // Generate random password
            string password = GetRandomString(16);

            // Generate random username with A-Z a-z 0-9
            string username = "user_" + GetRandomString(8);

            // Create user
            User newUser = new()
            {
                FirstName = createUserVm.FirstName,
                LastName = createUserVm.LastName,
                UserName = username,
                Email = createUserVm.Email,
                Password = HashPassword(password),
                Role = createUserVm.Role
            };

            await _db.Users.AddAsync(newUser);
            await _db.SaveChangesAsync();
            LoggerUtilities.Info("[Process] Created user Id=" + newUser.Id + ", Email=" + MaskEmail(newUser.Email));

            // Send email notification
            AccountCreatedByAdminVm accountCreatedByAdminVm = new()
            {
                FirstName = newUser.FirstName,
                LastName = newUser.LastName,
                Email = newUser.Email,
                Password = password,
                Url = loginUrl
            };
            string body = await _emailService.AccountCreatedByAdmin(accountCreatedByAdminVm);

            MailerConfig mail = new()
            {
                To = [newUser.Email],
                Subject = EmailSubject.AccountCreatedSubject,
                Body = body
            };
            await _mailerHelper.SendEmail(mail);
            LoggerUtilities.Info("[End] Email sent to " + MaskEmail(newUser.Email));
            return (newUser, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to create user for Email " + MaskEmail(createUserVm.Email) + ": " +
                                  ex.Message);
            return (null, $"Failed to create user: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<string?> SendPasswordResetLinkAsync(
        string userId, string resetPasswordUrl, User currentUser)
    {
        LoggerUtilities.Info("[Start] TargetUserId=" + userId + " by CurrentUserId=" + currentUser.Id);
        try
        {
            if (currentUser.Id.ToString() == userId)
            {
                LoggerUtilities.Warning("[Process] Attempt to reset own password by UserId=" + currentUser.Id);
                return "You cannot reset your own password!";
            }

            User? user = await FindUserAsync(userId);
            if (user == null)
            {
                LoggerUtilities.Warning("[Process] User not found - " + userId);
                return "User not found!";
            }

            // Authorization: Only SuperAdmin may send reset links for Admin and SuperAdmin.
            // Admins may send reset links only for Author and Member.
            if (currentUser.Role == nameof(UserRoleType.Admin))
            {
                if (user.Role == nameof(UserRoleType.Admin) || user.Role == nameof(UserRoleType.SuperAdmin))
                {
                    LoggerUtilities.Warning(
                        $"[Process] Admin attempted to send reset link to restricted role - TargetRole={user.Role} TargetId={user.Id}");
                    return "You are not permitted to send password reset link to admin or superadmin accounts.";
                }
            }
            else if (currentUser.Role != nameof(UserRoleType.SuperAdmin))
            {
                LoggerUtilities.Warning(
                    $"[Process] User without sufficient role attempted to send reset link - UserId={currentUser.Id}");
                return "You are not permitted to send password reset links.";
            }

            string token = Guid.NewGuid().ToString();

            ResetPassword? resetPassword = await _resetPasswordService.GetResetPasswordByUserIdAsync(userId);
            if (resetPassword != null)
            {
                resetPassword.Token = token;
                LoggerUtilities.Info("[Process] Updated reset token for UserId=" + user.Id);
            }
            else
            {
                resetPassword = new ResetPassword
                {
                    UserId = user.Id,
                    Token = token
                };
                await _db.ResetPasswords.AddAsync(resetPassword);
                LoggerUtilities.Info("[Process] Created reset token for UserId=" + user.Id);
            }

            await _db.SaveChangesAsync();

            // Send email
            // Ensure token is URL encoded before injecting into the URL placeholder
            // to avoid introducing invalid characters (%, /, +, etc.) in the link.
            string encodedToken = Uri.EscapeDataString(token);
            ResetPasswordVm resetPasswordVm = new()
            {
                Url = resetPasswordUrl.Replace("PutTokenHere", encodedToken)
            };
            string body = await _emailService.ResetPassword(resetPasswordVm);

            MailerConfig mail = new()
            {
                To = [user.Email],
                Subject = EmailSubject.ResetPasswordSubject,
                Body = body
            };
            await _mailerHelper.SendEmail(mail);
            LoggerUtilities.Info("[End] Reset link sent to Email " + MaskEmail(user.Email));
            return null;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to send reset password link for TargetUserId " + userId + ": " +
                                  ex.Message);
            return $"Failed to send reset password link: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public async Task<User?> FindUserAsync(string userId)
    {
        LoggerUtilities.Info("[Start] UserId=" + userId);
        User? user = await _db.Users.SingleOrDefaultAsync(u =>
            u.Id.ToString() == userId &&
            u.Status != nameof(UserStatusType.PermanentlyDeleted) &&
            u.Role != nameof(UserRoleType.SuperAdmin));
        if (user == null)
            LoggerUtilities.Info("[Process] no user found for UserId=" + userId);
        else
            LoggerUtilities.Info("[End] found UserId=" + user.Id + ", Email=" + MaskEmail(user.Email));

        return user;
    }

    /// <inheritdoc />
    public IPagedList<User> SortAndPaginateUsers(
        List<User> users, string? sortBy, string? sortDirection, int pageNumber, int pageSize = 10)
    {
        try
        {
            // Validate page number
            if (pageNumber < 1)
                pageNumber = 1;

            // Sorting
            Func<User, object> sortFunction = sortBy switch
            {
                "FirstName" => u => u.FirstName,
                "LastName" => u => u.LastName,
                "Email" => u => u.Email,
                "Role" => u => u.Role,
                "CreatedAt" => u => u.CreatedAt,
                _ => u => u.Id
            };

            IOrderedEnumerable<User> sorted = sortDirection == "des"
                ? users.AsEnumerable().OrderByDescending(sortFunction)
                : users.AsEnumerable().OrderBy(sortFunction);

            // Paging
            IPagedList<User> result = sorted.ToPagedList(pageNumber, pageSize);
            return result;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to sort and paginate users: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(string? profileImagePath, string? profileImageLink, string? error)> SaveProfileImageAsync(
        IFormFile file, string imagePath)
    {
        LoggerUtilities.Info("[Start] imagePath=" + imagePath + ", fileName=" + file.FileName);
        try
        {
            // Validate the image
            string validationError = _imageHelper.ValidatePhoto(file);
            if (!string.IsNullOrEmpty(validationError))
                // Validation is not a DB/network operation; return without logging per guideline
                return (null, null, validationError);

            // Generate unique filename
            string fileName = FileUtilities.GenerateFileName(file);
            string dropboxPath = $"/{imagePath}{fileName}";

            // Upload to Dropbox
            LoggerUtilities.Info("[Process] Uploading to " + dropboxPath);
            await _imageDropboxHelper.UploadPhotoToDropboxAsync(file, dropboxPath);

            // Get preview link
            string previewLink = await _imageDropboxHelper.GetDropboxImagePreviewLinkAsync(dropboxPath);

            LoggerUtilities.Info("[End] uploaded to " + dropboxPath);
            return (dropboxPath, previewLink, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to upload image: " + ex.Message);
            return (null, null, $"Failed to upload image: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileImageAsync(string? profileImagePath)
    {
        if (string.IsNullOrEmpty(profileImagePath))
            return true;
        try
        {
            LoggerUtilities.Info("[Start] deleting " + profileImagePath);
            await _imageDropboxHelper.DeletePhotoFromDropboxAsync(profileImagePath);
            LoggerUtilities.Info("[End] deleted " + profileImagePath);
            return true;
        }
        catch (Exception)
        {
            // Ignore deletion errors - file might not exist
            LoggerUtilities.Warning("[Process] failed to delete " + profileImagePath);
            return false;
        }
    }

    /// <inheritdoc />
    public async Task<(bool success, string? error)> UpdateUserProfileImageAsync(
        User user, IFormFile newImage, string imagePath)
    {
        LoggerUtilities.Info("[Start] UserId=" + user.Id);
        try
        {
            // Preserve old image path so we can delete it after successfully uploading a new one.
            string? oldProfileImagePath = user.ProfileImagePath;

            // Upload new image
            (string? newPath, string? newLink, string? error) =
                await SaveProfileImageAsync(newImage, imagePath);

            if (!string.IsNullOrEmpty(error))
            {
                LoggerUtilities.Warning("[Process] failed to save new image for UserId=" + user.Id + " - " + error);
                return (false, error);
            }

            // Update user entity
            user.ProfileImagePath = newPath;
            user.ProfileImageLink = newLink;
            user.UpdatedAt = DateTime.Now.ToLocalTime();

            // Delete old image if exists
            if (!string.IsNullOrEmpty(oldProfileImagePath))
                // Deletion is handled and logged inside DeleteProfileImageAsync
                await DeleteProfileImageAsync(oldProfileImagePath);

            LoggerUtilities.Info("[End] UserId=" + user.Id + ", Path=" + newPath);
            return (true, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to update profile image for UserId=" + user.Id + ": " + ex.Message);
            return (false, $"Failed to update profile image: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(bool success, string? error)> RemoveUserProfileImageAsync(User user)
    {
        LoggerUtilities.Info("[Start] UserId=" + user.Id);
        try
        {
            if (string.IsNullOrEmpty(user.ProfileImagePath))
            {
                LoggerUtilities.Warning("[Process] No image to remove for UserId=" + user.Id);
                return (false, "No image to remove!");
            }

            // Delete from Dropbox
            await DeleteProfileImageAsync(user.ProfileImagePath);

            // Clear database fields
            user.ProfileImagePath = null;
            user.ProfileImageLink = null;
            user.UpdatedAt = DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[End] UserId=" + user.Id);
            return (true, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to remove profile image for UserId=" + user.Id + ": " + ex.Message);
            return (false, $"Failed to remove profile image: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(User? user, string? error)> UpdateUserAsync(
        EditUserVm editUserVm, User currentUser, string imagePath, string profileUrl)
    {
        LoggerUtilities.Info("[Start] TargetUserId=" + editUserVm.Id + " by CurrentUserId=" + currentUser.Id);
        try
        {
            // Find user
            User? user = await FindUserAsync(editUserVm.Id);
            if (user == null)
                // FindUserAsync already logs DB lookup result; propagate not found
                return (null, "User not found!");

            // Validate permissions
            string? permissionError = await ValidateUserUpdatePermissionsAsync(currentUser, editUserVm.Id, user);
            if (permissionError != null)
                // Permission validation logs are done in ValidateUserUpdatePermissionsAsync
                return (null, permissionError);

            // Validate role
            bool isValidRole = await _db.IsValidRole(editUserVm.Role);
            if (!isValidRole)
            {
                LoggerUtilities.Warning("[Process] Invalid role - " + editUserVm.Role);
                return (null, "Invalid role!");
            }

            // Security: Ensure current user is allowed to assign this role to the target user
            if (!currentUser.CanCreateRole(editUserVm.Role))
            {
                LoggerUtilities.Warning("[Process] current user not allowed to assign role - " + editUserVm.Role);
                return (null, "You are not permitted to assign the selected role.");
            }

            // Validate email if changed
            if (user.Email != editUserVm.Email)
            {
                bool isEmailExists = await _db.IsEmailExists(editUserVm.Email);
                if (isEmailExists)
                {
                    LoggerUtilities.Warning("[Process] Email already exists - " + MaskEmail(editUserVm.Email));
                    return (null, Common.EmailExistsErrorMessage);
                }
            }

            // Validate username if changed
            if (user.UserName != editUserVm.UserName)
            {
                bool isUserNameExists = await _db.IsUserNameExists(editUserVm.UserName);
                if (isUserNameExists)
                {
                    LoggerUtilities.Warning("[Process] Username already exists - " + editUserVm.UserName);
                    return (null, "Username already exists. Please choose another username.");
                }
            }

            // Validate language (if provided)
            if (!LanguageHelper.IsValidLanguage(editUserVm.Language))
            {
                LoggerUtilities.Warning("[Process] Invalid language - " + editUserVm.Language);
                return (null, "Invalid language!");
            }

            // Handle profile image update
            if (editUserVm.ProfileImage != null)
            {
                (bool success, string? imageError) =
                    await UpdateUserProfileImageAsync(user, editUserVm.ProfileImage, imagePath);

                if (!success)
                {
                    LoggerUtilities.Warning("[Process] failed to update profile image for UserId=" + user.Id + " - " +
                                            imageError);
                    return (null, imageError);
                }
            }

            // Track changes for notification email
            List<AccountChangeVm> changes = [];
            string oldEmail = user.Email;
            string oldUserName = user.UserName;
            bool isEmailChanged = false;

            // Track First Name change
            if (user.FirstName != editUserVm.FirstName)
                changes.Add(new AccountChangeVm
                {
                    FieldName = "First Name",
                    OldValue = user.FirstName,
                    NewValue = editUserVm.FirstName
                });

            // Track Last Name change
            if (user.LastName != editUserVm.LastName)
                changes.Add(new AccountChangeVm
                {
                    FieldName = "Last Name",
                    OldValue = user.LastName,
                    NewValue = editUserVm.LastName
                });

            // Track Username change
            if (user.UserName != editUserVm.UserName)
                changes.Add(new AccountChangeVm
                {
                    FieldName = "Username",
                    OldValue = user.UserName,
                    NewValue = editUserVm.UserName
                });

            // Track Email change
            if (user.Email != editUserVm.Email)
            {
                isEmailChanged = true;
                changes.Add(new AccountChangeVm
                {
                    FieldName = "Email",
                    OldValue = user.Email,
                    NewValue = editUserVm.Email
                });
            }

            // Track Language change
            string oldLanguage = user.Language;
            string newLanguage = string.IsNullOrEmpty(editUserVm.Language) ? "(Not set)" : editUserVm.Language;
            if (oldLanguage != newLanguage)
                changes.Add(new AccountChangeVm
                {
                    FieldName = "Language Preference",
                    OldValue = oldLanguage == "(Not set)" ? "" : LanguageHelper.GetLanguageName(oldLanguage),
                    NewValue = newLanguage == "(Not set)" ? "" : LanguageHelper.GetLanguageName(newLanguage)
                });

            // Track Role change
            if (user.Role != editUserVm.Role)
                changes.Add(new AccountChangeVm
                {
                    FieldName = "Role",
                    OldValue = user.Role,
                    NewValue = editUserVm.Role
                });

            // Update user properties
            user.FirstName = editUserVm.FirstName;
            user.LastName = editUserVm.LastName;
            user.UserName = editUserVm.UserName;
            user.Email = editUserVm.Email;
            user.Language = editUserVm.Language;
            user.Role = editUserVm.Role;
            user.UpdatedAt = DateTime.Now.ToLocalTime();

            LoggerUtilities.Info("[Process] Saving changes for UserId=" + user.Id);
            await _db.SaveChangesAsync();

            // Send notification email if there are changes
            if (changes.Any())
            {
                LoggerUtilities.Info("[Process] Sending account update notification. ChangesCount=" + changes.Count);
                try
                {
                    AccountUpdatedByAdminVm accountUpdatedVm = new()
                    {
                        UserName = oldUserName,
                        AdminName = currentUser.FirstName + " " + currentUser.LastName,
                        Changes = changes,
                        UpdatedAt = user.UpdatedAt,
                        Url = profileUrl,
                        IsEmailChanged = isEmailChanged,
                        OldEmail = isEmailChanged ? oldEmail : null,
                        NewEmail = isEmailChanged ? editUserVm.Email : null
                    };

                    string emailBody = await _emailService.AccountUpdatedByAdmin(accountUpdatedVm);
                    string subject = "Your Account Has Been Updated";

                    // Send to old email if email was changed
                    if (isEmailChanged)
                    {
                        LoggerUtilities.Info("[Process] Sending notification to old email: " + MaskEmail(oldEmail));
                        MailerConfig mailOldEmail = new()
                        {
                            To = [oldEmail],
                            Subject = subject,
                            Body = emailBody
                        };
                        await _mailerHelper.SendEmail(mailOldEmail);
                    }

                    // Always send to current/new email
                    LoggerUtilities.Info("[Process] Sending notification to current email: " + MaskEmail(user.Email));
                    MailerConfig mailNewEmail = new()
                    {
                        To = [user.Email],
                        Subject = subject,
                        Body = emailBody
                    };
                    await _mailerHelper.SendEmail(mailNewEmail);

                    LoggerUtilities.Info("[Process] Account update notifications sent successfully");
                }
                catch (Exception emailEx)
                {
                    // Log but don't fail the update if email fails
                    LoggerUtilities.Error("[Process] Failed to send update notification email: " + emailEx.Message);
                }
            }

            LoggerUtilities.Info("[End] updated UserId=" + user.Id);
            return (user, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to update user " + editUserVm.Id + ": " + ex.Message);
            return (null, $"Failed to update user: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<string?> ValidateUserUpdatePermissionsAsync(
        User currentUser, string targetUserId, User? targetUser)
    {
        LoggerUtilities.Info("[Start] CurrentUserId=" + currentUser.Id + ", TargetUserId=" + targetUserId);
        if (currentUser.Id.ToString() == targetUserId)
        {
            LoggerUtilities.Warning("[Process] attempted self-update by UserId=" + currentUser.Id);
            return "You cannot update your own account!";
        }

        if (targetUser == null)
        {
            LoggerUtilities.Warning("[Process] target user not found - " + targetUserId);
            return "User not found!";
        }

        if (currentUser.Role == nameof(UserRoleType.Admin) &&
            await _db.Users.AnyAsync(u => u.Id == targetUser.Id &&
                                          (u.Role == nameof(UserRoleType.Admin) ||
                                           u.Role == nameof(UserRoleType.SuperAdmin))))
        {
            LoggerUtilities.Warning("[Process] admin cannot update other admin or superadmin UserId=" + targetUser.Id);
            return "You cannot update admin or superadmin account!";
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<(int count, string? error)> BulkUpdateUserStatusAsync(
        string[] ids, UserStatusType statusType, User currentUser, string emailSubject, string emailMessage,
        string? reason = null)
    {
        LoggerUtilities.Info("[Start] status=" + statusType + ", ids=" + ids.Length);
        try
        {
            if (ids.Length == 0)
            {
                LoggerUtilities.Warning("[Process] empty ids array");
                return (0, Common.InvalidRequestErrorMessage);
            }

            if (ids.Contains(currentUser.Id.ToString()))
            {
                LoggerUtilities.Warning("[Process] attempted to change own account status by UserId=" + currentUser.Id);
                return (0, $"You cannot {statusType.ToString().ToLower()} your own account!");
            }

            if (currentUser.Role == nameof(UserRoleType.Admin) &&
                await _db.Users.AnyAsync(u => ids.Contains(u.Id.ToString()) &&
                                              (u.Role == nameof(UserRoleType.Admin) ||
                                               u.Role == nameof(UserRoleType.SuperAdmin))))
            {
                LoggerUtilities.Warning("[Process] admin attempted to change other admin or superadmin status");
                return (0, $"You cannot {statusType.ToString().ToLower()} admin or superadmin account!");
            }

            // Update user status
            LoggerUtilities.Info("[Process] updating status for " + ids.Length + " users");
            List<string> userImageList = [];

            // Collect original email addresses before modification (for permanent deletion notification)
            string[] originalEmails = statusType == UserStatusType.PermanentlyDeleted
                ? await _db.Users
                    .Where(u => ids.Contains(u.Id.ToString()))
                    .Select(u => u.Email)
                    .ToArrayAsync()
                : [];

            // Delete discussion posts and comments for permanently deleted users
            if (statusType == UserStatusType.PermanentlyDeleted)
            {
                List<int> userIdsList = new();
                foreach (string id in ids)
                    if (int.TryParse(id, out int userId))
                        userIdsList.Add(userId);

                if (userIdsList.Count > 0)
                {
                    LoggerUtilities.Info("[Process] Deleting discussion posts and comments for " + userIdsList.Count +
                                         " users");
                    bool discussionDeleted = await _discussionService.DeleteUserDiscussionsAsync(userIdsList.ToArray());
                    if (!discussionDeleted)
                        LoggerUtilities.Warning("[Process] Failed to delete some discussion content for users");
                }
            }

            await _db.Users
                .Where(u => ids.Contains(u.Id.ToString()))
                .ForEachAsync(u =>
                {
                    u.Status = statusType.ToString();
                    u.UpdatedAt = DateTime.Now.ToLocalTime();

                    // Set BlockReason when blocking, clear it for other status changes
                    u.BlockReason = statusType == UserStatusType.Blocked ? reason : null;

                    // Special handling for permanent deletion
                    if (statusType == UserStatusType.PermanentlyDeleted)
                    {
                        u.Email = "PD:" + u.Email + "_" + DateTime.Now.ToLocalTime();
                        u.UserName = "PD:" + u.UserName + "_" + DateTime.Now.ToLocalTime();
                        u.DeletedOn = DateTime.Now.ToLocalTime();
                        if (!string.IsNullOrEmpty(u.ProfileImagePath))
                        {
                            userImageList.Add(u.ProfileImagePath);
                            u.ProfileImagePath = null;
                            u.ProfileImageLink = null;
                        }
                    }
                });

            await _db.SaveChangesAsync();

            // Delete images for permanently deleted users
            foreach (string imagePath in userImageList)
                await DeleteProfileImageAsync(imagePath);

            // Send email notification
            // For permanent deletion, use original emails collected before modification
            await SendBulkEmailAsync(ids, emailSubject, emailMessage,
                statusType == UserStatusType.PermanentlyDeleted ? originalEmails : null);

            LoggerUtilities.Info("[End] updated " + ids.Length + " users");
            return (ids.Length, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to update user status: " + ex.Message);
            return (0, $"Failed to update user status: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<(int count, string? error)> BulkUpdateUserRoleAsync(
        string[] ids, string role, User currentUser)
    {
        LoggerUtilities.Info("[Start] role=" + role + ", ids=" + ids.Length);
        try
        {
            if (ids.Length == 0)
            {
                LoggerUtilities.Warning("[Process] empty ids array");
                return (0, Common.InvalidRequestErrorMessage);
            }

            if (!await _db.IsValidRole(role))
            {
                LoggerUtilities.Warning("[Process] invalid role - " + role);
                return (0, "Invalid role!");
            }

            // Security check: Admins cannot assign Admin or SuperAdmin roles
            if (currentUser.Role == nameof(UserRoleType.Admin) &&
                (role == nameof(UserRoleType.Admin) || role == nameof(UserRoleType.SuperAdmin)))
            {
                LoggerUtilities.Warning("[Process] admin attempted to assign elevated role - " + role);
                return (0, "You are not permitted to assign that role!");
            }

            if (ids.Contains(currentUser.Id.ToString()))
            {
                LoggerUtilities.Warning("[Process] attempted to change own roles by UserId=" + currentUser.Id);
                return (0, "You cannot update your account roles!");
            }

            if (currentUser.Role == nameof(UserRoleType.Admin) &&
                await _db.Users.AnyAsync(u => ids.Contains(u.Id.ToString()) &&
                                              (u.Role == nameof(UserRoleType.Admin) ||
                                               u.Role == nameof(UserRoleType.SuperAdmin))))
            {
                LoggerUtilities.Warning("[Process] admin attempted to change other admin or superadmin roles");
                return (0, "You cannot update admin or superadmin account roles!");
            }

            LoggerUtilities.Info("[Process] updating roles for " + ids.Length + " users to " + role);
            await _db.Users
                .Where(u => ids.Contains(u.Id.ToString()))
                .ForEachAsync(u =>
                {
                    u.Role = role;
                    u.UpdatedAt = DateTime.Now.ToLocalTime();
                });

            await _db.SaveChangesAsync();

            LoggerUtilities.Info("[End] updated roles for " + ids.Length + " users to " + role);
            return (ids.Length, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to update user roles: " + ex.Message);
            return (0, $"Failed to update user roles: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<List<User>> GetFilteredUsersAsync(
        string search, string filter, UserStatusType statusType, int currentUserId)
    {
        LoggerUtilities.Info("[Start] search='" + search + "', filter='" + filter + "', status=" + statusType +
                             ", currentUserId=" + currentUserId);
        try
        {
            search = search.Trim();
            filter = filter.Trim();

            List<User> list = await _db.Users
                .Where(u => (u.FirstName.Contains(search) ||
                             u.LastName.Contains(search) ||
                             u.Email.Contains(search)) &&
                            (filter == "" || u.Role == filter) &&
                            // Exclude SuperAdmin and Current Logged-In User
                            u.Status == statusType.ToString() &&
                            u.Role != nameof(UserRoleType.SuperAdmin) &&
                            u.Id != currentUserId)
                .ToListAsync();

            LoggerUtilities.Info("[End] returned " + list.Count + " users");
            return list;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to filter users: " + ex.Message);
            throw new Exception($"Failed to filter users: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<List<Role>> GetAvailableRolesAsync(User user)
    {
        LoggerUtilities.Info("[Start] for UserId=" + user.Id);
        try
        {
            List<Role> roles = await _db.GetRolesAsync(user);
            LoggerUtilities.Info("[End] returned " + roles.Count + " roles for UserId=" + user.Id);
            return roles;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[Process] Failed to get available roles for UserId=" + user.Id + ": " + ex.Message);
            throw new Exception("Failed to get available roles: " + ex.Message);
        }
    }

    /// <summary>
    ///     Sends bulk email notifications to users.
    /// </summary>
    /// <param name="ids">User IDs to send emails to.</param>
    /// <param name="subject">Email subject.</param>
    /// <param name="message">Email message.</param>
    /// <param name="preFetchedEmails">Optional pre-fetched email list (used when emails are modified before sending).</param>
    private async Task SendBulkEmailAsync(string[] ids, string subject, string message,
        string[]? preFetchedEmails = null)
    {
        LoggerUtilities.Info("[Start] subject=" + subject + ", requestedRecipients=" + ids.Length);
        string[] emailList = preFetchedEmails ?? await _db.Users
            .Where(u => ids.Contains(u.Id.ToString()))
            .Select(u => u.Email)
            .ToArrayAsync();

        LoggerUtilities.Info("[Process] resolvedRecipients=" + emailList.Length);
        SimpleTitleMessageVm simpleTitleMessageVm = new()
        {
            Title = subject,
            Message = message
        };
        string body = await _emailService.SimpleTitleMessage(simpleTitleMessageVm);

        MailerConfig mail = new()
        {
            Bcc = emailList,
            Subject = subject,
            Body = body
        };
        await _mailerHelper.SendEmail(mail);
        LoggerUtilities.Info("[End] sent to " + emailList.Length + " addresses (bcc)");
    }
}