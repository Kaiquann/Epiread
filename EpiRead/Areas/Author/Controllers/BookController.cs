using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CreateBookVm = EpiRead.Areas.Author.ViewModels.CreateBookVm;
using EditBookVm = EpiRead.Areas.Author.ViewModels.EditBookVm;

namespace EpiRead.Areas.Author.Controllers;

/// <summary>
///     File: BookController.cs
///     Provides the Author-area MVC controller that enables authors to manage
///     their books. Responsibilities include listing, searching and filtering
///     books, creating and editing metadata, previewing files, managing ebook
///     documents and book cover images, as well as recovering or trashing books.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.AuthorOnly)]
[Area("Author")]
public class BookController : Controller
{
    private readonly IBookCategoryService _bookCategoryService;
    private readonly IBookService _bookService;
    private readonly DB _db;
    private readonly IDropboxService _dropboxService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BookController" /> class.
    ///     This constructor injects required services: database context, category
    ///     service, book service and Dropbox helper for image operations.
    /// </summary>
    /// <param name="db">Database context.</param>
    /// <param name="bookCategoryService">Service for book categories.</param>
    /// <param name="bookService">Service for book operations.</param>
    /// <param name="dropboxService">Dropbox helper service for image storage.</param>
    public BookController(DB db, IBookCategoryService bookCategoryService, IBookService bookService,
        IDropboxService dropboxService)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _bookCategoryService = bookCategoryService ?? throw new ArgumentNullException(nameof(bookCategoryService));
        _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
        // No additional logger required; using LoggerUtilities instead
        _dropboxService = dropboxService ?? throw new ArgumentNullException(nameof(dropboxService));
    }

    /// <summary>
    ///     Displays a paginated list of the current author's available books.
    ///     Supports optional search (title/author/description) and filter by
    ///     category or language. Returns HTML or partial view for AJAX requests.
    /// </summary>
    public async Task<IActionResult> Index(string? q = null, string? filter = null, int page = 1)
    {
        // Starting Index: not logging this; only DB/network operations are logged
        // Fetch current user (DB)
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        // Base query: only show Available books to authors on this page
        IQueryable<Book> booksQuery = _db.Books
            .Include(b => b.BookCategoryMaps)!
            .ThenInclude(m => m.BookCategory)
            .Where(b => b.UserId == currentUser.Id && b.Status == nameof(BookStatusType.Available))
            .OrderByDescending(b => b.UpdatedAt)
            .AsQueryable();

        // Apply search q (title/author/description)
        if (!string.IsNullOrWhiteSpace(q))
            booksQuery = booksQuery.Where(b => b.Title.Contains(q)
                                               || b.AuthorName.Contains(q)
                                               || (b.Description != null && b.Description.Contains(q)));

        // Apply filter: categories (C:Id) or language (L:Code)
        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("C:") && int.TryParse(filter.Substring(2), out int catId))
            {
                booksQuery = booksQuery.Where(b =>
                    b.BookCategoryMaps != null && b.BookCategoryMaps.Any(m => m.BookCategoryId == catId));
            }
            else if (filter.StartsWith("L:") && filter.Length > 2)
            {
                string lang = filter.Substring(2);
                booksQuery = booksQuery.Where(b => b.LanguageCode == lang);
            }
        }

        LoggerUtilities.Info($"[Process] executing books query (DB) for page {page}");
        IPagedList<Book> pagedList = booksQuery.ToPagedList(page, 6);

        // Persist filter/search for pager links and UI
        ViewBag.Q = q;
        ViewBag.Filter = filter;

        // informational: result count is available in view, avoid duplication

        if (Request.IsAjax()) return PartialView("_Index", pagedList);

        // also provide categories and languages for the filter UI (DB)
        LoggerUtilities.Info("[Process] loading categories (DB)");
        ViewBag.BookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();
        // [Index] page {page}
        return View(pagedList);
    }

    /// <summary>
    ///     Shows the current author's trashed books with optional search and
    ///     filter criteria. Supports AJAX partial rendering for pagination.
    /// </summary>
    public async Task<IActionResult> Trash(string? q = null, string? filter = null, int page = 1)
    {
        // Starting Trash: not logging this; only DB/network operations are logged
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        IQueryable<Book> booksQuery = _db.Books
            .Include(b => b.BookCategoryMaps)!
            .ThenInclude(m => m.BookCategory)
            .Where(b => b.UserId == currentUser.Id && b.Status == nameof(BookStatusType.Trashed))
            .OrderByDescending(b => b.UpdatedAt)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
            booksQuery = booksQuery.Where(b => b.Title.Contains(q)
                                               || b.AuthorName.Contains(q)
                                               || (b.Description != null && b.Description.Contains(q)));

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("C:") && int.TryParse(filter.Substring(2), out int catId))
            {
                booksQuery = booksQuery.Where(b =>
                    b.BookCategoryMaps != null && b.BookCategoryMaps.Any(m => m.BookCategoryId == catId));
            }
            else if (filter.StartsWith("L:") && filter.Length > 2)
            {
                string lang = filter.Substring(2);
                booksQuery = booksQuery.Where(b => b.LanguageCode == lang);
            }
        }

        LoggerUtilities.Info($"[Process] executing books query (DB) for page {page}");
        IPagedList<Book> pagedList = booksQuery.ToPagedList(page, 6);

        ViewBag.Q = q;
        ViewBag.Filter = filter;
        ViewBag.BookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        // informational: pagination result returned
        if (Request.IsAjax()) return PartialView("_Index", pagedList);

        ViewBag.Title = "Trashed Books";
        return View("Trash", pagedList);
    }

    /// <summary>
    ///     Returns the create book view for the current author. Prefills
    ///     metadata such as author name and loads categories and languages.
    ///     Returns a partial view for AJAX requests.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        CreateBookVm createBookVm = new()
        {
            AuthorName = $"{currentUser.FirstName} {currentUser.LastName}".Trim()
        };

        LoggerUtilities.Info("[Process] loading categories (DB)");
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.BookCategories = bookCategories;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        // Done: create view returned
        if (Request.IsAjax()) return PartialView("_Create", createBookVm);
        return View(createBookVm);
    }

    /// <summary>
    ///     Handles POST to create a new book. Validates input, uploads the cover
    ///     image (if present), maps categories, and creates associated discussion.
    ///     Returns JSON for AJAX requests, or redirects to the Show page on success.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store(CreateBookVm createBookVm)
    {
        // [Store] incoming request - proceed to DB/network steps
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.BookCategories = bookCategories;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        if (!ModelState.IsValid)
        {
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        // Validate language (if provided)
        if (!LanguageHelper.IsValidLanguage(createBookVm.Language))
        {
            ModelState.AddModelError("Language", "Invalid language!");
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        if (createBookVm.PublicationYear.HasValue)
        {
            bool isValidYear = IsValidYear(createBookVm.PublicationYear.Value, Common.MinPublicationYear);
            if (!isValidYear)
            {
                ModelState.AddModelError("PublicationYear",
                    $"Publication year must be between {Common.MinPublicationYear} and {Common.MaxPublicationYear}.");
                if (Request.IsAjax()) return PartialView("_Create", createBookVm);
                return View("Create", createBookVm);
            }
        }

        // Check for duplicate Title + Author combination
        bool duplicateExists = await _db.Books
            .AnyAsync(b => b.Title.ToLower() == createBookVm.Title.Trim().ToLower()
                           && b.AuthorName.ToLower() == createBookVm.AuthorName.Trim().ToLower());
        if (duplicateExists)
        {
            ModelState.AddModelError("", "A book with this title and author already exists in the system.");
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        // Fetch current user (DB)
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        try
        {
            Book newBook = new()
            {
                Title = createBookVm.Title,
                AuthorName = createBookVm.AuthorName,
                Description = createBookVm.Description,
                PublicationYear = createBookVm.PublicationYear.HasValue
                    ? new DateOnly(createBookVm.PublicationYear.Value, 1, 1)
                    : null,
                LanguageCode = createBookVm.Language,
                CoverImagePath = null,
                UserId = currentUser.Id
            };

            // Cover image handling (network)
            if (createBookVm.CoverImage != null)
            {
                LoggerUtilities.Info("[Process] saving cover image (network)");
                (string? coverImagePath, string? coverImageLink, string? error) =
                    await _dropboxService.SaveImageAsync(createBookVm.CoverImage, Common.ImagePath);
                if (!string.IsNullOrEmpty(error))
                {
                    LoggerUtilities.Error($"[Danger] cover image save error: {error}");
                    throw new Exception(error);
                }

                newBook.CoverImagePath = coverImagePath;
                newBook.CoverImageLink = coverImageLink;
            }

            // Use BookService to create book (handles transaction/DB)
            List<int> uniqueCategoryIds = createBookVm.CategoryIds.Distinct().ToList();

            // Auto-assign General category if no category is selected
            if (uniqueCategoryIds.Count == 0)
            {
                BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                if (generalCategory != null)
                {
                    uniqueCategoryIds = new List<int> { generalCategory.Id };
                    LoggerUtilities.Info("[Process] Auto-assigned General category for new book");
                }
                else
                {
                    LoggerUtilities.Warning(
                        "[Process] General category not found - book will be created without categories");
                    TempData[SweetAlert.Warning] = "Book created without category. Please add a category manually.";
                }
            }

            LoggerUtilities.Info("[Process] calling BookService.CreateBookAsync (DB)");
            (Book? created, string? createError) =
                await _bookService.CreateBookAsync(newBook, uniqueCategoryIds);
            if (!string.IsNullOrEmpty(createError) || created == null)
            {
                LoggerUtilities.Error($"[Danger] CreateBookAsync failed: {createError}");
                throw new Exception(createError ?? "Failed to create book");
            }

            LoggerUtilities.Info($"[Process] book created (DB) id {created.Id}");

            // create discussion for book (DB)
            Discussion discussion = new()
            {
                Title = CreateDiscussionForumTitle(created.Title),
                Description =
                    $"A dedicated forum for in-depth discussion of \"{newBook.Title}\", written by {newBook.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.",
                BookId = created.Id
            };
            _db.Discussions.Add(discussion);
            LoggerUtilities.Info($"[Process] saving discussion (DB) for book id {created.Id}");
            await _db.SaveChangesAsync();

            // book creation complete

            TempData[SweetAlert.Success] = "Book created successfully.";

            if (Request.IsAjax()) return Json(new { redirect = Url.Action("Show", new { id = created.Id }) });

            return RedirectToAction("Show", new { id = created.Id });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] exception while creating book: {ex.Message}");
            TempData[SweetAlert.Danger] = ex.Message;
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }
    }

    /// <summary>
    ///     Shows book details and associated documents for a specific book owned
    ///     by the current author. Returns partial view for AJAX requests.
    /// </summary>
    public async Task<IActionResult> Show(int id)
    {
        // Fetch current user (DB)
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        LoggerUtilities.Info($"[Process] fetching book (DB) id: {id}");
        Book? book = await _bookService.GetBookByIdAsync(id);
        if (book == null) return NotFound();

        // allow if owner or admin/author higher
        if (book.UserId != currentUser.Id) return Forbid();

        LoggerUtilities.Info($"[Process] loading documents (DB) for book id: {id}");
        List<BookDocument> bookDocuments = await _db.BookDocuments.Where(d => d.BookId == id).ToListAsync();
        ViewBag.BookDocuments = bookDocuments;

        // End: book details loaded

        if (Request.IsAjax()) return PartialView("_Show", book);
        return View(book);
    }

    // Author: Edit ebook files (GET)
    /// <summary>
    ///     Returns the UI to list, rename, delete or upload ebook documents for
    ///     a given book. Ensures current user is the owner and loads existing files.
    /// </summary>
    public async Task<IActionResult> EditEbook(int id)
    {
        // beginning EditEbook GET: only DB/network operations will be logged
        // Fetch current user (DB)
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        Book? book = await _db.Books.FindAsync(id);
        if (book == null)
        {
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

        // Ensure owner
        if (book.UserId != currentUser.Id) return Forbid();

        LoggerUtilities.Info("[Process] loading ebook documents (DB)");
        List<BookDocument> existingEbooks = await _db.BookDocuments.Where(d => d.BookId == id).ToListAsync();

        EditEbookVm editEbookVm = new()
        {
            BookId = id,
            Book = book,
            ExistingEbooks = existingEbooks
        };

        if (Request.IsAjax()) return PartialView("_EditEbook", editEbookVm);
        // end EditEbook GET
        return View(editEbookVm);
    }

    // Author: Edit book details (GET)
    /// <summary>
    ///     Returns the edit form for a book's metadata and categories. Loads
    ///     available categories and languages, and ensures the current user is
    ///     the owner of the book.
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        // Begin Edit GET: only log DB/network operations below
        LoggerUtilities.Info("[Process] fetching current user (DB)");
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        Book? book = await _bookService.GetBookByIdAsync(id);
        if (book == null)
        {
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

        // Ensure owner (or admin)
        if (book.UserId != currentUser.Id) return Forbid();

        EditBookVm editBookVm = new()
        {
            Id = book.Id,
            Title = book.Title,
            AuthorName = book.AuthorName,
            Description = book.Description,
            PublicationYear = book.PublicationYear?.Year,
            Language = book.LanguageCode,
            CoverImagePath = book.CoverImagePath,
            CoverImageLink = book.CoverImageLink,
            Status = book.Status,
            CategoryIds = book.BookCategoryMaps?.Select(bcm => bcm.BookCategoryId).ToList() ?? []
        };

        ViewBag.BookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        // Edit GET complete

        if (Request.IsAjax()) return PartialView("_Edit", editBookVm);
        ViewBag.Title = "Edit Book : " + editBookVm.Title;
        return View(editBookVm);
    }

    /// <summary>
    ///     Prepares or downloads an EPUB file for preview and sets the web
    ///     accessible URL used by the preview page. Returns a redirect on error.
    /// </summary>
    public async Task<IActionResult> EpubPreview(int id)
    {
        // Begin PdfPreview: only log DB/network operations below
        LoggerUtilities.Info($"[Process] loading BookDocument (DB) id {id}");
        BookDocument? bookDocument = _db.BookDocuments.FirstOrDefault(d => d.Id == id);
        if (bookDocument == null || Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant() != ".epub")
        {
            TempData["Error"] = "EPUB file not found.";
            return RedirectToAction("Index");
        }

        // Use BookService to get or download the EPUB file for preview
        LoggerUtilities.Info("[Process] requesting local epub file (may trigger download)");
        (string? localFilePath, string? error) = await _bookService.GetOrDownloadEpubForPreviewAsync(bookDocument);

        if (!string.IsNullOrEmpty(error))
        {
            LoggerUtilities.Error($"[Danger] error fetching epub: {error}");
            TempData["Error"] = error;
            return RedirectToAction("Index");
        }

        // Convert local file path to web-accessible URL
        string fileName = Path.GetFileName(localFilePath!);
        string fileUrl = Url.Content($"~/uploads/ebooks/{fileName}");
        ViewBag.EpubFileUrl = fileUrl;

        LoggerUtilities.Info($"[End] file prepared {fileName}");

        return View();
    }

    /// <summary>
    ///     Prepares or downloads a PDF file for preview and sets the web
    ///     accessible URL used by the preview page. Returns a redirect on error.
    /// </summary>
    public async Task<IActionResult> PdfPreview(int id)
    {
        // Begin PdfPreview: only DB/network operations logged below
        LoggerUtilities.Info($"[Process] loading BookDocument (DB) id {id}");
        BookDocument? bookDocument = _db.BookDocuments.FirstOrDefault(d => d.Id == id);
        if (bookDocument == null || Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant() != ".pdf")
        {
            TempData["Error"] = "PDF file not found.";
            return RedirectToAction("Index");
        }

        // Use BookService to get or download the PDF file for preview
        LoggerUtilities.Info("[Process] requesting local pdf file (may trigger download)");
        (string? localFilePath, string? error) = await _bookService.GetOrDownloadPdfForPreviewAsync(bookDocument);

        if (!string.IsNullOrEmpty(error))
        {
            LoggerUtilities.Error($"[Danger] error fetching pdf: {error}");
            TempData["Error"] = error;
            return RedirectToAction("Index");
        }

        // Convert local file path to web-accessible URL
        string fileName = Path.GetFileName(localFilePath!);
        string fileUrl = Url.Content($"~/uploads/ebooks/{fileName}");
        ViewBag.PdfFileUrl = fileUrl;

        LoggerUtilities.Info($"[End] file prepared {fileName}");

        return View();
    }

    /// <summary>
    ///     Updates a book's metadata, categories and optionally the cover image.
    ///     Performs ownership validation and persists changes to the database.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Update(EditBookVm editBookVm)
    {
        // Begin Update: only DB/network operations logged below
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        try
        {
            if (editBookVm == null || !ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            // Validate language (if provided)
            if (!LanguageHelper.IsValidLanguage(editBookVm.Language))
            {
                LoggerUtilities.Warning("[Process] Invalid language - " + editBookVm.Language);
                throw new Exception("Invalid language!");
            }

            if (editBookVm.PublicationYear.HasValue)
            {
                bool isValidYear = IsValidYear(editBookVm.PublicationYear.Value, Common.MinPublicationYear);
                if (!isValidYear)
                    throw new Exception(
                        $"Publication year must be between {Common.MinPublicationYear} and {Common.MaxPublicationYear}.");
            }

            // Check for duplicate Title + Author combination (exclude current book)
            bool duplicateExists = await _db.Books
                .AnyAsync(b => b.Title.ToLower() == editBookVm.Title.Trim().ToLower()
                               && b.AuthorName.ToLower() == editBookVm.AuthorName.Trim().ToLower()
                               && b.Id != editBookVm.Id);
            if (duplicateExists)
                throw new Exception("A book with this title and author already exists in the system.");

            LoggerUtilities.Info($"[Process] fetching book (DB) id: {editBookVm.Id}");
            Book? book = await _db.Books.Include(b => b.BookCategoryMaps)
                .FirstOrDefaultAsync(b => b.Id == editBookVm.Id);
            if (book == null) throw new Exception("Book not found.");

            // Ownership check
            if (book.UserId != currentUser.Id) return Forbid();

            // Update fields
            book.Title = editBookVm.Title;
            book.AuthorName = editBookVm.AuthorName;
            book.Description = editBookVm.Description;
            book.PublicationYear = editBookVm.PublicationYear.HasValue
                ? new DateOnly(editBookVm.PublicationYear.Value, 1, 1)
                : null;
            book.LanguageCode = editBookVm.Language;
            book.UpdatedAt = DateTime.Now.ToLocalTime();

            // Handle cover image (network)
            if (editBookVm.CoverImage != null)
            {
                LoggerUtilities.Info($"[Process] saving new cover image (network) for book id {book.Id}");
                (string? coverImagePath, string? coverImageLink, string? error) =
                    await _dropboxService.SaveImageAsync(editBookVm.CoverImage, Common.ImagePath);
                if (!string.IsNullOrEmpty(error))
                {
                    LoggerUtilities.Error($"[Danger] save cover image error: {error}");
                    throw new Exception(error);
                }

                // Preserve the old path before setting the new one so we delete the correct file
                string? oldCoverImagePath = book.CoverImagePath;

                book.CoverImagePath = coverImagePath;
                book.CoverImageLink = coverImageLink;

                // Delete old image from Dropbox if it exists. Do not block update on failure to delete
                if (!string.IsNullOrWhiteSpace(oldCoverImagePath))
                {
                    LoggerUtilities.Info($"[Process] deleting old cover image (network) for book id {book.Id}");
                    bool deleted = await _dropboxService.DeleteImageAsync(oldCoverImagePath);
                    if (!deleted)
                        LoggerUtilities.Warning($"[Process] failed to delete old cover image for book id {book.Id}");
                }
            }

            // Update categories
            // Ensure the navigation collection is initialized to avoid null dereference warnings
            if (book.BookCategoryMaps == null) book.BookCategoryMaps = new List<BookCategoryMap>();

            List<BookCategoryMap> existingCategoryMaps = book.BookCategoryMaps.ToList();
            List<int> existingCategoryIds = existingCategoryMaps.Select(bcm => bcm.BookCategoryId).ToList();
            List<int> newCategoryIds = editBookVm.CategoryIds;

            // Auto-assign General category if no category is selected
            if (newCategoryIds.Count == 0)
            {
                BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                if (generalCategory != null)
                {
                    newCategoryIds = new List<int> { generalCategory.Id };
                    LoggerUtilities.Info($"[Process] Auto-assigned General category for book id {book.Id}");
                }
                else
                {
                    LoggerUtilities.Warning(
                        $"[Process] General category not found for book id {book.Id} - keeping existing categories");
                    // Keep existing categories if General category doesn't exist
                    newCategoryIds = existingCategoryIds;
                }
            }

            List<int> categoriesToAdd = newCategoryIds.Except(existingCategoryIds).ToList();
            foreach (int categoryId in categoriesToAdd)
                book.BookCategoryMaps.Add(new BookCategoryMap { BookId = book.Id, BookCategoryId = categoryId });

            List<BookCategoryMap> categoriesToRemove =
                existingCategoryMaps.Where(bcm => !newCategoryIds.Contains(bcm.BookCategoryId)).ToList();
            if (categoriesToRemove.Count > 0)
            {
                LoggerUtilities.Info(
                    $"[Process] removing {categoriesToRemove.Count} category map(s) (DB) for book id {book.Id}");
                _db.BookCategoryMaps.RemoveRange(categoriesToRemove);
            }

            // Update related Discussion forum title and description when book is renamed
            LoggerUtilities.Info($"[Process] fetching discussion (DB) for book id {book.Id}");
            Discussion? discussion = await _db.Discussions
                .FirstOrDefaultAsync(d => d.BookId == book.Id);
            if (discussion != null)
            {
                discussion.Title = CreateDiscussionForumTitle(book.Title);
                discussion.Description =
                    $"A dedicated forum for in-depth discussion of \"{book.Title}\", written by {book.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.";
                discussion.UpdatedAt = DateTime.Now.ToLocalTime();
                _db.Discussions.Update(discussion);
                LoggerUtilities.Info($"[Process] updated discussion forum for book id {book.Id}");
            }

            _db.Books.Update(book);
            LoggerUtilities.Info($"[Process] saving changes (DB) for book id {book.Id}");
            await _db.SaveChangesAsync();
            // saved changes (DB)

            TempData[SweetAlert.Success] = "Book updated successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] error: {ex.Message}");
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Deletes the book cover image from remote storage (Dropbox) and clears
    ///     the cover-related fields on the book record.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemoveCoverImage(int id)
    {
        try
        {
            // Begin RemoveCoverImage: only network operations will be logged
            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            // Find the pass in book id
            Book? book = await _db.Books.FindAsync(id);
            if (book == null) throw new Exception("Book not found!");

            LoggerUtilities.Info($"[Process] deleting cover image (network) for book id {id}");
            bool success = await _dropboxService.DeleteImageAsync(book.CoverImagePath);

            // If deletion fails (e.g., file not found or Dropbox error), treat as non-fatal.
            // We still want the DB to reflect that the book has no cover image. Log the
            // failure and show a warning to the admin, but do not abort the request.
            if (!success)
            {
                LoggerUtilities.Warning($"[Danger] failed to delete cover image for bookId={id}");
                TempData[SweetAlert.Warning] =
                    "Cover was removed from this book, but we couldn't delete the image file from Dropbox (it may already have been removed).";
            }

            // Update book record
            book.CoverImagePath = null;
            book.CoverImageLink = null;
            await _db.SaveChangesAsync();
            LoggerUtilities.Info($"[End] succeeded for bookId={id}");
            TempData[SweetAlert.Success] = "Book cover image removed successfully";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] error for id={id}: {ex.Message}");
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Handles POST actions for ebook file management: deleting selected
    ///     documents, renaming files, and uploading new ebook files for a book.
    ///     Also cleans up related records such as bookmarks and history.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditEbook(int bookId, EditEbookVm editEbookVm)
    {
        // Begin EditEbook POST: only DB/network ops are logged
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        Book? book = await _db.Books.FindAsync(bookId);
        if (book == null)
        {
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

        // Ownership check
        if (book.UserId != currentUser.Id) return Forbid();

        try
        {
            // Handle deletions
            if (editEbookVm.EbooksToDelete != null && editEbookVm.EbooksToDelete.Count > 0)
                foreach (int docId in editEbookVm.EbooksToDelete)
                {
                    BookDocument? bookDocument =
                        _db.BookDocuments.FirstOrDefault(d => d.Id == docId && d.BookId == bookId);
                    if (bookDocument != null)
                    {
                        LoggerUtilities.Info($"[Process] deleting bookDocument id {docId} (DB + network)");
                        // Delete related BookHistory records first
                        List<BookHistory> bookHistoriesToDelete =
                            await _db.BookHistories.Where(bh => bh.BookDocumentId == docId).ToListAsync();
                        if (bookHistoriesToDelete.Count > 0)
                        {
                            LoggerUtilities.Info(
                                $"[Process] removing {bookHistoriesToDelete.Count} BookHistory record(s) (DB) for document id {docId}");
                            _db.BookHistories.RemoveRange(bookHistoriesToDelete);
                        }

                        // Delete related Bookmark records
                        List<Bookmark> bookmarksToDelete =
                            await _db.Bookmarks.Where(bm => bm.BookDocumentId == docId).ToListAsync();
                        if (bookmarksToDelete.Count > 0)
                        {
                            LoggerUtilities.Info(
                                $"[Process] removing {bookmarksToDelete.Count} Bookmark record(s) (DB) for document id {docId}");
                            _db.Bookmarks.RemoveRange(bookmarksToDelete);
                        }

                        // Delete using BookService (handles external storage)
                        LoggerUtilities.Info($"[Process] deleting remote document (network) for document id {docId}");
                        await _bookService.DeleteBookDocumentAsync(bookDocument);
                        _db.BookDocuments.Remove(bookDocument);
                    }
                }

            // Handle name updates (rename file)
            if (editEbookVm.UpdatedNames != null && editEbookVm.UpdatedNames.Count > 0)
                foreach (KeyValuePair<int, string> kv in editEbookVm.UpdatedNames)
                {
                    int docId = kv.Key;
                    string newName = kv.Value.Trim();
                    if (string.IsNullOrEmpty(newName)) continue;

                    BookDocument? bookDocument =
                        _db.BookDocuments.FirstOrDefault(d => d.Id == docId && d.BookId == bookId);
                    if (bookDocument != null)
                    {
                        LoggerUtilities.Info($"[Process] renaming document id {docId} (network)");
                        (BookDocument? updatedDocument, string? error) =
                            await _bookService.UpdateBookDocumentNameAsync(bookDocument, newName, bookId);
                        if (updatedDocument != null)
                            await _db.SaveChangesAsync();
                        else
                            ModelState.AddModelError("", $"Failed to rename file to '{newName}': {error}");
                    }
                }

            // Handle new files
            List<BookDocument> newDocuments = [];
            if (editEbookVm.NewEbookFiles != null && editEbookVm.NewEbookFiles.Count > 0)
                for (int i = 0; i < editEbookVm.NewEbookFiles.Count; i++)
                {
                    IFormFile file = editEbookVm.NewEbookFiles[i];
                    string customName = editEbookVm.NewCustomNames != null && editEbookVm.NewCustomNames.Count > i
                        ? editEbookVm.NewCustomNames[i].Trim()
                        : Path.GetFileNameWithoutExtension(file.FileName);

                    LoggerUtilities.Info($"[Process] uploading new ebook file (network) for book id {bookId}");
                    (List<BookDocument> documents, List<string> warnings) =
                        await _bookService.ProcessNewEbookUploadAsync(file, bookId, customName);
                    newDocuments.AddRange(documents);
                    foreach (string warning in warnings) TempData[SweetAlert.Warning] = warning;
                }

            if (newDocuments.Count > 0) _db.BookDocuments.AddRange(newDocuments);

            await _db.SaveChangesAsync();

            TempData[SweetAlert.Success] = "Ebook files updated successfully.";
            LoggerUtilities.Info($"[End] bookId: {bookId}");
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] exception: {ex.Message}");
            TempData[SweetAlert.Danger] = $"Error updating ebook files: {ex.Message}";
        }

        // Reload updated data
        List<BookDocument> existingEbooks = await _db.BookDocuments.Where(d => d.BookId == bookId).ToListAsync();
        EditEbookVm updatedVm = new()
        {
            BookId = bookId,
            Book = book,
            ExistingEbooks = existingEbooks
        };

        if (Request.IsAjax()) return PartialView("_EditEbook", updatedVm);
        // After non-AJAX submit, take the user back to the book Show page (details) instead of staying on EditEbook
        return RedirectToAction("Show", new { id = bookId });
    }

    /// <summary>
    ///     Generates a book description using the BookService—typically an AI
    ///     service—and returns JSON containing the generated text or an error.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GenerateDescription(int id)
    {
        try
        {
            // Begin GenerateDescription: only network operation logging below
            if (!ModelState.IsValid || id <= 0)
                return Json(new { success = false, message = "Invalid book ID." });

            LoggerUtilities.Info("[Process] calling BookService.GenerateDescriptionAsync (network)");
            // Use BookService to generate description
            (string? generatedDescription, string? error) = await _bookService.GenerateDescriptionAsync(id);

            if (!string.IsNullOrEmpty(error))
            {
                LoggerUtilities.Error($"GenerateDescription - service error: {error}");
                return Json(new { success = false, message = error });
            }

            LoggerUtilities.Info($"[End] id {id}");
            return Json(new { success = true, description = generatedDescription });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] error for id: {id}: {ex.Message}");
            return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Recovers one or more trashed books for the current author and updates
    ///     their status to available. Returns the refreshed partial view for AJAX.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Recover(int[] ids)
    {
        // Begin Recover: only DB/network operations are logged
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        try
        {
            if (ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            // Ensure the books belong to the current user
            LoggerUtilities.Info("[Process] querying owned books (DB)");
            List<int> ownerBookIds = await _db.Books
                .Where(b => ids.Contains(b.Id) && b.UserId == currentUser.Id)
                .Select(b => b.Id)
                .ToListAsync();

            if (ownerBookIds.Count == 0)
                throw new Exception("No books found to recover or you do not have permission.");

            LoggerUtilities.Info("[Process] calling BookService.RecoverBooksAsync (DB/network)");
            (bool success, string? error) = await _bookService.RecoverBooksAsync(ownerBookIds.ToArray());
            if (!success) throw new Exception(error ?? "Failed to recover books.");

            TempData[SweetAlert.Success] = $"{ownerBookIds.Count} book(s) recovered successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] error: {ex.Message}");
            TempData[SweetAlert.Danger] = ex.Message;
        }

        // For AJAX requests return the updated trashed list partial so the UI can refresh in place
        if (Request.IsAjax())
        {
            IQueryable<Book> booksQuery = _db.Books
                .Include(b => b.BookCategoryMaps)!
                .ThenInclude(m => m.BookCategory)
                .Where(b => b.UserId == currentUser.Id && b.Status == nameof(BookStatusType.Trashed))
                .OrderByDescending(b => b.UpdatedAt)
                .AsQueryable();

            IPagedList<Book> pagedList = booksQuery.ToPagedList(1, 6);
            return PartialView("_Index", pagedList);
        }

        return RedirectToAction("Trash");
    }

    /// <summary>
    ///     Moves one or more owned books to the trash status. Validates ownership
    ///     and returns the updated partial for AJAX or a redirect to the list.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Trash(int[] ids)
    {
        // Begin Trash: only DB/network operations are logged
        User? currentUser = await User.GetCurrentUser(_db);
        if (currentUser == null) return Unauthorized();

        try
        {
            if (ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            // Ensure the books belong to the current user
            LoggerUtilities.Info("[Process] querying owned books (DB)");
            List<int> ownerBookIds = await _db.Books
                .Where(b => ids.Contains(b.Id) && b.UserId == currentUser.Id)
                .Select(b => b.Id)
                .ToListAsync();

            if (ownerBookIds.Count == 0)
                throw new Exception("No books found to move to trash or you do not have permission.");

            LoggerUtilities.Info("[Process] calling BookService.TrashBooksAsync (DB/network)");
            (bool success, string? error) = await _bookService.TrashBooksAsync(ownerBookIds.ToArray());
            if (!success) throw new Exception(error ?? "Failed to move books to trash.");

            TempData[SweetAlert.Success] = $"{ownerBookIds.Count} book(s) moved to trash successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] error: {ex.Message}");
            TempData[SweetAlert.Danger] = ex.Message;
        }

        // For AJAX requests return the updated available list partial so the UI can refresh in place
        if (Request.IsAjax())
        {
            IQueryable<Book> booksQuery = _db.Books
                .Include(b => b.BookCategoryMaps)!
                .ThenInclude(m => m.BookCategory)
                .Where(b => b.UserId == currentUser.Id && b.Status == nameof(BookStatusType.Available))
                .OrderByDescending(b => b.UpdatedAt)
                .AsQueryable();

            IPagedList<Book> pagedList = booksQuery.ToPagedList(1, 6);
            return PartialView("_Index", pagedList);
        }

        return RedirectToAction("Index");
    }

    /// <summary>
    ///     Creates a discussion forum title from a book title, ensuring it doesn't exceed
    ///     the Discussion.Title MaxLength constraint of 100 characters.
    /// </summary>
    /// <param name="bookTitle">The book title to use in the discussion forum title.</param>
    /// <returns>A discussion forum title that fits within the 100 character limit.</returns>
    private static string CreateDiscussionForumTitle(string bookTitle)
    {
        const string suffix = " Discussion Forum";
        const int maxDiscussionTitleLength = 100;
        int maxBookTitleLength = maxDiscussionTitleLength - suffix.Length; // 83 characters

        string discussionTitle = $"{bookTitle}{suffix}";
        if (discussionTitle.Length > maxDiscussionTitleLength)
        {
            // Truncate book title to fit within limit
            string truncatedBookTitle = bookTitle.Length > maxBookTitleLength
                ? bookTitle.Substring(0, maxBookTitleLength)
                : bookTitle;
            discussionTitle = $"{truncatedBookTitle}{suffix}";
        }

        return discussionTitle;
    }
}