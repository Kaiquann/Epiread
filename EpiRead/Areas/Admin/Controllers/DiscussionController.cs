using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead.Areas.Admin.Controllers;

/// <summary>
///     DiscussionController (Admin area) - manages discussion forums, their
///     posts, and comments. Supports listing forums with search and category
///     filters, viewing and paginating posts, and moderation actions such as
///     ban, trash, recover and delete. Helper endpoints return partial views
///     used by AJAX callers. Business operations are delegated to the Db
///     context and domain services (e.g., IBookCategoryService and
///     IDropboxService).
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.RequireAdminOrHigher)]
[Area("Admin")]
public class DiscussionController : Controller
{
    private const int MaxSearchLength = 200;
    private readonly IBookCategoryService _bookCategoryService;
    private readonly DB _db;
    private readonly IDiscussionService _discussionService;

    public DiscussionController(DB db, IBookCategoryService bookCategoryService, IDiscussionService discussionService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _bookCategoryService = bookCategoryService ?? throw new ArgumentNullException(nameof(bookCategoryService));
        _discussionService = discussionService ?? throw new ArgumentNullException(nameof(discussionService));
    }

    /// <summary>
    ///     Display a paginated list of discussion forums. Supports an optional
    ///     search term, sorting, sort direction, and an optional book category
    ///     filter.
    /// </summary>
    /// <param name="search">Search string to match against title or description.</param>
    /// <param name="sort">Column to sort by (e.g., Title, Total Posts).</param>
    /// <param name="dir">Sort direction ("des" for descending or null/other for ascending).</param>
    /// <param name="filter">Optional category id to filter discussions.</param>
    /// <param name="page">Page number for pagination.</param>
    public async Task<IActionResult> Index(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        LoggerUtilities.Info("[Start] listing discussions. search='" + search + "', filter='" + filter + "', page=" +
                             page);
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        LoggerUtilities.Info("[Process] fetched book categories. count=" + (bookCategories?.Count ?? 0));
        SelectList bookCategorySelectList = new(bookCategories, "Id", "Name");
        ViewBag.BookCategories = bookCategorySelectList;

        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        // Parse filter for category
        int filterId = int.TryParse(filter, out int fid) ? fid : -1;
        bool hasCategoryFilter = filterId > 0;

        List<Discussion> searched =
            await _discussionService.GetFilteredDiscussionsAsync(search, hasCategoryFilter ? filterId : null);

        LoggerUtilities.Info("[Process] fetched " + (searched?.Count ?? 0) + " discussions from database.");

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        IPagedList<Discussion> model = SortAndPaginateDiscussions(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for page " + page);
            return PartialView("_Index", model);
        }

        LoggerUtilities.Info("[End] returning view for page " + page);
        return View(model);
    }

    /// <summary>
    ///     Show the posts for a discussion forum identified by id. Only active
    ///     (non-deleted) posts are returned and the results are paginated.
    /// </summary>
    /// <param name="id">Discussion forum id.</param>
    /// <param name="search">Optional search term to filter posts by title or content.</param>
    /// <param name="page">Page number for pagination.</param>
    public async Task<IActionResult> Post(int id, string? search, int page = 1)
    {
        LoggerUtilities.Info("[Start] loading discussion id=" + id + ", search='" + search + "', page=" + page);
        Discussion? discussion = await _discussionService.GetDiscussionWithNavigationAsync(id);

        if (discussion == null) return NotFound();

        // Load posts for the forum (only non-deleted). Order by UpdatedAt desc
        // Trim and limit search term length for performance and security
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        if (search != null && search.Length > MaxSearchLength) search = search[..MaxSearchLength];
        List<DiscussionPost> posts = await _discussionService.GetActivePostsForDiscussionAsync(id, search);

        LoggerUtilities.Info("[Process] loaded " + (posts?.Count ?? 0) + " posts for discussion id=" + id + ".");

        IPagedList<DiscussionPost> pagedPosts = posts.ToPagedList(page, 5);

        // Build view model
        DiscussionShowVm vm = new DiscussionShowVm
        {
            Discussion = discussion,
            Posts = pagedPosts
        };

        ViewBag.ForumId = id;
        ViewBag.Title = discussion.Title;
        ViewBag.Search = search;

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial posts for discussion id=" + id);
            return PartialView("_Posts", vm.Posts);
        }

        LoggerUtilities.Info("[End] returning view for discussion id=" + id);
        return View(vm);
    }

    /// <summary>
    ///     Update a forum's title and description using values from the
    ///     provided edit view model. Returns an updated partial when called
    ///     via AJAX so the client can refresh the UI in-place.
    /// </summary>
    /// <param name="model">View model containing the discussion id and updated fields.</param>
    [HttpPost]
    public async Task<IActionResult> Edit(EditDiscussionVm model)
    {
        LoggerUtilities.Info("[Start] editing discussion id=" + model.Id);
        if (!ModelState.IsValid)
        {
            TempData["SweetAlert.Danger"] = "Invalid input.";
            return RedirectToAction("Post", new { id = model.Id });
        }

        Discussion? discussion = await _discussionService.GetDiscussionWithNavigationAsync(model.Id);
        if (discussion == null) return NotFound();

        discussion.Title = model.Title?.Trim() ?? string.Empty;
        discussion.Description = model.Description?.Trim();
        discussion.UpdatedAt = DateTime.Now.ToLocalTime();

        LoggerUtilities.Info("[Process] saving discussion id=" + model.Id);
        Discussion? updatedDiscussion = await _discussionService.UpdateDiscussionAsync(model);
        if (updatedDiscussion == null) return NotFound();
        LoggerUtilities.Info("[End] saved discussion id=" + model.Id);
        LoggerUtilities.Info("[End] saved discussion id=" + model.Id);

        TempData["SweetAlert.Success"] = "Forum updated successfully.";

        // If request comes from AJAX, return the updated partial so client can replace it in-place
        if (Request.IsAjax())
        {
            // Reload discussion with navigation properties to ensure counts are accurate
            Discussion? updated = updatedDiscussion;

            if (updated == null) return NotFound();
            LoggerUtilities.Info("[Process] reloaded updated discussion id=" + model.Id);
            return PartialView("_ForumDetailsView", updated);
        }

        return RedirectToAction("Post", new { id = model.Id });
    }

    /// <summary>
    ///     Returns trashed posts for a forum. The results are paginated and
    ///     intended for the admin 'TrashedPosts' view.
    /// </summary>
    /// <param name="id">Discussion forum id.</param>
    /// <param name="page">Page number for pagination.</param>
    [HttpGet]
    public async Task<IActionResult> TrashedPosts(int id, int page = 1)
    {
        LoggerUtilities.Info("[Start] retrieving trashed posts for discussion id=" + id + ", page=" + page);
        List<DiscussionPost> trashedDiscussionPosts = await _discussionService.GetTrashedPostsForDiscussionAsync(id);
        LoggerUtilities.Info("[Process] fetched " + (trashedDiscussionPosts?.Count ?? 0) +
                             " trashed posts for discussion id=" + id);

        ViewBag.Id = id;
        IPagedList<DiscussionPost> paged = trashedDiscussionPosts.ToPagedList(page, 10);
        if (page < 1) return RedirectToAction(null, new { forumId = id, page = 1 });
        if (page > paged.PageCount && paged.PageCount > 0)
            return RedirectToAction(null, new { forumId = id, page = paged.PageCount });

        if (Request.IsAjax()) return PartialView("_TrashedPosts", paged);
        return View("TrashedPosts", paged);
    }

    /// <summary>
    ///     Returns banned posts for a forum. The results are paginated and
    ///     intended for the admin 'BannedPosts' view.
    /// </summary>
    /// <param name="id">Discussion forum id.</param>
    /// <param name="page">Page number for pagination.</param>
    [HttpGet]
    public async Task<IActionResult> BannedPosts(int id, int page = 1)
    {
        LoggerUtilities.Info("[Start] retrieving banned posts for discussion id=" + id + ", page=" + page);
        List<DiscussionPost> bannedPosts = await _discussionService.GetBannedPostsForDiscussionAsync(id);
        LoggerUtilities.Info("[Process] fetched " + (bannedPosts?.Count ?? 0) + " banned posts for discussion id=" +
                             id);

        ViewBag.Id = id;
        IPagedList<DiscussionPost> paged = bannedPosts.ToPagedList(page, 10);
        if (page < 1) return RedirectToAction(null, new { forumId = id, page = 1 });
        if (page > paged.PageCount && paged.PageCount > 0)
            return RedirectToAction(null, new { forumId = id, page = paged.PageCount });

        if (Request.IsAjax()) return PartialView("_BannedPosts", paged);
        return View("BannedPosts", paged);
    }

    /// <summary>
    ///     Allows an administrator to unban one or more discussion posts. If
    ///     called via AJAX from a post details page, returns the updated
    ///     post partial so the client can update the display.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to unban.</param>
    [HttpPost]
    public async Task<IActionResult> UnbanPost(int[] ids)
    {
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);
            LoggerUtilities.Info("[Start] unbanning " + ids.Length + " post(s)");
            LoggerUtilities.Info("[Process] beginning database transaction");
            LoggerUtilities.Info("[Process] began transaction");
            try
            {
                List<DiscussionPost> updated = await _discussionService.UnbanPostsAsync(ids);
                LoggerUtilities.Info("[Process] unbanned " + updated.Count + " posts via service");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[End] UnbanPost failed: " + ex.Message);
                throw;
            }

            TempData[SweetAlert.Success] = $"Unbanned {ids.Length} post(s) successfully.";

            if (Request.IsAjax())
            {
                // if url is PostDetails, return updated post details partial
                string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();
                if (referer.Contains("postdetails"))
                {
                    LoggerUtilities.Info("[Process] reloading post id=" + ids.First() + " for partial view");
                    DiscussionPost? updatedPost = await _discussionService.GetPostByIdWithDetailsAsync(ids.First());

                    if (updatedPost == null) return NotFound();
                    return PartialView("_PostDetails", updatedPost);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("UnbanPost failed for ids=" + (ids == null ? "null" : string.Join(',', ids)) + " " +
                                  ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Recover one or more posts previously moved to trash. On success
    ///     posts are set back to the active state. If called via AJAX from a
    ///     post details page, returns the updated post partial.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to recover.</param>
    [HttpPost]
    public async Task<IActionResult> RecoverPost(int[] ids)
    {
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);
            LoggerUtilities.Info("[Start] recovering " + ids.Length + " post(s)");
            LoggerUtilities.Info("[Process] beginning database transaction");
            LoggerUtilities.Info("[Process] began transaction");
            try
            {
                List<DiscussionPost> recovered = await _discussionService.RecoverPostsAsync(ids);
                LoggerUtilities.Info("[Process] recovered " + recovered.Count + " posts via service");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("[End] RecoverPost failed: " + ex.Message);
                throw;
            }

            TempData[SweetAlert.Success] = $"Recovered {ids.Length} post(s) successfully.";

            if (Request.IsAjax())
            {
                // if url is PostDetails, return updated post details partial
                string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();
                if (referer.Contains("postdetails"))
                {
                    // Reload the post with navigation properties
                    DiscussionPost? updatedPost = await _discussionService.GetPostByIdWithDetailsAsync(ids.First());

                    if (updatedPost == null) return NotFound();
                    return PartialView("_PostDetails", updatedPost);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("RecoverPost failed for ids=" + (ids == null ? "null" : string.Join(',', ids)) + " " +
                                  ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Display detailed information for a single discussion post
    ///     including related user, comments, likes and attachments. Returns
    ///     the 'PostDetails' view; used by admins to moderate a specific post.
    /// </summary>
    /// <param name="id">Identifier of the discussion post to load.</param>
    [HttpGet]
    public async Task<IActionResult> PostDetails(int id)
    {
        LoggerUtilities.Info("[Start] loading post id=" + id);
        DiscussionPost? discussionPost = await _discussionService.GetPostByIdWithDetailsAsync(id);

        if (discussionPost == null)
        {
            LoggerUtilities.Warning("[Process] post id=" + id + " not found");
            return Request.RedirectToReferer();
        }

        LoggerUtilities.Info("[Process] loaded post id=" + id);
        LoggerUtilities.Info("[End] returning view for post id=" + id);
        return View("PostDetails", discussionPost);
    }

    /// <summary>
    ///     Return only the comments and related data for a discussion post as
    ///     a partial view. This is used to dynamically refresh comments in
    ///     the admin post view (AJAX callers).
    /// </summary>
    /// <param name="postId">The id of the post whose comments should be returned.</param>
    [HttpGet]
    public async Task<IActionResult> PostComments(int postId)
    {
        LoggerUtilities.Info("[Start] loading comments for post id=" + postId);
        DiscussionPost? post = await _discussionService.GetPostByIdWithDetailsAsync(postId);

        if (post == null)
        {
            LoggerUtilities.Warning("[Process] post id=" + postId + " not found");
            return Request.RedirectToReferer();
        }

        LoggerUtilities.Info("[Process] loaded comments for post id=" + postId);
        LoggerUtilities.Info("[End] returning comments partial for post id=" + postId);
        return PartialView("_PostComments", post);
    }

    // -------------------- Admin moderation actions --------------------

    /// <summary>
    ///     Set a discussion post's status to 'Banned', record an optional
    ///     reason, update metadata and optionally return an updated post
    ///     partial for AJAX calls.
    /// </summary>
    /// <param name="id">The id of the post to ban.</param>
    /// <param name="reason">Optional reason for banning the post.</param>
    [HttpPost]
    public async Task<IActionResult> BanPost(int id, string? reason)
    {
        try
        {
            LoggerUtilities.Info("[Start] banning post id=" + id);
            DiscussionPost? discussionPost = await _discussionService.GetPostByIdWithDetailsAsync(id);
            if (discussionPost == null) throw new Exception("Post not found.");

            discussionPost.Status = nameof(DiscussionPostStatusType.Banned);
            discussionPost.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason?.Trim();
            discussionPost.BannedOn = DateTime.Now.ToLocalTime();
            discussionPost.DeletedOn = null;
            discussionPost.UpdatedAt = DateTime.Now.ToLocalTime();
            // Persist using service
            DiscussionPost? banned = await _discussionService.BanPostAsync(id, reason);
            if (banned == null) throw new Exception("Post not found.");
            LoggerUtilities.Info("[Process] saved banned status for post id=" + id);
            LoggerUtilities.Info("[End] banned post id=" + id);

            TempData[SweetAlert.Success] = "Post has been banned successfully.";

            if (Request.IsAjax())
            {
                // if url is PostDetails, return updated post details partial
                string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();
                if (referer.Contains("postdetails"))
                {
                    LoggerUtilities.Info("[Process] reloading post id=" + id + " for partial view");
                    // Reload the post with navigation properties
                    DiscussionPost? updatedPost = await _discussionService.GetPostByIdWithDetailsAsync(id);

                    if (updatedPost == null) return NotFound();
                    return PartialView("_PostDetails", updatedPost);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] failed for id=" + id + ": " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Move one or more posts to trash (soft delete). This marks the post
    ///     status as 'Trashed' and records a deletion timestamp. Returns an
    ///     updated post partial for AJAX calls from the post details page.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to move to trash.</param>
    [HttpPost]
    public async Task<IActionResult> TrashPost(int[] ids)
    {
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            LoggerUtilities.Info("[Start] moving " + ids.Length + " post(s) to trash");
            LoggerUtilities.Info("[Process] beginning database transaction");
            LoggerUtilities.Info("[Process] began transaction");
            try
            {
                List<DiscussionPost> discussionPosts = await _discussionService.TrashPostsAsync(ids);
                LoggerUtilities.Info("[Process] trashed " + discussionPosts.Count + " posts via service");
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error("TrashPost failed: " + ex.Message);
                throw;
            }

            TempData[SweetAlert.Success] = "Post has been moved to trash successfully.";

            if (Request.IsAjax())
            {
                // if url is PostDetails, return updated post details partial
                string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();
                if (referer.Contains("postdetails"))
                {
                    DiscussionPost? updatedPost = await _discussionService.GetPostByIdWithDetailsAsync(ids.First());

                    if (updatedPost == null) return NotFound();
                    return PartialView("_PostDetails", updatedPost);
                }
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("TrashPost failed for ids=" + (ids == null ? "null" : string.Join(',', ids)) + ": " +
                                  ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Permanently delete one or more posts, their attachments, comments,
    ///     replies, and like counts. Any associated files stored externally
    ///     (e.g., Dropbox) are removed. On success returns a redirect or a
    ///     partial used by AJAX callers.
    /// </summary>
    /// <param name="ids">Array of discussion post ids to delete.</param>
    /// <param name="discussionId">Optional id of the parent discussion, used for redirects.</param>
    [HttpPost]
    public async Task<IActionResult> DeletePost(int[] ids, int? discussionId)
    {
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            LoggerUtilities.Info("[Start] deleting " + ids.Length + " post(s)");
            int deleted = await _discussionService.DeletePostsAsync(ids);
            TempData[SweetAlert.Success] = $"Permanently deleted {deleted} post(s) successfully.";
        }
        catch (Exception ex)
        {
            TempData[SweetAlert.Danger] = ex.Message;
        }

        if (Request.IsAjax())
        {
            // if url is PostDetails, return to forum posts list since the post is deleted
            string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();
            if (referer.Contains("postdetails"))
                // return RedirectToAction("Post", new { id = discussionId });
                return Request.RedirectToReferer(this, "Post", "Discussion", new { id = discussionId });
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Remove a comment and its replies, attachments and like counts.
    ///     Returns an updated comments partial when called via AJAX so client
    ///     can refresh the discussion post comments.
    /// </summary>
    /// <param name="commentId">The id of the comment to remove.</param>
    /// <param name="postId">The id of the post the comment belongs to.</param>
    [HttpPost]
    public async Task<IActionResult> RemoveComment(int commentId, int postId)
    {
        LoggerUtilities.Info("[Start] removing comment id=" + commentId + " from post id=" + postId);

        // Use DiscussionService to remove comment and return the updated post with navigation
        DiscussionPost? postAfterRemoval = await _discussionService.RemoveCommentAsync(commentId, postId);

        if (postAfterRemoval == null)
        {
            TempData[SweetAlert.Danger] = "Failed to remove comment.";

            // If AJAX, return current state
            if (Request.IsAjax())
            {
                DiscussionPost? currentPost = await _discussionService.GetPostByIdWithDetailsAsync(postId);
                if (currentPost != null)
                    return PartialView("_PostComments", currentPost);
            }

            return RedirectToAction("PostDetails", new { id = postId });
        }

        TempData[SweetAlert.Success] = "Comment removed.";

        // If AJAX, return refreshed comments partial for the post
        if (Request.IsAjax())
        {
            string referer = Request.Headers["Referer"].ToString().ToLowerInvariant();

            // If caller is a post details page, return the comments partial
            if (referer.Contains("postdetails")) return PartialView("_PostComments", postAfterRemoval);

            // Otherwise default to returning the forum's posts list partial so the caller can update its UI
            int forumId = postAfterRemoval.DiscussionId;
            List<DiscussionPost> posts = await _discussionService.GetActivePostsForDiscussionAsync(forumId);

            IPagedList<DiscussionPost> paged = posts.ToPagedList(1, 10);
            return PartialView("_Posts", paged);
        }

        return RedirectToAction("PostDetails", new { id = postId });
    }

    #region Helper

    /// <summary>
    ///     Sort and paginate a list of discussions. Sorting keys include
    ///     Title, Description, Total Posts, and Total Comments. This is a
    ///     private helper used by the `Index` action.
    /// </summary>
    /// <param name="searched">Pre-filtered list of discussions to sort and paginate.</param>
    /// <param name="sort">Sort key to decide the order of the list.</param>
    /// <param name="dir">Sort direction; "des" will order descending.</param>
    /// <param name="page">Page number to return.</param>
    private IPagedList<Discussion> SortAndPaginateDiscussions(List<Discussion> searched, string? sort, string? dir,
        int page = 1)
    {
        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Discussion, object> fn = sort switch
        {
            "Title" => s => s.Title,
            "Description" => s => s.Description ?? string.Empty,
            "Total Posts" => s => s.DiscussionPosts?.Count ?? 0,
            "Total Comments" => s =>
                s.DiscussionPosts != null
                    ? s.DiscussionPosts.Sum(p => p.DiscussionComments != null ? p.DiscussionComments.Count : 0)
                    : 0,
            _ => s => s.Id
        };

        IOrderedEnumerable<Discussion> sorted = dir == "des"
            ? searched.AsEnumerable().OrderByDescending(fn)
            : searched.AsEnumerable().OrderBy(fn);

        // (3) Paging ---------------------------
        IPagedList<Discussion> pagedList = sorted.ToPagedList(page, 10);

        return pagedList;
    }

    #endregion
}