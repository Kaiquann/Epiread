namespace EpiRead.Services.Interfaces;

/// <summary>
///     Defines methods for user-related operations used by services in the application.
///     This includes creating users, password reset operations, finding users, managing
///     profile images, updating user details, and bulk operations for roles and status.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IUserService
{
    /// <summary>
    ///     Create a new user and send an email notification with login information.
    /// </summary>
    /// <param name="createUserVm">View model containing data to create a new user.</param>
    /// <param name="loginUrl">The login URL to include in the email notification.</param>
    /// <returns>
    ///     A tuple where <c>user</c> is the created <see cref="User" /> or <c>null</c> on failure,
    ///     and <c>error</c> contains an error message when the operation fails.
    /// </returns>
    Task<(User? user, string? error)> CreateUserWithEmailNotificationAsync(
        CreateUserVm createUserVm, string loginUrl);

    /// <summary>
    ///     Sends a password reset link to the user identified by <paramref name="userId" />.
    /// </summary>
    /// <param name="userId">The identifier of the user to send the reset link to.</param>
    /// <param name="resetPasswordUrl">The URL used to reset the user's password.</param>
    /// <param name="currentUser">The currently authenticated user performing the action.</param>
    /// <returns>
    ///     An error message when sending fails; otherwise <c>null</c> on success.
    /// </returns>
    Task<string?> SendPasswordResetLinkAsync(
        string userId, string resetPasswordUrl, User currentUser);

    /// <summary>
    ///     Finds and returns a user for the given identifier.
    /// </summary>
    /// <param name="userId">The user identifier.</param>
    /// <returns>The <see cref="User" /> if found; otherwise <c>null</c>.</returns>
    Task<User?> FindUserAsync(string userId);

    /// <summary>
    ///     Saves a user's profile image to the given path and returns the saved file path
    ///     and a link to access it.
    /// </summary>
    /// <param name="file">The uploaded image file.</param>
    /// <param name="imagePath">The directory path where the image will be saved.</param>
    /// <returns>
    ///     A tuple containing the <c>profileImagePath</c>, an accessible <c>profileImageLink</c>,
    ///     and an <c>error</c> message if saving fails.
    /// </returns>
    Task<(string? profileImagePath, string? profileImageLink, string? error)> SaveProfileImageAsync(
        IFormFile file, string imagePath);

    /// <summary>
    ///     Deletes a profile image from storage.
    /// </summary>
    /// <param name="profileImagePath">The path of the profile image to delete.</param>
    /// <returns><c>true</c> when deleted successfully; otherwise <c>false</c>.</returns>
    Task<bool> DeleteProfileImageAsync(string? profileImagePath);

    /// <summary>
    ///     Replaces an existing user's profile image with a new image and updates records.
    /// </summary>
    /// <param name="user">The user whose profile image will be updated.</param>
    /// <param name="newImage">The new image file to use as the profile image.</param>
    /// <param name="imagePath">The path to save the new image.</param>
    /// <returns>
    ///     A tuple where <c>success</c> indicates the operation result, and <c>error</c>
    ///     contains an error message if the update failed.
    /// </returns>
    Task<(bool success, string? error)> UpdateUserProfileImageAsync(
        User user, IFormFile newImage, string imagePath);

    /// <summary>
    ///     Removes a user's profile image and updates the user's profile accordingly.
    /// </summary>
    /// <param name="user">The user whose profile image should be removed.</param>
    /// <returns>
    ///     A tuple where <c>success</c> is <c>true</c> if removal succeeded; otherwise <c>false</c>,
    ///     and <c>error</c> may contain a message when the operation fails.
    /// </returns>
    Task<(bool success, string? error)> RemoveUserProfileImageAsync(User user);

    /// <summary>
    ///     Updates a user's profile with the values from the provided view model.
    ///     Sends email notification to user if any changes are made by admin.
    /// </summary>
    /// <param name="editUserVm">View model containing the updated values.</param>
    /// <param name="currentUser">The user performing the update.</param>
    /// <param name="imagePath">Path to save additional images if they are part of the update.</param>
    /// <param name="profileUrl">The URL to the user's profile page for email notifications.</param>
    /// <returns>
    ///     A tuple where <c>user</c> is the updated user instance, and <c>error</c>
    ///     contains an error message if the update fails.
    /// </returns>
    Task<(User? user, string? error)> UpdateUserAsync(
        EditUserVm editUserVm, User currentUser, string imagePath, string profileUrl);

    /// <summary>
    ///     Validates whether the <paramref name="currentUser" /> has permission to update the given target user.
    /// </summary>
    /// <param name="currentUser">The user attempting the update.</param>
    /// <param name="targetUserId">The id of the user whose data is going to be updated.</param>
    /// <param name="targetUser">The target user's instance (may be <c>null</c> when unknown).</param>
    /// <returns>
    ///     <c>null</c> when the operation is permitted; otherwise an error message describing why the
    ///     update is not allowed.
    /// </returns>
    Task<string?> ValidateUserUpdatePermissionsAsync(
        User currentUser, string targetUserId, User? targetUser);

    /// <summary>
    ///     Bulk updates the status for a list of users and optionally notifies them via email.
    /// </summary>
    /// <param name="ids">Array of user identifiers to update.</param>
    /// <param name="statusType">The status type to set for the provided users.</param>
    /// <param name="currentUser">The user performing the bulk update.</param>
    /// <param name="emailSubject">Subject for notification email.</param>
    /// <param name="emailMessage">Message body for notification email.</param>
    /// <param name="reason">Optional reason for blocking (used only for Blocked status).</param>
    /// <returns>
    ///     A tuple with the <c>count</c> of users updated and an <c>error</c> message if failures occurred.
    /// </returns>
    Task<(int count, string? error)> BulkUpdateUserStatusAsync(
        string[] ids, UserStatusType statusType, User currentUser, string emailSubject, string emailMessage,
        string? reason = null);

    /// <summary>
    ///     Bulk updates a list of users to the specified role.
    /// </summary>
    /// <param name="ids">Array of user identifiers to update their role.</param>
    /// <param name="role">The new role to assign to the selected users.</param>
    /// <param name="currentUser">The user performing the role changes.</param>
    /// <returns>A tuple with the <c>count</c> of updated users and an <c>error</c> message if any.</returns>
    Task<(int count, string? error)> BulkUpdateUserRoleAsync(
        string[] ids, string role, User currentUser);

    /// <summary>
    ///     Retrieves a list of users filtered by provided search criteria and status.
    /// </summary>
    /// <param name="search">Free text search string to match against users.</param>
    /// <param name="filter">The specific filter to apply (e.g., by role or group).</param>
    /// <param name="statusType">User status to filter by.</param>
    /// <param name="currentUserId">Id of the current user (used to limit results or exclude self).</param>
    /// <returns>A list of users that match the criteria.</returns>
    Task<List<User>> GetFilteredUsersAsync(
        string search, string filter, UserStatusType statusType, int currentUserId);

    /// <summary>
    ///     Returns the list of roles available to the given user. This may vary by user privileges, context, or custom rules.
    /// </summary>
    /// <param name="user">The user for which to list available roles.</param>
    /// <returns>A list of <see cref="Role" /> objects the user can be assigned to.</returns>
    Task<List<Role>> GetAvailableRolesAsync(User user);

    /// <summary>
    ///     Sorts and paginates a list of users.
    /// </summary>
    /// <param name="users">The list of users to prepare for pagination.</param>
    /// <param name="sortBy">Property name to sort by (e.g., "Name" or "Email").</param>
    /// <param name="sortDirection">Sort direction: "asc" or "desc".</param>
    /// <param name="pageNumber">The page index for pagination (1-based).</param>
    /// <param name="pageSize">Number of items per page. Defaults to 10.</param>
    /// <returns>A paginated list of users as an <see cref="IPagedList{User}" />.</returns>
    IPagedList<User> SortAndPaginateUsers(
        List<User> users, string? sortBy, string? sortDirection, int pageNumber, int pageSize = 10);
}