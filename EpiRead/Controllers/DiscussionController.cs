using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EpiRead.Controllers;

[Authorize]
public class DiscussionController : Controller
{
    private readonly IContentModerationService _contentModerationService;
    private readonly DB _db;
    private readonly IDropboxService _dropboxService;

    public DiscussionController(DB db, IDropboxService dropboxService,
        IContentModerationService contentModerationService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _dropboxService = dropboxService ?? throw new ArgumentNullException(nameof(dropboxService));
        _contentModerationService = contentModerationService ??
                                    throw new ArgumentNullException(nameof(contentModerationService));
    }

    public async Task<IActionResult> Index(int? bookId, int? discussionId, string? search, string? sortBy)
    {
        LoggerUtilities.Info($"[Start] Loading discussion bookId={bookId} discussionId={discussionId}");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
        {
            TempData[SweetAlert.Danger] = "You must be logged in to view discussion.";
            return RedirectToAction("Index", "Login");
        }

        // Find the selected book
        Book? book =
            await _db.Books.FirstOrDefaultAsync(b => b.Id == bookId && b.Status == nameof(BookStatusType.Available));
        BookCategory? bookCategory = await _db.BookCategories
            .Include(c => c.BookCategoryMaps)
            .ThenInclude(map => map.Book)
            .FirstOrDefaultAsync(c => c.BookCategoryMaps.Any(map => map.BookId == bookId));

        Discussion? discussion = await _db.Discussions
            .FirstOrDefaultAsync(d => d.BookId == bookId || d.Id == discussionId);
        LoggerUtilities.Info($"[Process] Discussion lookup completed found={discussion != null}");

        if (discussion == null)
        {
            TempData[SweetAlert.Danger] = "Forum not found.";
            return RedirectToAction("Index", "Book");
        }

        if (book == null)
        {
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index", "Book");
        }

        // Base query
        IQueryable<DiscussionPost> query = _db.DiscussionPosts
            .Include(p => p.DiscussionLikeCounts)
            .Include(p => p.DiscussionAttachments)
            .Include(p => p.User)
            .Where(p => p.DiscussionId == discussion.Id && p.Status == nameof(DiscussionPostStatusType.Active))
            .AsQueryable();

        // ✅ Search filter
        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(p =>
                p.Title.Contains(search) ||
                (p.User != null && p.User.UserName.Contains(search)));

        // ✅ Sorting filter
        switch (sortBy)
        {
            case "views":
                query = query.OrderByDescending(p => p.ViewCount);
                break;
            case "likes":
                query = query.OrderByDescending(p => p.DiscussionLikeCounts.Count);
                break;
            default:
                query = query.OrderByDescending(p => p.CreatedAt);
                break;
        }

        List<DiscussionPost> posts = await query.ToListAsync();
        LoggerUtilities.Info($"[Process] Retrieved {posts.Count} discussion posts");

        // Average rating
        List<Rating> bookRatings = await _db.Ratings
            .Where(r => r.BookId == bookId)
            .ToListAsync();

        double averageRating = bookRatings.Any() ? bookRatings.Average(r => r.StartCount) : 0.0;

        // Create a ViewModel to pass both book details and discussion posts
        DiscussionVm vm = new()
        {
            Book = book,
            DiscussionPosts = posts,
            BookCategory = bookCategory,
            AverageRating = averageRating,
            DiscussionId = discussion.Id,
            User = currentUser,
            Search = search,
            SortBy = sortBy
        };
        LoggerUtilities.Info("[End] Discussion view prepared");
        if (Request.IsAjax())
            return PartialView("_DiscussionPostList", vm);

        return View(vm);
    }


    [HttpPost]
    public async Task<IActionResult> CreatePost(CreatePostVm createPostVm, string content)
    {
        LoggerUtilities.Info($"[Start] Create discussion post discussionId={createPostVm.DiscussionId}");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
        {
            TempData[SweetAlert.Danger] = "You must be logged in to create a post.";
            return RedirectToAction("Index", "Login");
        }

        // Check for offensive content using AI
        LoggerUtilities.Info("[Start] AI content moderation check");
        ContentModerationResult moderationResult = await _contentModerationService.AnalyzeContentAsync(content);
        LoggerUtilities.Info("[Process] Content moderation completed");
        if (!moderationResult.IsOffensive)
        {
            await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                DiscussionPost newPost = new()
                {
                    DiscussionId = createPostVm.DiscussionId,
                    UserId = currentUser.Id,
                    Title = createPostVm.Title,
                    CreatedAt = DateTime.Now.ToLocalTime()
                };

                _db.DiscussionPosts.Add(newPost);
                await _db.SaveChangesAsync();

                string error = await ProcessEmbeddedImagesAsync(null, newPost, content);
                if (!string.IsNullOrEmpty(error)) throw new Exception(error);

                await transaction.CommitAsync();
                LoggerUtilities.Info("[End] Discussion post created successfully");
                TempData[SweetAlert.Success] = "Your post has been created successfully!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                LoggerUtilities.Error(ex, "[Process] Failed to create discussion post");
                TempData[SweetAlert.Danger] = ex.Message;
            }

            return RedirectToAction("Index", new { bookId = createPostVm.BookId });
        }

        TempData[SweetAlert.Danger] =
            $"Your post contains offensive content and cannot be published. Reason: {moderationResult.Reason}";
        return RedirectToAction("Index", new { bookId = createPostVm.BookId });
    }


    [HttpPost]
    public async Task<IActionResult> ToggleLike(int discussionPostId, int bookId, string source)
    {
        User? currentUser = await User.GetCurrentUser(_db);
        DiscussionPost? post = await _db.DiscussionPosts
            .Include(p => p.Discussion)
            .ThenInclude(p => p.Book)
            .Include(p => p.DiscussionAttachments)
            .Include(p => p.DiscussionLikeCounts)
            .Include(p => p.User)
            .Where(p => p.Id == discussionPostId)
            .FirstOrDefaultAsync();

        if (currentUser == null)
            return Unauthorized();

        if (post == null)
        {
            TempData[SweetAlert.Danger] = "Post not found!";
            return RedirectToAction("Index", new { bookId });
        }

        // Like / Unlike logic
        DiscussionLikeCount? existingLike = await _db.DiscussionLikeCounts
            .FirstOrDefaultAsync(l => l.UserId == currentUser.Id && l.DiscussionPostId == discussionPostId);

        if (existingLike != null)
            _db.DiscussionLikeCounts.Remove(existingLike);
        else
            _db.DiscussionLikeCounts.Add(new DiscussionLikeCount
            {
                DiscussionPostId = discussionPostId,
                UserId = currentUser.Id
            });

        await _db.SaveChangesAsync();

        // Refresh data for updated view
        Book? book = await _db.Books.FirstOrDefaultAsync(b => b.Id == bookId);
        List<DiscussionPost> posts = await _db.DiscussionPosts
            .Include(p => p.DiscussionLikeCounts)
            .Include(p => p.User)
            .Where(p => p.DiscussionId == post.DiscussionId)
            .OrderByDescending(p => p.CreatedAt)
            .ToListAsync();

        // Return correct partial depending on source
        if (Request.IsAjax())
        {
            if (source == "book-forum")
            {
                DiscussionVm vm = new()
                {
                    Book = book,
                    DiscussionPosts = posts,
                    DiscussionId = post.DiscussionId,
                    User = currentUser
                };
                return PartialView("_DiscussionPost", vm);
            }

            if (source == "post-comment")
            {
                DiscussionPostVm commentVm = new()
                {
                    Post = post,
                    Comments = await _db.DiscussionComments
                        .Include(c => c.User)
                        .Where(c => c.DiscussionPostId == discussionPostId)
                        .OrderByDescending(c => c.CreatedAt)
                        .ToListAsync(),
                    User = currentUser
                };
                return PartialView("_LikeButton", commentVm);
            }
        }

        return RedirectToAction("Index", new { bookId, discussionId = post.DiscussionId });
    }


    public async Task<IActionResult> DiscussionPostComment(int discussionPostId)
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null)
            return Unauthorized();

        ViewData["CurrentUserId"] = currentUser.Id;
        DiscussionPost? post = await _db.DiscussionPosts
            .Include(p => p.Discussion)
            .ThenInclude(p => p.Book)
            .Include(p => p.DiscussionAttachments)
            .Include(p => p.DiscussionLikeCounts)
            .Include(p => p.User)
            .Where(p => p.Id == discussionPostId)
            .FirstOrDefaultAsync();

        List<DiscussionComment> comments = await _db.DiscussionComments
            .Include(c => c.DiscussionLikeCounts)
            .Include(c => c.User)
            .Where(c => c.DiscussionPostId == discussionPostId &&
                        c.Status == nameof(DiscussionCommentStatusType.Active) &&
                        c.User.Status == nameof(UserStatusType.Active))
            .OrderBy(c => c.CreatedAt)
            .ToListAsync();

        // Separate top-level comments and replies
        List<DiscussionComment> topLevelComments = comments
            .Where(c => c.ReplyCommentId == null)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        foreach (DiscussionComment parent in topLevelComments) parent.Replies = GetRepliesRecursive(parent, comments);


        if (post == null)
        {
            TempData[SweetAlert.Success] = "No post found !";
            return RedirectToAction("Index");
        }

        post.ViewCount++;
        _db.DiscussionPosts.Update(post);
        await _db.SaveChangesAsync();

        DiscussionPostVm vm = new()
        {
            Post = post,
            Comments = topLevelComments,
            User = currentUser
        };

        return View(vm);
    }

    [HttpPost]
    public async Task<IActionResult> ToggleLikeForComment(int discussionCommentId)
    {
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null)
            return Unauthorized();

        ViewData["CurrentUserId"] = currentUser.Id;
        DiscussionComment? comment = await _db.DiscussionComments
            .Include(c => c.DiscussionLikeCounts)
            .FirstOrDefaultAsync(c => c.Id == discussionCommentId);

        if (comment == null)
            return NotFound();

        DiscussionLikeCount? existingLike = comment.DiscussionLikeCounts
            .FirstOrDefault(l => l.UserId == currentUser.Id && l.DiscussionCommentId == discussionCommentId);

        if (existingLike != null)
            _db.DiscussionLikeCounts.Remove(existingLike);
        else
            _db.DiscussionLikeCounts.Add(new DiscussionLikeCount
            {
                DiscussionCommentId = discussionCommentId,
                UserId = currentUser.Id,
                CreatedAt = DateTime.Now.ToLocalTime()
            });

        await _db.SaveChangesAsync();

        // Reload the comment to reflect updated likes
        DiscussionComment? updatedComment = await _db.DiscussionComments
            .Include(c => c.DiscussionLikeCounts)
            .FirstOrDefaultAsync(c => c.Id == discussionCommentId);

        if (Request.IsAjax())
            return PartialView("Comment/_LikeButtonForComment", updatedComment);


        return RedirectToAction("DiscussionPostComment", new { discussionPostId = comment.DiscussionPostId });
    }

    private List<DiscussionComment> GetRepliesRecursive(DiscussionComment parent, List<DiscussionComment> allComments)
    {
        List<DiscussionComment> replies = allComments
            .Where(c => c.ReplyCommentId == parent.Id)
            .OrderBy(c => c.CreatedAt)
            .ToList();

        foreach (DiscussionComment reply in replies) reply.Replies = GetRepliesRecursive(reply, allComments);

        return replies;
    }


    [HttpPost]
    public async Task<IActionResult> AddComment(DiscussionPostVm Vm, string content, int PostId)
    {
        User? currentUser = await User.GetCurrentUser(_db);

        if (currentUser == null)
            return Unauthorized();

        // Check for offensive content using AI
        ContentModerationResult moderationResult = await _contentModerationService.AnalyzeContentAsync(content);
        if (!moderationResult.IsOffensive)
        {
            await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();

            try
            {
                DiscussionComment comment = new()
                {
                    DiscussionPostId = PostId,
                    ReplyCommentId = Vm.ParentCommentId,
                    UserId = currentUser.Id,
                    Status = nameof(DiscussionCommentStatusType.Active),
                    ViewCount = 0,
                    CreatedAt = DateTime.Now.ToLocalTime(),
                    UpdatedAt = DateTime.Now.ToLocalTime()
                };

                _db.DiscussionComments.Add(comment);
                await _db.SaveChangesAsync();

                string error = await ProcessEmbeddedImagesAsync(comment, null, content);

                if (!string.IsNullOrEmpty(error)) throw new Exception(error);

                await transaction.CommitAsync();
                TempData[SweetAlert.Success] = "Comment posted successfully!";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData[SweetAlert.Danger] = ex.Message;
            }

            return RedirectToAction("DiscussionPostComment", new { discussionPostId = PostId });
        }

        TempData[SweetAlert.Danger] =
            $"Your comment contains offensive content and cannot be published. Reason: {moderationResult.Reason}";
        return RedirectToAction("DiscussionPostComment", new { discussionPostId = PostId });
    }

    public async Task<string> ProcessEmbeddedImagesAsync(DiscussionComment? comment,
        DiscussionPost? post, string content)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(content)) throw new Exception("Invalid content");

            Regex imgRegex = new(
                "<img[^>]+src=\"data:image/(?<type>.+?);base64,(?<data>.+?)\"[^>]*>",
                RegexOptions.IgnoreCase);

            MatchCollection matches = imgRegex.Matches(content);

            foreach (Match match in matches)
            {
                string base64Data = match.Groups["data"].Value;
                string fileType = match.Groups["type"].Value.ToLowerInvariant();
                byte[] imageBytes = Convert.FromBase64String(base64Data);

                // Determine prefix and file naming
                string prefix = comment != null ? "comment" : "post";
                int id = comment?.Id ?? post?.Id ?? 0;
                string fileName = $"{prefix}_{id}_{Guid.NewGuid()}.{fileType}";

                // Convert base64 bytes to IFormFile
                using MemoryStream ms = new(imageBytes);
                FormFile formFile = new(ms, 0, ms.Length, fileName, fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = $"image/{fileType}"
                };

                // Upload to Dropbox
                (string? imagePath, string? imageLink, string? error) =
                    await _dropboxService.SaveImageAsync(formFile, Common.DiscussionPath);

                if (!string.IsNullOrEmpty(error))
                    throw new Exception(error);

                // Store attachment record
                DiscussionAttachment attachment = new()
                {
                    DiscussionCommentId = comment?.Id,
                    DiscussionPostId = post?.Id,
                    FilePath = imagePath,
                    FileLink = imageLink,
                    FileType = fileType,
                    CreatedAt = DateTime.Now.ToLocalTime()
                };

                _db.DiscussionAttachments.Add(attachment);
                await _db.SaveChangesAsync();

                // Replace <img> tag with Dropbox URL
                content = content.Replace(match.Value,
                    $"<img src=\"{imageLink}\" class=\"img-fluid\" loading=\"lazy\" />");
            }

            // Update the corresponding record content
            if (comment != null)
            {
                comment.Content = content;
                _db.DiscussionComments.Update(comment);
            }
            else if (post != null)
            {
                post.Content = content;
                _db.DiscussionPosts.Update(post);
            }

            await _db.SaveChangesAsync();

            return string.Empty;
        }
        catch (Exception ex)
        {
            return ex.Message;
        }
    }


    public IActionResult Unauthorized()
    {
        TempData[SweetAlert.Danger] = "You need to login first.";
        return RedirectToAction("Index", "login");
    }
}