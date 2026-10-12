namespace EpiRead.Services.Interfaces;

/// <summary>
///     Provides methods for working with forum discussions and posts. Includes
///     support for retrieving, filtering, updating, trashing, unbanning, and
///     deleting discussion posts and comments.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IDiscussionService
{
    /// <summary>
    ///     Get a filtered list of discussions matching a search phrase and optional
    ///     book category filter.
    /// </summary>
    /// <param name="search">Partial text to match against discussion title and description.</param>
    /// <param name="filterId">Optional book category id used to filter discussions. Pass null or 0 for no filter.</param>
    /// <returns>List of matching <see cref="Discussion" /> instances.</returns>
    Task<List<Discussion>> GetFilteredDiscussionsAsync(string search, int? filterId);

    /// <summary>
    ///     Retrieve a discussion along with its navigation properties (book, posts and comments).
    /// </summary>
    /// <param name="id">Discussion id.</param>
    /// <returns>The requested discussion with related navigation entities or null if not found.</returns>
    Task<Discussion?> GetDiscussionWithNavigationAsync(int id);

    /// <summary>
    ///     Get active posts for a specific discussion.
    /// </summary>
    /// <param name="discussionId">The id of the discussion.</param>
    /// <returns>List of active discussion posts ordered by last update time.</returns>
    Task<List<DiscussionPost>> GetActivePostsForDiscussionAsync(int discussionId);

    /// <summary>
    ///     Get active posts for a specific discussion with optional search filter.
    /// </summary>
    /// <param name="discussionId">The id of the discussion.</param>
    /// <param name="search">Optional search term to filter posts by title or content.</param>
    /// <returns>List of active discussion posts ordered by last update time.</returns>
    Task<List<DiscussionPost>> GetActivePostsForDiscussionAsync(int discussionId, string? search);

    /// <summary>
    ///     Update a discussion using <see cref="EditDiscussionVm" />.
    /// </summary>
    /// <param name="model">Edited values for the discussion.</param>
    /// <returns>The updated discussion with navigation properties or null if not found.</returns>
    Task<Discussion?> UpdateDiscussionAsync(EditDiscussionVm model);

    /// <summary>
    ///     Get posts for a discussion that are in the trashed state.
    /// </summary>
    /// <param name="discussionId">The id of the discussion.</param>
    /// <returns>List of trashed posts.</returns>
    Task<List<DiscussionPost>> GetTrashedPostsForDiscussionAsync(int discussionId);

    /// <summary>
    ///     Get posts for a discussion that are currently banned.
    /// </summary>
    /// <param name="discussionId">The id of the discussion.</param>
    /// <returns>List of banned posts.</returns>
    Task<List<DiscussionPost>> GetBannedPostsForDiscussionAsync(int discussionId);

    /// <summary>
    ///     Unban a collection of posts and set them to the active state.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to unban.</param>
    /// <returns>List of unbanned post entities.</returns>
    Task<List<DiscussionPost>> UnbanPostsAsync(int[] ids);

    /// <summary>
    ///     Recover (restore) trashed posts so they become active again.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to recover.</param>
    /// <returns>List of recovered post entities.</returns>
    Task<List<DiscussionPost>> RecoverPostsAsync(int[] ids);

    /// <summary>
    ///     Retrieve a post with all details: user, likes, attachments and comments.
    /// </summary>
    /// <param name="id">Discussion post id.</param>
    /// <returns>The requested post with related details or null if not found.</returns>
    Task<DiscussionPost?> GetPostByIdWithDetailsAsync(int id);

    /// <summary>
    ///     Ban a post by id and add an optional reason.
    /// </summary>
    /// <param name="id">Discussion post id.</param>
    /// <param name="reason">Optional reason for banning.</param>
    /// <returns>The banned post with details or null if not found.</returns>
    Task<DiscussionPost?> BanPostAsync(int id, string? reason);

    /// <summary>
    ///     Move a list of posts to the trashed state.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to trash.</param>
    /// <returns>List of trashed post entities.</returns>
    Task<List<DiscussionPost>> TrashPostsAsync(int[] ids);

    /// <summary>
    ///     Permanently delete a list of posts including related comments, likes and attachments.
    ///     Also removes files from external storage providers as necessary.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to delete.</param>
    /// <returns>Number of deleted posts.</returns>
    Task<int> DeletePostsAsync(int[] ids);

    /// <summary>
    ///     Remove a comment from a post; also removes attachments and child replies.
    /// </summary>
    /// <param name="commentId">The id of the comment to remove.</param>
    /// <param name="postId">The id of the parent post the comment belongs to.</param>
    /// <returns>The updated parent post with related navigation properties, or null if not found.</returns>
    Task<DiscussionPost?> RemoveCommentAsync(int commentId, int postId);

    /// <summary>
    ///     Permanently delete all discussion posts and comments created by specified users.
    ///     Called when users are permanently deleted.
    ///     Also removes associated attachments from external storage.
    /// </summary>
    /// <param name="userIds">Array of user IDs whose discussion content should be deleted.</param>
    /// <returns>True if deletion was successful, false otherwise.</returns>
    Task<bool> DeleteUserDiscussionsAsync(int[] userIds);
}