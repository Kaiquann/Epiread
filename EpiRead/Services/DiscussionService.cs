using CommonUtilities.Utilities.Database;
using EpiRead.Models.StoredProcedure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EpiRead.Services;

/// <summary>
///     Provides an implementation of <see cref="IDiscussionService" /> for
///     managing forum discussions and their posts. Supports retrieving,
///     updating and removing posts and comments, handling trashed/banned states,
///     and coordinating external file deletion with a Dropbox service.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DiscussionService : IDiscussionService
{
    private readonly IConfiguration _configuration;
    private readonly DB _db;
    private readonly IDropboxService _dropboxService;

    /// <summary>
    ///     Initializes a new instance of <see cref="DiscussionService" />.
    /// </summary>
    /// <param name="db">Database context for accessing discussion data.</param>
    /// <param name="dropboxService">Service used to delete external attachments on Dropbox.</param>
    /// <param name="configuration">Configuration for accessing connection strings.</param>
    public DiscussionService(DB db, IDropboxService dropboxService, IConfiguration configuration)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _dropboxService = dropboxService ?? throw new ArgumentNullException(nameof(dropboxService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc />
    public async Task<List<Discussion>> GetFilteredDiscussionsAsync(string search, int? filterId)
    {
        LoggerUtilities.Info($"[Start] GetFilteredDiscussions search='{search}' filterId={filterId ?? 0}");
        bool hasCategoryFilter = filterId.HasValue && filterId.Value > 0;

        List<Discussion> discussions = await _db.Discussions
            .Where(bc => (bc.Title.Contains(search) || bc.Description!.Contains(search)) &&
                         (!hasCategoryFilter || (bc.Book != null && bc.Book.BookCategoryMaps != null &&
                                                 bc.Book.BookCategoryMaps.Any(bcm => bcm.BookCategoryId == filterId))))
            .Include(bc => bc.Book)
            .Include(bc =>
                bc.DiscussionPosts!.Where(p =>
                    p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted)))!
            .ThenInclude(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ToListAsync();

        LoggerUtilities.Info($"[End] GetFilteredDiscussions returnedCount={discussions.Count}");

        return discussions;
    }

    /// <inheritdoc />
    public async Task<Discussion?> GetDiscussionWithNavigationAsync(int id)
    {
        LoggerUtilities.Info($"[Start] GetDiscussionWithNavigation id={id}");
        return await _db.Discussions
            .Include(d => d.Book)
            .Include(d =>
                d.DiscussionPosts!.Where(p =>
                    p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(p => p.User)
            .Include(d =>
                d.DiscussionPosts!.Where(p =>
                    p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.User)
            .Include(d =>
                d.DiscussionPosts!.Where(p =>
                    p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.DiscussionAttachment)
            .FirstOrDefaultAsync(d => d.Id == id);
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> GetActivePostsForDiscussionAsync(int discussionId)
    {
        LoggerUtilities.Info($"[Start] GetActivePostsForDiscussion discussionId={discussionId}");
        return await _db.DiscussionPosts
            .Where(p => p.DiscussionId == discussionId &&
                        p.Status == nameof(DiscussionPostStatusType.Active) &&
                        p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted))
            .Include(p => p.User)
            .Include(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.User)
            .Include(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.DiscussionAttachment)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> GetActivePostsForDiscussionAsync(int discussionId, string? search)
    {
        LoggerUtilities.Info($"[Start] GetActivePostsForDiscussion discussionId={discussionId}, search='{search}'");

        var query = _db.DiscussionPosts
            .Where(p => p.DiscussionId == discussionId &&
                        p.Status == nameof(DiscussionPostStatusType.Active) &&
                        p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted));

        // EF Core's Contains method uses parameterized queries, preventing SQL injection
        // SQL Server default collation performs case-insensitive searches
        // Note: For large datasets, consider adding database indexes on Title and Content columns
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p => p.Title.Contains(search) || p.Content.Contains(search));

        return await query
            .Include(p => p.User)
            .Include(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .Include(p => p.DiscussionAttachments)
            .OrderByDescending(p => p.UpdatedAt)
            .ToListAsync();
    }

    /// <inheritdoc />
    public async Task<Discussion?> UpdateDiscussionAsync(EditDiscussionVm model)
    {
        LoggerUtilities.Info($"[Start] UpdateDiscussion id={model.Id}");
        Discussion? discussion = await _db.Discussions.FindAsync(model.Id);
        if (discussion == null) return null;

        discussion.Title = model.Title.Trim();
        discussion.Description = model.Description?.Trim();
        discussion.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();

        LoggerUtilities.Info($"[Process] UpdateDiscussion saved id={model.Id}");

        // reload with navigation props for accurate counts
        Discussion? updated = await _db.Discussions
            .Include(d => d.DiscussionPosts!)
            .ThenInclude(p => p.DiscussionComments)
            .FirstOrDefaultAsync(d => d.Id == model.Id);
        LoggerUtilities.Info($"[End] UpdateDiscussion id={model.Id} returned={updated != null}");

        return updated;
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> GetTrashedPostsForDiscussionAsync(int discussionId)
    {
        LoggerUtilities.Info($"[Start] GetTrashedPostsForDiscussion discussionId={discussionId}");
        List<DiscussionPost> trashed = await _db.DiscussionPosts
            .Where(p => p.DiscussionId == discussionId &&
                        p.Status == nameof(DiscussionPostStatusType.Trashed) &&
                        p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted))
            .OrderByDescending(p => p.UpdatedAt)
            .Include(p => p.User)
            .ToListAsync();
        LoggerUtilities.Info(
            $"[End] GetTrashedPostsForDiscussion discussionId={discussionId} returnedCount={trashed.Count}");
        return trashed;
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> GetBannedPostsForDiscussionAsync(int discussionId)
    {
        LoggerUtilities.Info($"[Start] GetBannedPostsForDiscussion discussionId={discussionId}");
        List<DiscussionPost> banned = await _db.DiscussionPosts
            .Where(p => p.DiscussionId == discussionId &&
                        p.Status == nameof(DiscussionPostStatusType.Banned) &&
                        p.User != null && p.User.Status != nameof(UserStatusType.PermanentlyDeleted))
            .OrderByDescending(p => p.UpdatedAt)
            .Include(p => p.User)
            .ToListAsync();
        LoggerUtilities.Info(
            $"[End] GetBannedPostsForDiscussion discussionId={discussionId} returnedCount={banned.Count}");
        return banned;
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> UnbanPostsAsync(int[] ids)
    {
        LoggerUtilities.Info($"[Start] UnbanPosts idsCount={ids?.Length ?? 0}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            List<DiscussionPost> discussionPosts = await _db.DiscussionPosts
                .Where(p => (ids ?? Array.Empty<int>()).Contains(p.Id))
                .ToListAsync();

            foreach (DiscussionPost discussionPost in discussionPosts)
                if (discussionPost.Status == nameof(DiscussionPostStatusType.Banned))
                {
                    discussionPost.Status = nameof(DiscussionPostStatusType.Active);
                    discussionPost.Reason = null;
                    discussionPost.UpdatedAt = DateTime.Now.ToLocalTime();
                }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            LoggerUtilities.Info($"[End] UnbanPosts completed updatedCount={discussionPosts.Count}");
            return discussionPosts;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error($"[Error] UnbanPosts failed: {ex.Message}");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> RecoverPostsAsync(int[] ids)
    {
        LoggerUtilities.Info($"[Start] RecoverPosts idsCount={ids?.Length ?? 0}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            List<DiscussionPost> discussionPosts = await _db.DiscussionPosts
                .Where(p => (ids ?? Array.Empty<int>()).Contains(p.Id))
                .ToListAsync();

            foreach (DiscussionPost discussionPost in discussionPosts)
                if (discussionPost.Status == nameof(DiscussionPostStatusType.Trashed))
                {
                    discussionPost.Status = nameof(DiscussionPostStatusType.Active);
                    discussionPost.DeletedOn = null;
                    discussionPost.UpdatedAt = DateTime.Now.ToLocalTime();
                }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            LoggerUtilities.Info($"[End] RecoverPosts completed updatedCount={discussionPosts.Count}");
            return discussionPosts;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error($"[Error] RecoverPosts failed: {ex.Message}");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DiscussionPost?> GetPostByIdWithDetailsAsync(int id)
    {
        LoggerUtilities.Info($"[Start] GetPostByIdWithDetails id={id}");
        DiscussionPost? post = await _db.DiscussionPosts
            .Where(p => p.Id == id)
            .Include(p => p.User)
            .Include(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.User)
            .Include(p => p.DiscussionComments!.Where(dc =>
                dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
            .ThenInclude(dc => dc.DiscussionAttachment)
            .Include(p => p.DiscussionLikeCounts!)
            .Include(p => p.DiscussionAttachments!)
            .FirstOrDefaultAsync();

        PopulateCommentReplies(post);

        LoggerUtilities.Info($"[End] GetPostByIdWithDetails id={id} found={post != null}");
        return post;
    }

    /// <inheritdoc />
    public async Task<DiscussionPost?> BanPostAsync(int id, string? reason)
    {
        LoggerUtilities.Info($"[Start] BanPost id={id}");
        DiscussionPost? discussionPost = await _db.DiscussionPosts.FindAsync(id);
        if (discussionPost == null) return null;

        discussionPost.Status = nameof(DiscussionPostStatusType.Banned);
        discussionPost.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        discussionPost.BannedOn = DateTime.Now.ToLocalTime();
        discussionPost.DeletedOn = null;
        discussionPost.UpdatedAt = DateTime.Now.ToLocalTime();

        await _db.SaveChangesAsync();

        LoggerUtilities.Info($"[Process] BanPost saved id={id}");

        // reload with navigation
        DiscussionPost? result = await GetPostByIdWithDetailsAsync(id);
        LoggerUtilities.Info($"[End] BanPost id={id} returned={result != null}");
        return result;
    }

    /// <inheritdoc />
    public async Task<List<DiscussionPost>> TrashPostsAsync(int[] ids)
    {
        LoggerUtilities.Info($"[Start] TrashPosts idsCount={ids?.Length ?? 0}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            List<DiscussionPost> discussionPosts = await _db.DiscussionPosts
                .Where(p => (ids ?? Array.Empty<int>()).Contains(p.Id))
                .ToListAsync();

            foreach (DiscussionPost discussionPost in discussionPosts)
            {
                discussionPost.Status = nameof(DiscussionPostStatusType.Trashed);
                discussionPost.Reason = null;
                discussionPost.BannedOn = null;
                discussionPost.DeletedOn = DateTime.Now.ToLocalTime();
                discussionPost.UpdatedAt = DateTime.Now.ToLocalTime();
            }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            LoggerUtilities.Info($"[End] TrashPosts completed affectedCount={discussionPosts.Count}");

            return discussionPosts;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error($"[Error] TrashPosts failed: {ex.Message}");
            await transaction.RollbackAsync();
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<int> DeletePostsAsync(int[] ids)
    {
        if (ids == null || ids.Length == 0) return 0;

        LoggerUtilities.Info($"[Start] DeletePosts idsCount={ids.Length}");

        try
        {
            // Get connection string from appsettings.json
            string? connectionString = _configuration.GetConnectionString("DBConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggerUtilities.Error("[Danger] Database connection string 'DBConnection' not found");
                return 0;
            }

            // Convert array to comma-separated string for stored procedure
            string postIdsParam = string.Join(",", ids);

            // Prepare stored procedure parameters
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@PostIds", postIdsParam)
            };

            // Execute stored procedure using DatabaseUtilities
            LoggerUtilities.Info($"[Process] Executing sp_DeleteDiscussionPosts for postIds={postIdsParam}");
            List<DeleteDiscussionPostResult> results = await DatabaseUtilities.ExecuteAsync<DeleteDiscussionPostResult>(
                connectionString,
                "sp_DeleteDiscussionPosts",
                parameters
            );

            // Check if stored procedure execution was successful
            if (results == null || results.Count == 0)
            {
                LoggerUtilities.Error("[Danger] Stored procedure returned no results");
                return 0;
            }

            DeleteDiscussionPostResult firstResult = results[0];
            if (firstResult.Success == 0)
            {
                LoggerUtilities.Error($"[Danger] Stored procedure failed: {firstResult.ErrorMessage}");
                return 0;
            }

            LoggerUtilities.Info("[Process] Stored procedure executed successfully, cleaning up external files");

            // Filter results to get attachment paths for cleanup
            var attachmentsToDelete = results.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
            int attachmentDeleteCount = 0;

            // Delete external attachments from Dropbox
            foreach (DeleteDiscussionPostResult attachment in attachmentsToDelete)
                try
                {
                    if (!string.IsNullOrWhiteSpace(attachment.FilePath))
                    {
                        await _dropboxService.DeleteImageAsync(attachment.FilePath);
                        attachmentDeleteCount++;
                        LoggerUtilities.Info(
                            $"[Process] Deleted {attachment.FileType} attachment: {attachment.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning(
                        $"[Process] Failed to delete attachment file: {attachment.FilePath}, Error: {ex.Message}");
                    // Continue with other files even if one fails
                }

            int deletedCount = ids.Length;
            LoggerUtilities.Info(
                $"[End] DeletePosts completed. PostsDeleted={deletedCount}, AttachmentsDeleted={attachmentDeleteCount}");

            return deletedCount;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error deleting posts idsCount={ids?.Length ?? 0}");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<DiscussionPost?> RemoveCommentAsync(int commentId, int postId)
    {
        LoggerUtilities.Info($"[Start] RemoveComment commentId={commentId} postId={postId}");

        try
        {
            // Get connection string from appsettings.json
            string? connectionString = _configuration.GetConnectionString("DBConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggerUtilities.Error("[Danger] Database connection string 'DBConnection' not found");
                return null;
            }

            // Prepare stored procedure parameters
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@CommentId", commentId),
                new SqlParameter("@PostId", postId)
            };

            // Execute stored procedure using DatabaseUtilities
            LoggerUtilities.Info(
                $"[Process] Executing sp_DeleteDiscussionComment for commentId={commentId} postId={postId}");
            List<DeleteDiscussionCommentResult> results =
                await DatabaseUtilities.ExecuteAsync<DeleteDiscussionCommentResult>(
                    connectionString,
                    "sp_DeleteDiscussionComment",
                    parameters
                );

            // Check if stored procedure execution was successful
            if (results == null || results.Count == 0)
            {
                LoggerUtilities.Error("[Danger] Stored procedure returned no results");
                return null;
            }

            DeleteDiscussionCommentResult firstResult = results[0];
            if (firstResult.Success == 0)
            {
                LoggerUtilities.Error($"[Danger] Stored procedure failed: {firstResult.ErrorMessage}");
                return null;
            }

            LoggerUtilities.Info("[Process] Stored procedure executed successfully, cleaning up external files");

            // Filter results to get attachment paths for cleanup
            var attachmentsToDelete = results.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
            int attachmentDeleteCount = 0;

            // Delete external attachments from Dropbox
            foreach (DeleteDiscussionCommentResult attachment in attachmentsToDelete)
                try
                {
                    if (!string.IsNullOrWhiteSpace(attachment.FilePath))
                    {
                        await _dropboxService.DeleteImageAsync(attachment.FilePath);
                        attachmentDeleteCount++;
                        LoggerUtilities.Info($"[Process] Deleted attachment: {attachment.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning(
                        $"[Process] Failed to delete attachment file: {attachment.FilePath}, Error: {ex.Message}");
                    // Continue with other files even if one fails
                }

            LoggerUtilities.Info(
                $"[Process] Successfully deleted comment and nested replies. Attachments deleted: {attachmentDeleteCount}");

            // Get updated post with all navigation properties
            DiscussionPost? post = await _db.DiscussionPosts
                .Where(p => p.Id == postId)
                .Include(p => p.User)
                .Include(p => p.DiscussionComments!.Where(dc =>
                    dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
                .ThenInclude(dc => dc.User)
                .Include(p => p.DiscussionComments!.Where(dc =>
                    dc.User != null && dc.User.Status != nameof(UserStatusType.PermanentlyDeleted)))
                .ThenInclude(dc => dc.DiscussionAttachment)
                .Include(p => p.DiscussionLikeCounts!)
                .Include(p => p.DiscussionAttachments!)
                .FirstOrDefaultAsync();

            PopulateCommentReplies(post);

            LoggerUtilities.Info(
                $"[End] RemoveComment commentId={commentId} postId={postId} returnedPost={post != null}");

            return post;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error deleting comment commentId={commentId} postId={postId}");
            return null;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteUserDiscussionsAsync(int[] userIds)
    {
        if (userIds == null || userIds.Length == 0) return true;

        LoggerUtilities.Info($"[Start] DeleteUserDiscussions userIdsCount={userIds.Length}");

        try
        {
            // Get connection string from appsettings.json
            string? connectionString = _configuration.GetConnectionString("DBConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggerUtilities.Error("[Danger] Database connection string 'DBConnection' not found");
                return false;
            }

            // Convert array to comma-separated string for stored procedure
            string userIdsParam = string.Join(",", userIds);

            // Prepare stored procedure parameters
            List<SqlParameter> parameters = new()
            {
                new SqlParameter("@UserIds", userIdsParam)
            };

            // Execute stored procedure using DatabaseUtilities
            LoggerUtilities.Info($"[Process] Executing sp_DeleteUserDiscussions for userIds={userIdsParam}");
            List<DeleteUserDiscussionsResult> results =
                await DatabaseUtilities.ExecuteAsync<DeleteUserDiscussionsResult>(
                    connectionString,
                    "sp_DeleteUserDiscussions",
                    parameters
                );

            // Check if stored procedure execution was successful
            if (results == null || results.Count == 0)
            {
                LoggerUtilities.Error("[Danger] Stored procedure returned no results");
                return false;
            }

            DeleteUserDiscussionsResult firstResult = results[0];
            if (firstResult.Success == 0)
            {
                LoggerUtilities.Error($"[Danger] Stored procedure failed: {firstResult.ErrorMessage}");
                return false;
            }

            LoggerUtilities.Info("[Process] Stored procedure executed successfully, cleaning up external files");

            // Filter results to get attachment paths for cleanup
            var attachmentsToDelete = results.Where(r => !string.IsNullOrWhiteSpace(r.FilePath)).ToList();
            int attachmentDeleteCount = 0;

            // Delete external attachments from Dropbox
            foreach (DeleteUserDiscussionsResult attachment in attachmentsToDelete)
                try
                {
                    await _dropboxService.DeleteImageAsync(attachment.FilePath!);
                    attachmentDeleteCount++;
                    LoggerUtilities.Info(
                        $"[Process] Deleted {attachment.FileType} attachment: {attachment.FilePath}");
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning(
                        $"[Process] Failed to delete attachment file: {attachment.FilePath}, Error: {ex.Message}");
                    // Continue with other files even if one fails
                }

            LoggerUtilities.Info(
                $"[End] DeleteUserDiscussions completed. AttachmentsDeleted={attachmentDeleteCount}");

            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error deleting user discussions userIdsCount={userIds?.Length ?? 0}");
            return false;
        }
    }

    /// <summary>
    ///     Populates the Replies navigation property for all comments in a post.
    /// </summary>
    /// <param name="post">The post whose comments need Replies populated.</param>
    private static void PopulateCommentReplies(DiscussionPost? post)
    {
        if (post?.DiscussionComments == null) return;

        List<DiscussionComment> allComments = post.DiscussionComments.ToList();
        List<DiscussionComment> topLevelComments = allComments.Where(c => c.ReplyCommentId == null).ToList();
        foreach (DiscussionComment parent in topLevelComments)
            parent.Replies = GetRepliesRecursive(parent, allComments);
    }

    /// <summary>
    ///     Recursively builds the Replies collection for a parent comment.
    /// </summary>
    /// <param name="parent">The parent comment to find replies for.</param>
    /// <param name="allComments">All comments for the post.</param>
    /// <returns>A list of direct and nested replies.</returns>
    private static List<DiscussionComment> GetRepliesRecursive(DiscussionComment parent,
        List<DiscussionComment> allComments)
    {
        List<DiscussionComment> replies = allComments
            .Where(c => c.ReplyCommentId == parent.Id)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        foreach (DiscussionComment reply in replies) reply.Replies = GetRepliesRecursive(reply, allComments);

        return replies;
    }
}