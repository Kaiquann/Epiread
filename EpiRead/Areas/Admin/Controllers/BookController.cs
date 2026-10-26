using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.TypeConversion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EpiRead.Areas.Admin.Controllers;

/// <summary>
///     File: BookController.cs
///     Controller responsible for the administration of books in the Admin area.
///     This controller provides actions to list, filter, create, update, trash,
///     delete, import, and export books. It also manages book documents for
///     ebook previews, cover images and category mappings. Business rules are
///     delegated to application services like <see cref="IBookService" /> and
///     <see cref="IBookCategoryService" /> to keep controllers thin and focused on
///     request/response handling.
/// </summary>
/// <author>Chong Jun Xiang</author>
[Authorize(Policy = AuthorizationPolicies.RequireAdminOrHigher)]
[Area("Admin")]
public class BookController : Controller
{
    private const int ChunkSize = 5 * 1024 * 1024; // 5MB per chunk (compatible with most proxies)

    // Static dictionary to store chunk upload sessions (in production, consider using distributed cache)
    private static readonly Dictionary<string, ChunkUploadSession> _chunkSessions = new();
    private static readonly object _chunkSessionLock = new();
    private readonly IBookCategoryService _bookCategoryService;
    private readonly IBookService _bookService;
    private readonly DB _db;
    private readonly IFileStorageHelper _dropboxHelper;
    private readonly IDropboxService _dropboxService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="BookController" /> class.
    ///     The controller requires the application's database context and the
    ///     domain services used to manage books, categories and Dropbox file storage.
    /// </summary>
    public BookController(DB db, IBookCategoryService bookCategoryService, IBookService bookService,
        ILogger<BookController> logger, IDropboxService dropboxService, IFileStorageHelper dropboxHelper)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _bookCategoryService = bookCategoryService ?? throw new ArgumentNullException(nameof(bookCategoryService));
        _bookService = bookService ?? throw new ArgumentNullException(nameof(bookService));
        _dropboxService = dropboxService ?? throw new ArgumentNullException(nameof(dropboxService));
        _dropboxHelper = dropboxHelper ?? throw new ArgumentNullException(nameof(dropboxHelper));
    }

    /// <summary>
    ///     Display a paginated list of available books. Supports optional query
    ///     parameters for searching, sorting and filtering by category or language.
    ///     Results are returned as a view or a partial view for AJAX requests.
    /// </summary>
    public async Task<IActionResult> Index(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        // Fetch categories (DB op)
        LoggerUtilities.Info("[Start] fetching book categories for page=" + page);
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        LoggerUtilities.Info("[Process] fetched " + (bookCategories?.Count ?? 0) + " book categories");
        SelectList bookCategorySelectList = new(bookCategories, "Id", "Name");
        ViewBag.BookCategories = bookCategorySelectList;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        // Parse filter: C:5 for category, L:en for language
        int filterId = 0;
        string? languageCode = null;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("C:") && int.TryParse(filter.Substring(2), out filterId))
            {
                // Category filter
            }
            else if (filter.StartsWith("L:"))
            {
                languageCode = filter.Substring(2);
            }
        }

        // Use BookService to get filtered books (DB/service op)
        LoggerUtilities.Info("[Process] fetching filtered books. search='" + search + "', filterId=" + filterId +
                             ", lang=" + languageCode);
        List<Book> searched = await _bookService.GetFilteredBooksAsync(
            search, filterId, languageCode, nameof(BookStatusType.Available));
        LoggerUtilities.Info("[Process] fetched " + (searched?.Count ?? 0) + " books");

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        IPagedList<Book> model = SortAndPaginateBooks(searched ?? new List<Book>(), sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax()) return PartialView("_Index", model);
        LoggerUtilities.Info("[End] Completed page=" + page + ", items=" + model.TotalItemCount);
        return View(model);
    }

    /// <summary>
    ///     Display a paginated list of trashed items. Supports search, sorting and
    ///     filter options similar to <see cref="Index" />, but returns only
    ///     books with status set to trashed.
    /// </summary>
    public async Task<IActionResult> Trashed(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        // Fetch categories (DB/service op)
        LoggerUtilities.Info("[Start] fetching categories for view. search=" + search + ", filter=" + filter +
                             ", page=" + page);
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        SelectList bookCategorySelectList = new(bookCategories, "Id", "Name");
        ViewBag.BookCategories = bookCategorySelectList;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        // Parse filter: C:5 for category, L:en for language
        int filterId = 0;
        string? languageCode = null;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("C:") && int.TryParse(filter.Substring(2), out filterId))
            {
                // Category filter
            }
            else if (filter.StartsWith("L:"))
            {
                languageCode = filter.Substring(2);
            }
        }

        LoggerUtilities.Info("[Process] fetching filtered trashed books. search='" + search + "', filterId=" +
                             filterId + ", lang=" + languageCode);
        List<Book> searched =
            await _bookService.GetFilteredBooksAsync(search, filterId, languageCode, nameof(BookStatusType.Trashed));
        LoggerUtilities.Info("[Process] fetched " + (searched?.Count ?? 0) + " books");

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        IPagedList<Book> model = SortAndPaginateBooks(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax()) return PartialView("_Index", model);
        LoggerUtilities.Info("[End] Completed page=" + page + ", items=" + model.TotalItemCount);
        return View(model);
    }

    /// <summary>
    ///     Display the create-book form. View model data such as book categories
    ///     and language options are prepared for display in the create UI.
    /// </summary>
    public async Task<IActionResult> Create()
    {
        // Fetch categories for the create view (DB/service op)
        LoggerUtilities.Info("[Start] fetching categories for view.");
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();

        ViewBag.BookCategories = bookCategories;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();
        LoggerUtilities.Info("[End] Prepared create view with " + (bookCategories?.Count ?? 0) + " categories");
        return View();
    }

    /// <summary>
    ///     Handle book creation POST requests. Validates the input, uploads a
    ///     cover image to Dropbox if provided, creates category mappings and
    ///     an associated discussion forum, and persists the new book to the
    ///     database inside a transaction.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Store(CreateBookVm createBookVm)
    {
        // Store: will log around DB and network operations (Dropbox upload, SaveChanges)
        LoggerUtilities.Info("[Start] Start storing new book: Title=" + createBookVm?.Title);
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        ViewBag.BookCategories = bookCategories;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        if (createBookVm == null || !ModelState.IsValid)
        {
            LoggerUtilities.Warning("[Process] validation failed. ModelState invalid.");
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        // Validate language (if provided)
        if (!LanguageHelper.IsValidLanguage(createBookVm.Language))
        {
            LoggerUtilities.Warning("[Process] Invalid language - " + createBookVm.Language);
            ModelState.AddModelError("Language", "Invalid language!");
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        // must not be less than 1950 and not greater than current year
        if (createBookVm.PublicationYear.HasValue)
        {
            bool isValidYear = IsValidYear(createBookVm.PublicationYear.Value, Common.MinPublicationYear);
            if (!isValidYear)
            {
                ModelState.AddModelError("PublicationYear",
                    $"Publication year must be between {Common.MinPublicationYear} and {Common.MaxPublicationYear}.");
                LoggerUtilities.Warning("[Process] validation failed. Invalid PublicationYear=" +
                                        createBookVm.PublicationYear + ".");
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
            LoggerUtilities.Warning("[Process] validation failed. Duplicate Title+Author: Title=" +
                                    createBookVm.Title + ", Author=" + createBookVm.AuthorName);
            TempData[SweetAlert.Danger] = "A book with this title and author already exists in the system.";
            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }

        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Map ViewModel to Book entity
            Book newBook = new()
            {
                Title = createBookVm.Title,
                AuthorName = createBookVm.AuthorName,
                Description = createBookVm.Description,
                PublicationYear = createBookVm.PublicationYear.HasValue
                    ? new DateOnly(createBookVm.PublicationYear.Value, 1, 1)
                    : null,
                LanguageCode = createBookVm.Language,
                CoverImagePath = null // Will be set after upload
            };

            // Handle CoverImage upload to Dropbox
            if (createBookVm.CoverImage != null)
            {
                (string? coverImagePath, string? coverImageLink, string? error) =
                    await _dropboxService.SaveImageAsync(createBookVm.CoverImage, Common.ImagePath);

                if (!string.IsNullOrEmpty(error)) throw new Exception(error);

                newBook.CoverImagePath = coverImagePath;
                newBook.CoverImageLink = coverImageLink;
            }

            // Handle categories - auto-assign General category if none selected
            if (createBookVm.CategoryIds.Count > 0)
            {
                newBook.BookCategoryMaps = createBookVm.CategoryIds
                    .Select(bookCategoryId => new BookCategoryMap { BookCategoryId = bookCategoryId })
                    .ToList();
            }
            else
            {
                // Auto-assign General category when no category is selected
                BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                if (generalCategory != null)
                {
                    newBook.BookCategoryMaps = new List<BookCategoryMap>
                    {
                        new() { BookCategoryId = generalCategory.Id }
                    };
                    LoggerUtilities.Info("[Process] Auto-assigned General category for new book");
                }
                else
                {
                    LoggerUtilities.Warning(
                        "[Process] General category not found - book will be created without categories");
                    TempData[SweetAlert.Warning] = "Book created without category. Please add a category manually.";
                }
            }

            _db.Books.Add(newBook);
            LoggerUtilities.Info("[Process] saving new book to database (SaveChanges)");
            await _db.SaveChangesAsync();

            // Auto create a discussion forum for the book
            Discussion discussion = new()
            {
                Title = CreateDiscussionForumTitle(newBook.Title),
                Description =
                    $"A dedicated forum for in-depth discussion of \"{newBook.Title}\", written by {newBook.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.",
                BookId = newBook.Id
            };
            _db.Discussions.Add(discussion);

            LoggerUtilities.Info("[Process] saving discussion and committing transaction");
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData[SweetAlert.Success] = "Book created successfully. You can now manage ebook files.";

            // Handle AJAX request with JSON redirect
            if (Request.IsAjax())
            {
                LoggerUtilities.Info("[End] succeeded (AJAX). newBookId=" + newBook.Id);
                return Json(new { redirect = Url.Action("Show", new { id = newBook.Id }) });
            }

            LoggerUtilities.Info("[End] succeeded. newBookId=" + newBook.Id);
            return RedirectToAction("Show", new { id = newBook.Id });
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            LoggerUtilities.Error(ex,
                "[End] failed. Title=" + createBookVm?.Title + ", Author=" + createBookVm?.AuthorName);
            TempData[SweetAlert.Danger] = ex.Message;

            if (Request.IsAjax()) return PartialView("_Create", createBookVm);
            return View("Create", createBookVm);
        }
        finally
        {
            await _db.DisposeAsync();
        }
    }

    /// <summary>
    ///     Display details for a single book by id. Includes a list of related
    ///     book documents for the viewer to manage or preview.
    /// </summary>
    public async Task<IActionResult> Show(int id)
    {
        if (!ModelState.IsValid)
        {
            LoggerUtilities.Warning("[Process] invalid request. id=" + id);
            TempData[SweetAlert.Danger] = "Invalid request.";
            return View();
        }

        // Use BookService to get book by ID (DB/service op)
        LoggerUtilities.Info("[Start] fetching book by id=" + id);
        Book? book = await _bookService.GetBookByIdAsync(id);

        if (book == null)
        {
            LoggerUtilities.Warning("[Process] book not found. id=" + id);
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

        // Get BookDocuments for this book (DB op)
        LoggerUtilities.Info("[Process] fetching book documents for bookId=" + id);
        List<BookDocument> bookDocuments = await _db.BookDocuments
            .Where(d => d.BookId == id)
            .ToListAsync();
        ViewBag.BookDocuments = bookDocuments;

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial view for id=" + id);
            return PartialView("_Show", book);
        }

        LoggerUtilities.Info("[End] Completed for id=" + id);
        return View(book);
    }

    /// <summary>
    ///     Display the edit form for a book. The existing book entity and its
    ///     category mappings are loaded and mapped to the edit view model.
    /// </summary>
    public async Task<IActionResult> Edit(int id)
    {
        if (!ModelState.IsValid)
        {
            TempData[SweetAlert.Danger] = "Invalid request.";
            return View();
        }

        // Use BookService to get book by ID (DB/service op)
        LoggerUtilities.Info("[Start] fetching book and categories for id=" + id);
        Book? book = await _bookService.GetBookByIdAsync(id);

        if (book == null)
        {
            LoggerUtilities.Warning("[Process] book not found. id=" + id);
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

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
            CategoryIds = book.BookCategoryMaps!.Select(bcm => bcm.BookCategoryId).ToList()
        };

        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();

        ViewBag.BookCategories = bookCategories;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] returning partial edit view for id=" + id);
            return PartialView("_Edit", editBookVm);
        }

        LoggerUtilities.Info("[End] Prepared edit view for id=" + id);
        return View(editBookVm);
    }

    /// <summary>
    ///     Prepare an EPUB file to be previewed in the browser.
    ///     Validates the requested document and obtains a local copy using
    ///     the <see cref="IBookService" />, then exposes a web-accessible URL.
    /// </summary>
    public async Task<IActionResult> EpubPreview(int id)
    {
        // Use BookService to get or download the EPUB file for preview (network op)
        LoggerUtilities.Info("[Start] retrieving EPUB for documentId=" + id);
        BookDocument? bookDocument = _db.BookDocuments.FirstOrDefault(d => d.Id == id);
        if (bookDocument == null || Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant() != ".epub")
        {
            LoggerUtilities.Warning("[Process] file not found or invalid extension. documentId=" + id);
            TempData["Error"] = "EPUB file not found.";
            return RedirectToAction("Index");
        }

        // Use BookService to get or download the EPUB file for preview
        (string? localFilePath, string? error) = await _bookService.GetOrDownloadEpubForPreviewAsync(bookDocument);

        if (!string.IsNullOrEmpty(error))
        {
            LoggerUtilities.Warning("[Process] error while retrieving file. documentId=" + id + ", error=" + error);
            TempData["Error"] = error;
            return RedirectToAction("Index");
        }

        // Convert local file path to web-accessible URL
        string fileName = Path.GetFileName(localFilePath!);
        string fileUrl = Url.Content($"~/uploads/ebooks/{fileName}");
        ViewBag.EpubFileUrl = fileUrl;
        LoggerUtilities.Info("[End] prepared preview url for documentId=" + id);
        return View();
    }

    /// <summary>
    ///     Prepare a PDF file to be previewed in the browser.
    ///     Similar to <see cref="EpubPreview" />, but accepts PDF documents.
    /// </summary>
    public async Task<IActionResult> PdfPreview(int id)
    {
        // Use BookService to get or download the PDF file for preview (network op)
        LoggerUtilities.Info("[Start] retrieving PDF for documentId=" + id);
        BookDocument? bookDocument = _db.BookDocuments.FirstOrDefault(d => d.Id == id);
        if (bookDocument == null || Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant() != ".pdf")
        {
            LoggerUtilities.Warning("[Process] file not found or invalid extension. documentId=" + id);
            TempData["Error"] = "PDF file not found.";
            return RedirectToAction("Index");
        }

        // Use BookService to get or download the PDF file for preview
        (string? localFilePath, string? error) = await _bookService.GetOrDownloadPdfForPreviewAsync(bookDocument);

        if (!string.IsNullOrEmpty(error))
        {
            LoggerUtilities.Warning("[Process] error while retrieving file. documentId=" + id + ", error=" + error);
            TempData["Error"] = error;
            return RedirectToAction("Index");
        }

        // Convert local file path to web-accessible URL
        string fileName = Path.GetFileName(localFilePath!);
        string fileUrl = Url.Content($"~/uploads/ebooks/{fileName}");
        ViewBag.PdfFileUrl = fileUrl;
        LoggerUtilities.Info("[End] prepared preview url for documentId=" + id);
        return View();
    }

    /// <summary>
    ///     Handle POST requests to update book metadata and category mappings.
    ///     Validates input, updates cover image on Dropbox when provided and
    ///     synchronizes BookCategoryMap changes accordingly.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Update(EditBookVm editBookVm)
    {
        LoggerUtilities.Info("[Start] started. bookId=" + editBookVm.Id);
        try
        {
            if (editBookVm == null || !ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            // Validate language (if provided)
            if (!LanguageHelper.IsValidLanguage(editBookVm.Language))
            {
                LoggerUtilities.Warning("[Process] Invalid language - " + editBookVm.Language);
                throw new Exception("Invalid language!");
            }

            // must not be less than 1950 and not greater than current year
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

            Book? book = await _db.Books
                .Include(b => b.BookCategoryMaps)
                .FirstOrDefaultAsync(b => b.Id == editBookVm.Id);

            if (book == null) throw new Exception("Book not found.");

            // Update book properties
            book.Title = editBookVm.Title;
            book.AuthorName = editBookVm.AuthorName;
            book.Description = editBookVm.Description;
            book.PublicationYear = editBookVm.PublicationYear.HasValue
                ? new DateOnly(editBookVm.PublicationYear.Value, 1, 1)
                : null;
            book.LanguageCode = editBookVm.Language;
            book.UpdatedAt = DateTime.Now.ToLocalTime();

            // Handle CoverImage update with Dropbox
            if (editBookVm.CoverImage != null)
            {
                // Preserve old image path so we can delete it after successfully uploading a new one.
                string? oldCoverImagePath = book.CoverImagePath;

                // Upload new image to Dropbox
                (string? coverImagePath, string? coverImageLink, string? error) =
                    await _dropboxService.SaveImageAsync(editBookVm.CoverImage, Common.ImagePath);

                if (!string.IsNullOrEmpty(error)) throw new Exception(error);

                book.CoverImagePath = coverImagePath;
                book.CoverImageLink = coverImageLink;

                // Delete old image from Dropbox if it exists. Do not block update on failure to delete
                if (!string.IsNullOrWhiteSpace(oldCoverImagePath))
                {
                    bool deleted = await _dropboxService.DeleteImageAsync(oldCoverImagePath);
                    if (!deleted)
                        LoggerUtilities.Warning(
                            $"[Process] failed to delete old cover image from Dropbox for bookId={book.Id}");
                }
            }

            // Update categories
            List<BookCategoryMap> existingCategoryMaps = book.BookCategoryMaps != null
                ? book.BookCategoryMaps.ToList()
                : [];
            List<int> existingCategoryIds = existingCategoryMaps.Select(bcm => bcm.BookCategoryId).ToList();
            List<int> newCategoryIds = editBookVm.CategoryIds;

            // Auto-assign General category if no category is selected
            if (newCategoryIds.Count == 0)
            {
                BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                if (generalCategory != null)
                {
                    newCategoryIds = new List<int> { generalCategory.Id };
                    LoggerUtilities.Info($"[Process] Auto-assigned General category for bookId={book.Id}");
                }
                else
                {
                    LoggerUtilities.Warning(
                        $"[Process] General category not found for bookId={book.Id} - keeping existing categories");
                    // Keep existing categories if General category doesn't exist
                    newCategoryIds = existingCategoryIds;
                }
            }

            // Categories to add
            List<int> categoriesToAdd = newCategoryIds.Except(existingCategoryIds).ToList();
            foreach (int categoryId in categoriesToAdd)
                book.BookCategoryMaps!.Add(new BookCategoryMap
                {
                    BookId = book.Id,
                    BookCategoryId = categoryId
                });

            // Categories to remove
            List<BookCategoryMap> categoriesToRemove = existingCategoryMaps
                .Where(bcm => !newCategoryIds.Contains(bcm.BookCategoryId))
                .ToList();
            _db.BookCategoryMaps.RemoveRange(categoriesToRemove);

            // Update related Discussion forum title and description when book is renamed
            LoggerUtilities.Info($"[Process] fetching discussion for bookId={book.Id}");
            Discussion? discussion = await _db.Discussions
                .FirstOrDefaultAsync(d => d.BookId == book.Id);
            if (discussion != null)
            {
                discussion.Title = CreateDiscussionForumTitle(book.Title);
                discussion.Description =
                    $"A dedicated forum for in-depth discussion of \"{book.Title}\", written by {book.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.";
                discussion.UpdatedAt = DateTime.Now.ToLocalTime();
                _db.Discussions.Update(discussion);
                LoggerUtilities.Info($"[Process] updated discussion forum for bookId={book.Id}");
            }

            _db.Books.Update(book);
            await _db.SaveChangesAsync();

            LoggerUtilities.Info("[End] succeeded. bookId=" + editBookVm.Id);
            TempData[SweetAlert.Success] = "Book updated successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] error for bookId=" + editBookVm.Id + ", error=" + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Remove the cover image associated with the specified book. This
    ///     deletes the image from Dropbox and clears the image path and link
    ///     from the database record.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> RemoveCoverImage(int id)
    {
        try
        {
            LoggerUtilities.Info("[Start] started for id=" + id);
            if (!ModelState.IsValid) throw new Exception(Common.InvalidRequestErrorMessage);

            // Find the pass in book id
            Book? book = await _db.Books.FindAsync(id);
            if (book == null) throw new Exception("Book not found!");

            bool success = await _dropboxService.DeleteImageAsync(book.CoverImagePath);

            // If deletion fails it's likely the file already doesn't exist or the external
            // service is unavailable. Treat this as non-fatal – the desired final state is
            // that the DB record no longer references the image. Log and optionally
            // notify the admin with a non-fatal warning, then continue.
            if (!success)
            {
                LoggerUtilities.Warning("[Process] delete failed in Dropbox for bookId=" + id);
                TempData[SweetAlert.Warning] =
                    "Cover was removed from this book, but we couldn't delete the image file from Dropbox (it may already have been removed).";
            }

            // Update book record
            book.CoverImagePath = null;
            book.CoverImageLink = null;
            await _db.SaveChangesAsync();
            LoggerUtilities.Info("[End] succeeded for bookId=" + id);
            TempData[SweetAlert.Success] = "Book cover image removed successfully";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] error for id=" + id + ", error=" + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Display the ebook management page for a book. Shows all existing
    ///     ebook documents and allows admins to upload, rename or delete files.
    /// </summary>
    public async Task<IActionResult> EditEbook(int id)
    {
        LoggerUtilities.Info("[Start] (GET) started. bookId=" + id);
        Book? book = await _db.Books.FindAsync(id);
        if (book == null)
        {
            LoggerUtilities.Warning("[Process] (GET) book not found. bookId=" + id);
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

        List<BookDocument> existingEbooks = _db.BookDocuments.Where(d => d.BookId == id).ToList();

        EditEbookVm editEbookVm = new()
        {
            BookId = id,
            Book = book,
            ExistingEbooks = existingEbooks
        };

        LoggerUtilities.Info("[End] (GET) completed. bookId=" + id);
        return View(editEbookVm);
    }

    /// <summary>
    ///     Handle ebook management POST operations: file uploads, deletions,
    ///     and renames. Uses <see cref="IBookService" /> to carry out file
    ///     storage and renaming logic while keeping the database in sync.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> EditEbook(int bookId, EditEbookVm editEbookVm)
    {
        LoggerUtilities.Info("[Start] (POST) started. bookId=" + bookId);
        Book? book = await _db.Books.FindAsync(bookId);
        if (book == null)
        {
            LoggerUtilities.Warning("[Process] (POST) book not found. bookId=" + bookId);
            TempData[SweetAlert.Danger] = "Book not found.";
            return RedirectToAction("Index");
        }

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
                        // Delete related BookHistory records first (to avoid FK constraint violation)
                        List<BookHistory> bookHistoriesToDelete =
                            await _db.BookHistories.Where(bh => bh.BookDocumentId == docId).ToListAsync();
                        _db.BookHistories.RemoveRange(bookHistoriesToDelete);

                        // Delete related Bookmark records (to avoid FK constraint violation)
                        List<Bookmark> bookmarksToDelete =
                            await _db.Bookmarks.Where(bm => bm.BookDocumentId == docId).ToListAsync();
                        _db.Bookmarks.RemoveRange(bookmarksToDelete);

                        // Delete using BookService
                        LoggerUtilities.Info("[Process] deleting BookDocument id=" + docId);
                        await _bookService.DeleteBookDocumentAsync(bookDocument);
                        _db.BookDocuments.Remove(bookDocument);
                    }
                }

            // Handle name updates (rename file in Dropbox and update DB)
            if (editEbookVm.UpdatedNames != null && editEbookVm.UpdatedNames.Count > 0)
                foreach (KeyValuePair<int, string> keyValuePair in editEbookVm.UpdatedNames)
                {
                    int docId = keyValuePair.Key;
                    string newName = keyValuePair.Value?.Trim() ?? "";

                    if (string.IsNullOrEmpty(newName)) continue;

                    BookDocument? bookDocument =
                        _db.BookDocuments.FirstOrDefault(d => d.Id == docId && d.BookId == bookId);
                    if (bookDocument != null)
                    {
                        // Use BookService to rename
                        LoggerUtilities.Info("[Process] renaming BookDocument id=" + docId + " to " + newName);
                        (BookDocument? updatedDocument, string? error) =
                            await _bookService.UpdateBookDocumentNameAsync(bookDocument, newName, bookId);

                        if (updatedDocument != null)
                            // Save changes immediately to ensure DB consistency
                            await _db.SaveChangesAsync();
                        else
                            ModelState.AddModelError("", $"Failed to rename file to '{newName}': {error}");
                    }
                }

            // Handle new files
            List<BookDocument> newDocuments = [];
            List<string> allWarnings = [];
            if (editEbookVm.NewEbookFiles != null && editEbookVm.NewEbookFiles.Count > 0)
                for (int index = 0; index < editEbookVm.NewEbookFiles.Count; index++)
                {
                    IFormFile file = editEbookVm.NewEbookFiles[index];
                    string customName = editEbookVm.NewCustomNames?[index]?.Trim() ??
                                        Path.GetFileNameWithoutExtension(file.FileName);

                    // Use BookService to process the entire upload workflow
                    LoggerUtilities.Info("[Process] uploading new ebook file='" + file.FileName + "' for bookId=" +
                                         bookId);
                    (List<BookDocument> documents, List<string> warnings) =
                        await _bookService.ProcessNewEbookUploadAsync(file, bookId, customName);

                    // Add documents to the list
                    newDocuments.AddRange(documents);

                    // Collect warnings for later display
                    allWarnings.AddRange(warnings);
                }

            if (newDocuments.Count > 0) _db.BookDocuments.AddRange(newDocuments);

            await _db.SaveChangesAsync();

            // Display warnings before success message (if any exist)
            foreach (string warning in allWarnings)
                TempData[SweetAlert.Warning] = warning;

            LoggerUtilities.Info("[End] (POST) succeeded. bookId=" + bookId + ", newDocuments=" + newDocuments.Count);

            // Only show success message if there are no warnings
            if (allWarnings.Count == 0)
                TempData[SweetAlert.Success] = "Ebook files updated successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] (POST) failed. bookId=" + bookId + ". Error: " + ex.Message);
            TempData[SweetAlert.Danger] = $"Error updating ebook files: {ex.Message}";
        }

        // Reload the updated data for the partial view
        List<BookDocument> existingEbooks = _db.BookDocuments.Where(d => d.BookId == bookId).ToList();
        EditEbookVm updatedVm = new()
        {
            BookId = bookId,
            Book = book,
            ExistingEbooks = existingEbooks
        };

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[End] (POST) returning partial view for bookId=" + bookId);
            return PartialView("_EditEbook", updatedVm);
        }

        LoggerUtilities.Info("[End] (POST) completed. bookId=" + bookId);
        return RedirectToAction("EditEbook", new { id = bookId });
    }

    /// <summary>
    ///     Move the selected books to the application trash state (soft
    ///     deletion). Delegates to <see cref="IBookService" />.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Trash(int[] ids)
    {
        LoggerUtilities.Info("[Start] started. idsCount=" + (ids?.Length ?? 0));
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            // Use BookService to trash books
            (bool success, string? error) = await _bookService.TrashBooksAsync(ids);

            if (!success)
                throw new Exception(error ?? "Failed to trash books.");

            LoggerUtilities.Info("[End] succeeded. idsCount=" + ids.Length);
            TempData[SweetAlert.Success] = $"{ids.Length} book(s) moved to trash successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] failed. Error: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Recover selected books previously moved to trash. Restores their
    ///     status to available and updates the database.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Recover(int[] ids)
    {
        LoggerUtilities.Info("[Start] started. idsCount=" + (ids?.Length ?? 0));
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            // Use BookService to recover books
            (bool success, string? error) = await _bookService.RecoverBooksAsync(ids);

            if (!success)
                throw new Exception(error ?? "Failed to recover books.");

            LoggerUtilities.Info("[End] succeeded. idsCount=" + ids.Length);
            TempData[SweetAlert.Success] = $"{ids.Length} book(s) recovered successfully.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] failed. Error: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer();
    }

    /// <summary>
    ///     Permanently delete selected books and all related data.
    ///     Uses the book service to handle cascading deletes and storage
    ///     cleanup.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Delete(int[] ids)
    {
        LoggerUtilities.Info("[Start] started. idsCount=" + (ids?.Length ?? 0));
        try
        {
            if (!ModelState.IsValid || ids == null || ids.Length == 0)
                throw new Exception(Common.InvalidRequestErrorMessage);

            // Use BookService to delete books (handles cascade deletes)
            (bool success, string? error) = await _bookService.DeleteBooksAsync(ids);

            if (!success)
                throw new Exception(error ?? "Failed to delete books.");

            LoggerUtilities.Info("[End] succeeded. idsCount=" + ids.Length);
            TempData[SweetAlert.Success] = $"{ids.Length} book(s) deleted permanently.";
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] failed. Error: " + ex.Message);
            TempData[SweetAlert.Danger] = ex.Message;
        }

        return Request.RedirectToReferer(this, "Index", "Admin");
    }

    /// <summary>
    ///     Display the import page where an admin can upload a ZIP archive
    ///     containing CSV files for batch importing books and related data.
    /// </summary>
    public IActionResult Import()
    {
        if (Request.IsAjax()) return PartialView("_ImportUpload");
        return View();
    }

    /// <summary>
    ///     Process the uploaded ZIP file and parse contained CSVs. Detects
    ///     conflicts with existing books and prepares a conflict resolution
    ///     view for the admin to confirm import actions.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> UploadZipFile([FromForm] BookUploadZipVm model)
    {
        LoggerUtilities.Info("[Start] processing uploaded ZIP file '" + model?.ZipFile?.FileName + "'");
        if (model == null || !ModelState.IsValid || model.ZipFile == null)
        {
            TempData[SweetAlert.Danger] = "Please select a valid ZIP file.";
            TempData[SweetAlert.Danger] = "Please select a valid ZIP file.";
            return PartialView("_ImportUpload", model);
        }

        string[] allowedExtensions = [".zip"];
        string fileExt = Path.GetExtension(model.ZipFile.FileName).ToLowerInvariant();
        if (!allowedExtensions.Contains(fileExt))
        {
            ModelState.AddModelError("ZipFile", "Only .zip files are allowed.");
            return PartialView("_ImportUpload", model);
        }

        BookImportConflictVm bookImportConflictVm = new();
        List<BookImportRow> bookImportRows = [];
        List<BookCategoryMapImportRow> bookCategoryMapImportRows = [];
        List<BookDocumentImportRow> bookDocumentImportRows = [];

        try
        {
            // Extract and read CSV files from ZIP
            await using Stream zipStream = model.ZipFile.OpenReadStream();
            using ZipArchive archive = new(zipStream, ZipArchiveMode.Read);

            // Verify export integrity using manifest
            LoggerUtilities.Info("[Process] Verifying export integrity...");
            ZipArchiveEntry? manifestEntry = archive.GetEntry(".epiread_manifest.json");
            if (manifestEntry == null)
            {
                ModelState.AddModelError("ZipFile",
                    "This export file is missing integrity verification data. Please re-export the books using the latest version.");
                TempData[SweetAlert.Danger] = "Export file integrity verification failed: Missing manifest file.";
                return PartialView("_ImportUpload", model);
            }

            // Read and parse manifest
            dynamic? manifest = null;
            try
            {
                await using Stream manifestStream = manifestEntry.Open();
                manifest = await JsonSerializer.DeserializeAsync<JsonElement>(manifestStream);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("ZipFile", "Failed to read integrity manifest.");
                TempData[SweetAlert.Danger] = $"Export file integrity verification failed: {ex.Message}";
                LoggerUtilities.Error($"[Process] Manifest read error: {ex.Message}");
                return PartialView("_ImportUpload", model);
            }

            // Recompute hashes for verification
            Dictionary<string, string> computedHashes = new();
            string[] csvFiles = { "Books.csv", "BookCategoryMaps.csv", "BookDocuments.csv" };

            foreach (string csvFile in csvFiles)
            {
                ZipArchiveEntry? entry = archive.GetEntry(csvFile);
                if (entry != null)
                {
                    await using Stream entryStream = entry.Open();
                    using SHA256 sha256 = SHA256.Create();
                    byte[] hashBytes = await sha256.ComputeHashAsync(entryStream);
                    computedHashes[csvFile] = Convert.ToBase64String(hashBytes);
                }
            }

            // Verify file hashes match manifest
            JsonElement fileHashesElement = ((JsonElement)manifest).GetProperty("FileHashes");
            foreach (string csvFile in csvFiles)
                if (fileHashesElement.TryGetProperty(csvFile, out JsonElement expectedHashElement))
                {
                    string expectedHash = expectedHashElement.GetString() ?? "";
                    string computedHash = computedHashes.GetValueOrDefault(csvFile, "");

                    if (!string.Equals(expectedHash, computedHash, StringComparison.Ordinal))
                    {
                        ModelState.AddModelError("ZipFile",
                            $"File integrity check failed for {csvFile}. This export file may have been tampered with.");
                        TempData[SweetAlert.Danger] =
                            "Export file integrity verification failed: File hash mismatch detected.";
                        LoggerUtilities.Warning(
                            $"[Process] Hash mismatch for {csvFile}: Expected={expectedHash.Substring(0, 16)}..., Computed={computedHash.Substring(0, 16)}...");
                        return PartialView("_ImportUpload", model);
                    }

                    LoggerUtilities.Info($"[Process] Hash verified for {csvFile}");
                }

            // Verify integrity key
            string exportTimestamp = ((JsonElement)manifest).GetProperty("ExportTimestamp").GetString() ?? "";
            string combinedData = string.Join("|",
                computedHashes.GetValueOrDefault("Books.csv", ""),
                computedHashes.GetValueOrDefault("BookCategoryMaps.csv", ""),
                computedHashes.GetValueOrDefault("BookDocuments.csv", ""),
                exportTimestamp);

            using SHA256 sha256Final = SHA256.Create();
            byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
            string computedIntegrityKey = Convert.ToBase64String(integrityBytes);
            string expectedIntegrityKey = ((JsonElement)manifest).GetProperty("IntegrityKey").GetString() ?? "";

            if (!string.Equals(expectedIntegrityKey, computedIntegrityKey, StringComparison.Ordinal))
            {
                ModelState.AddModelError("ZipFile",
                    "Integrity key verification failed. This export file may have been tampered with.");
                TempData[SweetAlert.Danger] = "Export file integrity verification failed: Integrity key mismatch.";
                LoggerUtilities.Warning(
                    $"[Process] Integrity key mismatch: Expected={expectedIntegrityKey.Substring(0, 16)}..., Computed={computedIntegrityKey.Substring(0, 16)}...");
                return PartialView("_ImportUpload", model);
            }

            LoggerUtilities.Info("[Success] Export integrity verified successfully");

            // Read Books.csv
            ZipArchiveEntry? booksEntry = archive.GetEntry("Books.csv");
            if (booksEntry == null)
            {
                ModelState.AddModelError("ZipFile", "Books.csv not found in the ZIP file.");
                TempData[SweetAlert.Danger] = "Books.csv not found in the ZIP file.";
                return PartialView("_ImportUpload", model);
            }

            await using (Stream booksStream = booksEntry.Open())
            using (StreamReader booksReader = new(booksStream))
            using (CsvReader booksCsv = new(booksReader, CultureInfo.InvariantCulture))
            {
                bookImportRows = booksCsv.GetRecords<BookImportRow>().ToList();
            }

            // Read BookCategoryMaps.csv
            ZipArchiveEntry? bcmEntry = archive.GetEntry("BookCategoryMaps.csv");
            if (bcmEntry != null)
            {
                await using Stream bcmStream = bcmEntry.Open();
                using StreamReader bcmReader = new(bcmStream);
                using CsvReader bcmCsv = new(bcmReader, CultureInfo.InvariantCulture);
                bookCategoryMapImportRows = bcmCsv.GetRecords<BookCategoryMapImportRow>().ToList();
            }

            // Read BookDocuments.csv
            ZipArchiveEntry? bdEntry = archive.GetEntry("BookDocuments.csv");
            if (bdEntry != null)
            {
                await using Stream bdStream = bdEntry.Open();
                using StreamReader bdReader = new(bdStream);
                using CsvReader bdCsv = new(bdReader, CultureInfo.InvariantCulture);
                bookDocumentImportRows = bdCsv.GetRecords<BookDocumentImportRow>().ToList();
            }

            // Read export type from manifest (defaults to "metadata" if not present)
            string exportType = "metadata";
            if (((JsonElement)manifest).TryGetProperty("ExportType", out JsonElement exportTypeElement))
            {
                string? exportTypeValue = exportTypeElement.GetString();
                if (!string.IsNullOrWhiteSpace(exportTypeValue)) exportType = exportTypeValue;
            }

            TempData["ExportType"] = exportType;

            // Extract binary files if full export detected
            if (exportType.Equals("full", StringComparison.OrdinalIgnoreCase))
            {
                string tempFolder = Path.Combine(Path.GetTempPath(), $"epiread_import_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempFolder);
                Dictionary<string, string> extractedFiles = new();

                foreach (ZipArchiveEntry entry in archive.Entries)
                    // Extract non-CSV and non-marker files (cover images and ebooks)
                    if (!entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
                        !entry.FullName.StartsWith(".", StringComparison.OrdinalIgnoreCase) &&
                        !string.IsNullOrWhiteSpace(entry.Name))
                        try
                        {
                            string tempFilePath = Path.Combine(tempFolder,
                                entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                            string? tempFileDir = Path.GetDirectoryName(tempFilePath);
                            if (!string.IsNullOrWhiteSpace(tempFileDir))
                                Directory.CreateDirectory(tempFileDir);

                            await using Stream entryStream = entry.Open();
                            await using FileStream tempFs = new(tempFilePath, FileMode.Create, FileAccess.Write);
                            await entryStream.CopyToAsync(tempFs);

                            // Store mapping: ZIP entry path -> temp file path
                            extractedFiles[entry.FullName] = tempFilePath;
                            LoggerUtilities.Info($"[Process] Extracted {entry.FullName} to {tempFilePath}");
                        }
                        catch (Exception ex)
                        {
                            LoggerUtilities.Warning($"[Process] Failed to extract {entry.FullName}: {ex.Message}");
                        }

                TempData["ExtractedFiles"] = JsonSerializer.Serialize(extractedFiles);
                TempData["TempFolder"] = tempFolder;
                LoggerUtilities.Info($"[Process] Extracted {extractedFiles.Count} files to {tempFolder}");
            }

            // Store data in TempData for later use
            TempData["BookCategoryMapsData"] = JsonSerializer.Serialize(bookCategoryMapImportRows);
            TempData["BookDocumentsData"] = JsonSerializer.Serialize(bookDocumentImportRows);

            // Create category lookup by (BookTitle, BookAuthorName)
            Dictionary<string, List<string>> categoriesByBook = bookCategoryMapImportRows
                .GroupBy(bcm => $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}")
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(bcm => bcm.BookCategoryName ?? "Unknown").ToList()
                );

            // Detect conflicts in Books only
            // NOTE: With integrity verification in place, CSV duplicates are impossible
            // (hash verification would fail). We only need to check database conflicts.
            List<Book> existingBooks = await _db.Books
                .Include(b => b.BookCategoryMaps!)
                .ThenInclude(bcm => bcm.BookCategory)
                .ToListAsync();

            // Create lookup by Title + AuthorName (case-insensitive)
            Dictionary<string, Book> existingBooksByTitleAuthor = existingBooks
                .ToDictionary(
                    b => $"{b.Title.Trim().ToLower()}|{b.AuthorName.Trim().ToLower()}",
                    b => b
                );

            foreach (BookImportRow row in bookImportRows)
            {
                BookImportConflictItem item = new() { Row = row };
                string bookKey = $"{row.Title.Trim().ToLower()}|{row.AuthorName.Trim().ToLower()}";

                // Populate categories from imported CSV
                if (categoriesByBook.ContainsKey(bookKey)) item.Categories = categoriesByBook[bookKey];

                // Check if book exists in database
                if (existingBooksByTitleAuthor.ContainsKey(bookKey))
                {
                    Book existingBook = existingBooksByTitleAuthor[bookKey];
                    item.ConflictType = "Duplicate";
                    item.SuggestedAction = "Skip";
                    item.ExistingBookTitle = existingBook.Title;
                    item.ExistingBook = existingBook; // Store full existing book for comparison
                }
                else
                {
                    item.ConflictType = "New";
                    item.SuggestedAction = "Add";
                    item.ExistingBookTitle = null;
                    item.ExistingBook = null;
                }

                bookImportConflictVm.Items.Add(item);
            }
        }
        catch (TypeConverterException ex)
        {
            LoggerUtilities.Error("[End] CSV format error. fileName=" + model?.ZipFile?.FileName + ". Error: " +
                                  ex.Message);
            ModelState.AddModelError("ZipFile", $"CSV format error: {ex.Message}");
            TempData[SweetAlert.Danger] = $"CSV format error: {ex.Message}";
            return PartialView("_ImportUpload", model);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] processing error. fileName=" + model?.ZipFile?.FileName + ". Error: " +
                                  ex.Message);
            ModelState.AddModelError("ZipFile", $"Error processing ZIP file: {ex.Message}");
            TempData[SweetAlert.Danger] = $"Error processing ZIP file: {ex.Message}";
            return PartialView("_ImportUpload", model);
        }

        return PartialView("_ImportConflict", bookImportConflictVm);
    }

    /// <summary>
    ///     Initialize a chunked upload session for a large ZIP file.
    /// </summary>
    [HttpPost]
    public IActionResult InitChunkUpload([FromBody] ChunkUploadInitVm model)
    {
        LoggerUtilities.Info($"[Start] Initializing chunk upload for file: {model?.FileName}, size: {model?.FileSize}");

        if (model == null || !ModelState.IsValid)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request. Please provide valid file information."
            });

        // Validate file extension
        string fileExt = Path.GetExtension(model.FileName).ToLowerInvariant();
        if (fileExt != ".zip")
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Only .zip files are allowed."
            });

        // Generate unique upload ID
        string uploadId = Guid.NewGuid().ToString();

        // Create temp directory for chunks
        string tempFolder = Path.Combine(Path.GetTempPath(), $"epiread_chunk_{uploadId}");
        Directory.CreateDirectory(tempFolder);

        // Store session info
        lock (_chunkSessionLock)
        {
            _chunkSessions[uploadId] = new ChunkUploadSession
            {
                UploadId = uploadId,
                FileName = model.FileName,
                FileSize = model.FileSize,
                TotalChunks = model.TotalChunks,
                TempFolder = tempFolder,
                ReceivedChunks = new HashSet<int>(),
                CreatedAt = DateTime.UtcNow
            };
        }

        LoggerUtilities.Info($"[End] Chunk upload session initialized: {uploadId}");

        return Json(new ChunkUploadResponseVm
        {
            Success = true,
            UploadId = uploadId,
            Message = "Upload session initialized successfully."
        });
    }

    /// <summary>
    ///     Upload a single chunk of the file.
    /// </summary>
    [HttpPost]
    [RequestSizeLimit(8 * 1024 * 1024)] // 8MB limit per chunk request (5MB chunk + overhead)
    [RequestFormLimits(MultipartBodyLengthLimit = 8 * 1024 * 1024)]
    public async Task<IActionResult> UploadChunk([FromForm] ChunkUploadVm model)
    {
        if (model == null || !ModelState.IsValid || model.ChunkData == null)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid chunk data."
            });

        ChunkUploadSession? session;
        lock (_chunkSessionLock)
        {
            if (!_chunkSessions.TryGetValue(model.UploadId, out session))
                return Json(new ChunkUploadResponseVm
                {
                    Success = false,
                    Message = "Upload session not found or expired."
                });
        }

        try
        {
            // Save chunk to temp file
            string chunkPath = Path.Combine(session.TempFolder, $"chunk_{model.ChunkIndex}");
            await using (FileStream fs = new(chunkPath, FileMode.Create, FileAccess.Write))
            {
                await model.ChunkData.CopyToAsync(fs);
            }

            // Mark chunk as received
            lock (_chunkSessionLock)
            {
                session.ReceivedChunks.Add(model.ChunkIndex);
            }

            LoggerUtilities.Info(
                $"[Process] Received chunk {model.ChunkIndex + 1}/{session.TotalChunks} for upload {model.UploadId}");

            return Json(new ChunkUploadResponseVm
            {
                Success = true,
                UploadId = model.UploadId,
                ChunksReceived = session.ReceivedChunks.Count,
                TotalChunks = session.TotalChunks,
                Message = $"Chunk {model.ChunkIndex + 1} uploaded successfully."
            });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Error] Failed to save chunk {model.ChunkIndex} for upload {model.UploadId}");
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = $"Failed to save chunk: {ex.Message}"
            });
        }
    }

    /// <summary>
    ///     Complete the chunked upload and process the assembled ZIP file.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> CompleteChunkUpload([FromBody] ChunkUploadCompleteVm model)
    {
        LoggerUtilities.Info($"[Start] Completing chunk upload: {model?.UploadId}");

        if (model == null || !ModelState.IsValid)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request."
            });

        ChunkUploadSession? session;
        lock (_chunkSessionLock)
        {
            if (!_chunkSessions.TryGetValue(model.UploadId, out session))
                return Json(new ChunkUploadResponseVm
                {
                    Success = false,
                    Message = "Upload session not found or expired."
                });
        }

        // Verify all chunks received
        if (session.ReceivedChunks.Count != session.TotalChunks)
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                ChunksReceived = session.ReceivedChunks.Count,
                TotalChunks = session.TotalChunks,
                Message = $"Missing chunks. Received {session.ReceivedChunks.Count} of {session.TotalChunks}."
            });

        try
        {
            // Assemble chunks into final file
            string finalFilePath = Path.Combine(session.TempFolder, session.FileName);
            await using (FileStream finalStream = new(finalFilePath, FileMode.Create, FileAccess.Write))
            {
                for (int i = 0; i < session.TotalChunks; i++)
                {
                    string chunkPath = Path.Combine(session.TempFolder, $"chunk_{i}");
                    if (!System.IO.File.Exists(chunkPath)) throw new FileNotFoundException($"Chunk {i} not found.");

                    await using FileStream chunkStream = new(chunkPath, FileMode.Open, FileAccess.Read);
                    await chunkStream.CopyToAsync(finalStream);
                }
            }

            LoggerUtilities.Info($"[Process] Assembled {session.TotalChunks} chunks into {finalFilePath}");

            // Process the assembled ZIP file using the existing logic
            BookImportConflictVm bookImportConflictVm = await ProcessZipFileForImport(finalFilePath, session.FileName);

            // Clean up session
            CleanupChunkSession(model.UploadId);

            LoggerUtilities.Info($"[End] Chunk upload completed successfully: {model.UploadId}");

            // Return the conflict view
            return PartialView("_ImportConflict", bookImportConflictVm);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Error] Failed to complete chunk upload: {model.UploadId}");

            // Clean up on error
            CleanupChunkSession(model.UploadId);

            TempData[SweetAlert.Danger] = $"Error processing ZIP file: {ex.Message}";
            return PartialView("_ImportUpload", new BookUploadZipVm());
        }
    }

    /// <summary>
    ///     Cancel a chunk upload session and clean up resources.
    /// </summary>
    [HttpPost]
    public IActionResult CancelChunkUpload([FromBody] ChunkUploadCompleteVm model)
    {
        if (model == null || string.IsNullOrWhiteSpace(model.UploadId))
            return Json(new ChunkUploadResponseVm
            {
                Success = false,
                Message = "Invalid request."
            });

        CleanupChunkSession(model.UploadId);

        return Json(new ChunkUploadResponseVm
        {
            Success = true,
            Message = "Upload cancelled."
        });
    }

    /// <summary>
    ///     Process the ZIP file for import (extracted from UploadZipFile for reuse).
    /// </summary>
    private async Task<BookImportConflictVm> ProcessZipFileForImport(string zipFilePath, string originalFileName)
    {
        BookImportConflictVm bookImportConflictVm = new();
        List<BookImportRow> bookImportRows = [];
        List<BookCategoryMapImportRow> bookCategoryMapImportRows = [];
        List<BookDocumentImportRow> bookDocumentImportRows = [];

        await using FileStream zipFileStream = new(zipFilePath, FileMode.Open, FileAccess.Read);
        using ZipArchive archive = new(zipFileStream, ZipArchiveMode.Read);

        // Verify export integrity using manifest
        LoggerUtilities.Info("[Process] Verifying export integrity...");
        ZipArchiveEntry? manifestEntry = archive.GetEntry(".epiread_manifest.json");
        if (manifestEntry == null)
            throw new InvalidOperationException(
                "This export file is missing integrity verification data. Please re-export the books using the latest version.");

        // Read and parse manifest
        dynamic? manifest = null;
        try
        {
            await using Stream manifestStream = manifestEntry.Open();
            manifest = await JsonSerializer.DeserializeAsync<JsonElement>(manifestStream);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to read integrity manifest: {ex.Message}");
        }

        // Recompute hashes for verification
        Dictionary<string, string> computedHashes = new();
        string[] csvFiles = { "Books.csv", "BookCategoryMaps.csv", "BookDocuments.csv" };

        foreach (string csvFile in csvFiles)
        {
            ZipArchiveEntry? entry = archive.GetEntry(csvFile);
            if (entry != null)
            {
                await using Stream entryStream = entry.Open();
                using SHA256 sha256 = SHA256.Create();
                byte[] hashBytes = await sha256.ComputeHashAsync(entryStream);
                computedHashes[csvFile] = Convert.ToBase64String(hashBytes);
            }
        }

        // Verify file hashes match manifest
        JsonElement fileHashesElement = ((JsonElement)manifest).GetProperty("FileHashes");
        foreach (string csvFile in csvFiles)
            if (fileHashesElement.TryGetProperty(csvFile, out JsonElement expectedHashElement))
            {
                string expectedHash = expectedHashElement.GetString() ?? "";
                string computedHash = computedHashes.GetValueOrDefault(csvFile, "");

                if (!string.Equals(expectedHash, computedHash, StringComparison.Ordinal))
                    throw new InvalidOperationException(
                        $"File integrity check failed for {csvFile}. This export file may have been tampered with.");

                LoggerUtilities.Info($"[Process] Hash verified for {csvFile}");
            }

        // Verify integrity key
        string exportTimestamp = ((JsonElement)manifest).GetProperty("ExportTimestamp").GetString() ?? "";
        string combinedData = string.Join("|",
            computedHashes.GetValueOrDefault("Books.csv", ""),
            computedHashes.GetValueOrDefault("BookCategoryMaps.csv", ""),
            computedHashes.GetValueOrDefault("BookDocuments.csv", ""),
            exportTimestamp);

        using SHA256 sha256Final = SHA256.Create();
        byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
        string computedIntegrityKey = Convert.ToBase64String(integrityBytes);
        string expectedIntegrityKey = ((JsonElement)manifest).GetProperty("IntegrityKey").GetString() ?? "";

        if (!string.Equals(expectedIntegrityKey, computedIntegrityKey, StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Integrity key verification failed. This export file may have been tampered with.");

        LoggerUtilities.Info("[Success] Export integrity verified successfully");

        // Read Books.csv
        ZipArchiveEntry? booksEntry = archive.GetEntry("Books.csv");
        if (booksEntry == null) throw new InvalidOperationException("Books.csv not found in the ZIP file.");

        await using (Stream booksStream = booksEntry.Open())
        using (StreamReader booksReader = new(booksStream))
        using (CsvReader booksCsv = new(booksReader, CultureInfo.InvariantCulture))
        {
            bookImportRows = booksCsv.GetRecords<BookImportRow>().ToList();
        }

        // Read BookCategoryMaps.csv
        ZipArchiveEntry? bcmEntry = archive.GetEntry("BookCategoryMaps.csv");
        if (bcmEntry != null)
        {
            await using Stream bcmStream = bcmEntry.Open();
            using StreamReader bcmReader = new(bcmStream);
            using CsvReader bcmCsv = new(bcmReader, CultureInfo.InvariantCulture);
            bookCategoryMapImportRows = bcmCsv.GetRecords<BookCategoryMapImportRow>().ToList();
        }

        // Read BookDocuments.csv
        ZipArchiveEntry? bdEntry = archive.GetEntry("BookDocuments.csv");
        if (bdEntry != null)
        {
            await using Stream bdStream = bdEntry.Open();
            using StreamReader bdReader = new(bdStream);
            using CsvReader bdCsv = new(bdReader, CultureInfo.InvariantCulture);
            bookDocumentImportRows = bdCsv.GetRecords<BookDocumentImportRow>().ToList();
        }

        // Read export type from manifest (defaults to "metadata" if not present)
        string exportType = "metadata";
        if (((JsonElement)manifest).TryGetProperty("ExportType", out JsonElement exportTypeElement))
        {
            string? exportTypeValue = exportTypeElement.GetString();
            if (!string.IsNullOrWhiteSpace(exportTypeValue)) exportType = exportTypeValue;
        }

        TempData["ExportType"] = exportType;

        // Extract binary files if full export detected
        if (exportType.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            string tempFolder = Path.Combine(Path.GetTempPath(), $"epiread_import_{Guid.NewGuid()}");
            Directory.CreateDirectory(tempFolder);
            Dictionary<string, string> extractedFiles = new();

            foreach (ZipArchiveEntry entry in archive.Entries)
                // Extract non-CSV and non-marker files (cover images and ebooks)
                if (!entry.FullName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase) &&
                    !entry.FullName.StartsWith(".", StringComparison.OrdinalIgnoreCase) &&
                    !string.IsNullOrWhiteSpace(entry.Name))
                    try
                    {
                        string tempFilePath = Path.Combine(tempFolder,
                            entry.FullName.Replace('/', Path.DirectorySeparatorChar));
                        string? tempFileDir = Path.GetDirectoryName(tempFilePath);
                        if (!string.IsNullOrWhiteSpace(tempFileDir))
                            Directory.CreateDirectory(tempFileDir);

                        await using Stream entryStream = entry.Open();
                        await using FileStream tempFs = new(tempFilePath, FileMode.Create, FileAccess.Write);
                        await entryStream.CopyToAsync(tempFs);

                        // Store mapping: ZIP entry path -> temp file path
                        extractedFiles[entry.FullName] = tempFilePath;
                        LoggerUtilities.Info($"[Process] Extracted {entry.FullName} to {tempFilePath}");
                    }
                    catch (Exception ex)
                    {
                        LoggerUtilities.Warning($"[Process] Failed to extract {entry.FullName}: {ex.Message}");
                    }

            TempData["ExtractedFiles"] = JsonSerializer.Serialize(extractedFiles);
            TempData["TempFolder"] = tempFolder;
            LoggerUtilities.Info($"[Process] Extracted {extractedFiles.Count} files to {tempFolder}");
        }

        // Store data in TempData for later use
        TempData["BookCategoryMapsData"] = JsonSerializer.Serialize(bookCategoryMapImportRows);
        TempData["BookDocumentsData"] = JsonSerializer.Serialize(bookDocumentImportRows);

        // Create category lookup by (BookTitle, BookAuthorName)
        Dictionary<string, List<string>> categoriesByBook = bookCategoryMapImportRows
            .GroupBy(bcm => $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}")
            .ToDictionary(
                g => g.Key,
                g => g.Select(bcm => bcm.BookCategoryName ?? "Unknown").ToList()
            );

        // Detect conflicts in Books only
        List<Book> existingBooks = await _db.Books
            .Include(b => b.BookCategoryMaps!)
            .ThenInclude(bcm => bcm.BookCategory)
            .ToListAsync();

        // Create lookup by Title + AuthorName (case-insensitive)
        Dictionary<string, Book> existingBooksByTitleAuthor = existingBooks
            .ToDictionary(
                b => $"{b.Title.Trim().ToLower()}|{b.AuthorName.Trim().ToLower()}",
                b => b
            );

        foreach (BookImportRow row in bookImportRows)
        {
            BookImportConflictItem item = new() { Row = row };
            string bookKey = $"{row.Title.Trim().ToLower()}|{row.AuthorName.Trim().ToLower()}";

            // Populate categories from imported CSV
            if (categoriesByBook.ContainsKey(bookKey)) item.Categories = categoriesByBook[bookKey];

            // Check if book exists in database
            if (existingBooksByTitleAuthor.ContainsKey(bookKey))
            {
                Book existingBook = existingBooksByTitleAuthor[bookKey];
                item.ConflictType = "Duplicate";
                item.SuggestedAction = "Skip";
                item.ExistingBookTitle = existingBook.Title;
                item.ExistingBook = existingBook;
            }
            else
            {
                item.ConflictType = "New";
                item.SuggestedAction = "Add";
                item.ExistingBookTitle = null;
                item.ExistingBook = null;
            }

            bookImportConflictVm.Items.Add(item);
        }

        return bookImportConflictVm;
    }

    /// <summary>
    ///     Clean up chunk upload session and temporary files.
    /// </summary>
    private void CleanupChunkSession(string uploadId)
    {
        lock (_chunkSessionLock)
        {
            if (_chunkSessions.TryGetValue(uploadId, out ChunkUploadSession? session))
            {
                try
                {
                    if (Directory.Exists(session.TempFolder)) Directory.Delete(session.TempFolder, true);
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning($"[Process] Failed to clean up temp folder for {uploadId}: {ex.Message}");
                }

                _chunkSessions.Remove(uploadId);
            }
        }
    }

    /// <summary>
    ///     Confirm and execute the bulk import operation using the user's
    ///     conflict resolution choices. Import preserves relationship data
    ///     and executes inside a transaction to ensure consistency.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ImportConfirm([FromBody] BookImportConfirmVm model)
    {
        int added = 0, updated = 0, skipped = 0, unresolved = 0;
        if (model == null || model.Items == null || !model.Items.Any())
        {
            LoggerUtilities.Warning("ImportConfirm no items received.");
            return PartialView("_ImportSummary",
                new BookImportSummaryVm { Added = 0, Updated = 0, Skipped = 0, Unresolved = 0 });
        }

        // Retrieve stored relationship data
        List<BookCategoryMapImportRow> bookCategoryMapImportRows = [];
        List<BookDocumentImportRow> bookDocumentImportRows = [];

        if (TempData["BookCategoryMapsData"] is string bcmJson)
            bookCategoryMapImportRows = JsonSerializer.Deserialize<List<BookCategoryMapImportRow>>(bcmJson) ??
                                        [];

        if (TempData["BookDocumentsData"] is string bdJson)
            bookDocumentImportRows = JsonSerializer.Deserialize<List<BookDocumentImportRow>>(bdJson) ??
                                     [];

        // Retrieve extracted files mapping and temp folder path
        Dictionary<string, string>? extractedFiles = null;
        string? tempFolder = null;
        if (TempData["ExtractedFiles"] is string extractedJson)
        {
            extractedFiles = JsonSerializer.Deserialize<Dictionary<string, string>>(extractedJson);
            LoggerUtilities.Info($"[Process] Loaded {extractedFiles?.Count ?? 0} extracted files for upload");
        }

        if (TempData["TempFolder"] is string folder) tempFolder = folder;

        // Retrieve export type to determine import behavior
        string exportType = TempData["ExportType"] as string ?? "metadata";
        bool isMetadataOnly = exportType.Equals("metadata", StringComparison.OrdinalIgnoreCase);

        if (isMetadataOnly)
            LoggerUtilities.Info(
                "[Process] Metadata-only import detected. Existing cover images and ebook files will be preserved.");

        // Validate action choices against conflict types to prevent invalid operations
        // NOTE: With integrity verification, CSV duplicates are impossible, simplifying validation
        List<Book> existingBooksForValidation = await _db.Books.ToListAsync();
        Dictionary<string, Book> existingBooksByKey = existingBooksForValidation
            .ToDictionary(
                b => $"{b.Title.Trim().ToLower()}|{b.AuthorName.Trim().ToLower()}",
                b => b
            );

        foreach (BookImportConfirmItemVm item in model.Items)
        {
            string bookKey = $"{item.Title.Trim().ToLower()}|{item.AuthorName.Trim().ToLower()}";
            bool existsInDb = existingBooksByKey.ContainsKey(bookKey);

            // Validate action based on conflict type
            if (existsInDb)
            {
                // Duplicate (exists in DB): only Overwrite, Skip, or Merge allowed
                if (item.Action == "Add")
                {
                    LoggerUtilities.Warning(
                        $"[Validation] Invalid action 'Add' for existing book '{item.Title}'. Setting to Overwrite.");
                    item.Action = "Overwrite";
                }
            }
            else
            {
                // New: only Add or Skip allowed
                if (item.Action != "Add" && item.Action != "Skip")
                {
                    LoggerUtilities.Warning(
                        $"[Validation] Invalid action '{item.Action}' for new book '{item.Title}'. Setting to Add.");
                    item.Action = "Add";
                }
            }
        }

        // Process all items directly (no deduplication needed - integrity verification prevents duplicates)
        List<BookImportConfirmItemVm> deduplicatedItems = model.Items;

        // Track files to delete from Dropbox after successful transaction
        List<(int Id, int BookId, string DocumentPath, string? DocumentLink)> documentsToDelete = [];
        List<string> coverImagesToDelete = [];

        // Use transaction to ensure data consistency
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (BookImportConfirmItemVm item in deduplicatedItems)
                try
                {
                    // Match by Title + AuthorName instead of ID
                    string bookKey = $"{item.Title.Trim().ToLower()}|{item.AuthorName.Trim().ToLower()}";

                    Book? existingBook = await _db.Books
                        .Include(b => b.BookCategoryMaps)
                        .FirstOrDefaultAsync(b =>
                            b.Title.Trim().ToLower() + "|" + b.AuthorName.Trim().ToLower() == bookKey);

                    // Get existing book documents separately
                    List<BookDocument>? existingBookDocuments = existingBook != null
                        ? await _db.BookDocuments.Where(bd => bd.BookId == existingBook.Id).ToListAsync()
                        : null;

                    // Resolve owner from import row (optional). Option A: unknown users => null
                    int? resolvedUserId = null;
                    if (!string.IsNullOrWhiteSpace(item.UserEmail))
                    {
                        User? userByEmail = await _db.Users.FirstOrDefaultAsync(u => u.Email == item.UserEmail);
                        if (userByEmail != null) resolvedUserId = userByEmail.Id;
                    }

                    if (resolvedUserId == null && item.UserId.HasValue)
                    {
                        bool userExists = await _db.Users.AnyAsync(u => u.Id == item.UserId.Value);
                        if (userExists) resolvedUserId = item.UserId.Value;
                    }

                    switch (item.Action)
                    {
                        case "Add":
                        {
                            Book newBook = new()
                            {
                                Title = item.Title,
                                AuthorName = item.AuthorName,
                                Description = item.Description,
                                PublicationYear = item.PublicationYear.HasValue
                                    ? DateOnly.FromDateTime(new DateTime(item.PublicationYear.Value, 1, 1))
                                    : null,
                                LanguageCode = item.LanguageCode,
                                CoverImagePath = item.CoverImagePath,
                                CoverImageLink = item.CoverImageLink,
                                UserId = resolvedUserId,
                                Status = item.Status,
                                CreatedAt = item.CreatedAt,
                                UpdatedAt = item.UpdatedAt
                            };

                            _db.Books.Add(newBook);
                            await _db.SaveChangesAsync(); // Save to get the new ID

                            // Upload cover image if extracted files exist (skip for metadata-only imports)
                            if (!isMetadataOnly && extractedFiles != null &&
                                !string.IsNullOrWhiteSpace(newBook.CoverImagePath))
                            {
                                string normalizedPath = newBook.CoverImagePath.TrimStart('/').Replace('\\', '/');
                                if (extractedFiles.TryGetValue(normalizedPath, out string? tempFilePath) &&
                                    System.IO.File.Exists(tempFilePath))
                                    try
                                    {
                                        // Create FormFile from temp file for service method
                                        await using FileStream fs = new(tempFilePath, FileMode.Open, FileAccess.Read);
                                        FormFile formFile = new(fs, 0, fs.Length, "file",
                                            Path.GetFileName(tempFilePath))
                                        {
                                            Headers = new HeaderDictionary(),
                                            ContentType = "image/jpeg"
                                        };

                                        // Use service to upload with proper path generation
                                        (string? imagePath, string? imageLink, string? error) =
                                            await _dropboxService.SaveImageAsync(formFile, Common.ImagePath);

                                        if (string.IsNullOrEmpty(error))
                                        {
                                            newBook.CoverImagePath = imagePath;
                                            newBook.CoverImageLink = imageLink;
                                            LoggerUtilities.Info(
                                                $"[Process] Uploaded cover image for book '{newBook.Title}' to {imagePath}");
                                        }
                                        else
                                        {
                                            LoggerUtilities.Warning(
                                                $"[Process] Failed to upload cover image for book '{newBook.Title}': {error}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggerUtilities.Warning(
                                            $"[Process] Failed to upload cover image for book '{newBook.Title}': {ex.Message}");
                                    }
                            }

                            // Auto create a discussion forum for the book
                            Discussion discussion = new()
                            {
                                Title = CreateDiscussionForumTitle(newBook.Title),
                                Description =
                                    $"A dedicated forum for in-depth discussion of \"{newBook.Title}\", written by {newBook.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.",
                                BookId = newBook.Id,
                                CreatedAt = DateTime.Now.ToLocalTime(),
                                UpdatedAt = DateTime.Now.ToLocalTime()
                            };
                            _db.Discussions.Add(discussion);

                            // Add category mappings
                            List<BookCategoryMapImportRow> categoryMaps = bookCategoryMapImportRows
                                .Where(bcm =>
                                    $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}" ==
                                    bookKey)
                                .ToList();

                            bool addedCategoryMapping = false;
                            foreach (BookCategoryMapImportRow bcm in categoryMaps)
                            {
                                // Resolve category: try by ID first, then by name (helps when importing into a different system)
                                int? resolvedCategoryId = null;
                                if (bcm.BookCategoryId > 0)
                                {
                                    BookCategory? categoryId = await _db.BookCategories.FindAsync(bcm.BookCategoryId);
                                    if (categoryId != null) resolvedCategoryId = categoryId.Id;
                                }

                                if (resolvedCategoryId == null && !string.IsNullOrWhiteSpace(bcm.BookCategoryName))
                                {
                                    BookCategory? categoryName = await _db.BookCategories
                                        .FirstOrDefaultAsync(bc =>
                                            bc.Name.Trim().ToLower() == bcm.BookCategoryName.Trim().ToLower());
                                    if (categoryName != null) resolvedCategoryId = categoryName.Id;
                                }

                                if (resolvedCategoryId.HasValue)
                                    _db.BookCategoryMaps.Add(new BookCategoryMap
                                    {
                                        BookId = newBook.Id,
                                        BookCategoryId = resolvedCategoryId.Value
                                    });
                                if (resolvedCategoryId.HasValue) addedCategoryMapping = true;
                            }

                            // If no category mapping was valid for this new book, assign it to the default "General" category
                            if (!addedCategoryMapping)
                            {
                                BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                                if (generalCategory != null)
                                    _db.BookCategoryMaps.Add(new BookCategoryMap
                                    {
                                        BookId = newBook.Id,
                                        BookCategoryId = generalCategory.Id
                                    });
                            }

                            // Add book documents (skip file uploads for metadata-only imports)
                            List<BookDocumentImportRow> documents = bookDocumentImportRows
                                .Where(bd =>
                                    $"{bd.BookTitle.Trim().ToLower()}|{bd.BookAuthorName.Trim().ToLower()}" == bookKey)
                                .ToList();

                            foreach (BookDocumentImportRow bd in documents)
                                // Upload ebook if extracted files exist - use service for proper path generation
                                if (!isMetadataOnly && extractedFiles != null &&
                                    !string.IsNullOrWhiteSpace(bd.DocumentPath))
                                {
                                    string normalizedDocPath = bd.DocumentPath.TrimStart('/').Replace('\\', '/');
                                    if (extractedFiles.TryGetValue(normalizedDocPath, out string? tempDocPath) &&
                                        System.IO.File.Exists(tempDocPath))
                                        try
                                        {
                                            // Create FormFile from temp file
                                            await using FileStream docFs = new(tempDocPath, FileMode.Open,
                                                FileAccess.Read);
                                            string originalFileName = Path.GetFileName(tempDocPath);
                                            FormFile formFile = new(docFs, 0, docFs.Length, "file", originalFileName)
                                            {
                                                Headers = new HeaderDictionary(),
                                                ContentType = "application/octet-stream"
                                            };

                                            // Extract custom name (remove old book ID patterns)
                                            string customName = Path.GetFileNameWithoutExtension(originalFileName);
                                            // Remove prefix pattern like "123_EPUB_" or "123_PDF_" (old format)
                                            customName = Regex.Replace(customName, @"^\d+_(EPUB|PDF)_", "");
                                            // Remove suffix pattern "_bookId_guid" to get clean custom name
                                            customName = Regex.Replace(customName, @"_\d+_[0-9a-f-]+$", "");

                                            // Use service to process upload with NEW book ID
                                            (List<BookDocument> uploadedDocs, List<string> warnings) =
                                                await _bookService.ProcessNewEbookUploadAsync(formFile, newBook.Id,
                                                    customName);

                                            // Add the documents with NEW paths
                                            foreach (BookDocument doc in uploadedDocs)
                                            {
                                                // Preserve original timestamps
                                                doc.CreatedAt = bd.CreatedAt;
                                                doc.UpdatedAt = bd.UpdatedAt;
                                                _db.BookDocuments.Add(doc);
                                                LoggerUtilities.Info(
                                                    $"[Process] Uploaded ebook {doc.FormatType} for book '{newBook.Title}' to {doc.DocumentPath}");
                                            }

                                            foreach (string warning in warnings)
                                                LoggerUtilities.Warning($"[Process] {warning}");
                                        }
                                        catch (Exception ex)
                                        {
                                            LoggerUtilities.Warning(
                                                $"[Process] Failed to upload ebook for book '{newBook.Title}': {ex.Message}");
                                        }
                                }
                                else
                                {
                                    // No file extracted, just add metadata from CSV
                                    BookDocument newDoc = new BookDocument
                                    {
                                        BookId = newBook.Id,
                                        FormatType = bd.FormatType,
                                        DocumentPath = bd.DocumentPath,
                                        DocumentLink = bd.DocumentLink,
                                        CreatedAt = bd.CreatedAt,
                                        UpdatedAt = bd.UpdatedAt
                                    };
                                    _db.BookDocuments.Add(newDoc);
                                }

                            added++;
                            break;
                        }
                        case "Overwrite" when existingBook != null:
                        {
                            // For metadata-only imports, preserve existing files
                            if (!isMetadataOnly)
                            {
                                // Collect old cover image for deletion after transaction (full import only)
                                if (!string.IsNullOrWhiteSpace(existingBook.CoverImagePath))
                                    coverImagesToDelete.Add(existingBook.CoverImagePath);

                                // Collect old document files for deletion after transaction (full import only)
                                if (existingBookDocuments != null && existingBookDocuments.Any())
                                    foreach (BookDocument doc in existingBookDocuments)
                                        documentsToDelete.Add((doc.Id, doc.BookId, doc.DocumentPath, doc.DocumentLink));
                            }

                            // Update book properties
                            existingBook.Title = item.Title;
                            existingBook.AuthorName = item.AuthorName;
                            existingBook.Description = item.Description;
                            existingBook.PublicationYear = item.PublicationYear.HasValue
                                ? DateOnly.FromDateTime(new DateTime(item.PublicationYear.Value, 1, 1))
                                : null;
                            existingBook.LanguageCode = item.LanguageCode;

                            // For metadata-only imports, preserve existing cover image paths
                            if (!isMetadataOnly)
                            {
                                existingBook.CoverImagePath = item.CoverImagePath;
                                existingBook.CoverImageLink = item.CoverImageLink;
                            }

                            // Update owner (Option A: set to resolved user id or null)
                            existingBook.UserId = resolvedUserId;
                            existingBook.Status = item.Status;
                            existingBook.CreatedAt = item.CreatedAt;
                            existingBook.UpdatedAt = item.UpdatedAt;

                            // Upload cover image if extracted files exist (skip for metadata-only imports)
                            if (!isMetadataOnly && extractedFiles != null &&
                                !string.IsNullOrWhiteSpace(existingBook.CoverImagePath))
                            {
                                string normalizedPath = existingBook.CoverImagePath.TrimStart('/').Replace('\\', '/');
                                if (extractedFiles.TryGetValue(normalizedPath, out string? tempFilePath) &&
                                    System.IO.File.Exists(tempFilePath))
                                    try
                                    {
                                        await using FileStream fs = new(tempFilePath, FileMode.Open, FileAccess.Read);
                                        FormFile formFile = new(fs, 0, fs.Length, "file",
                                            Path.GetFileName(tempFilePath))
                                        {
                                            Headers = new HeaderDictionary(),
                                            ContentType = "image/jpeg"
                                        };

                                        (string? imagePath, string? imageLink, string? error) =
                                            await _dropboxService.SaveImageAsync(formFile, Common.ImagePath);

                                        if (string.IsNullOrEmpty(error))
                                        {
                                            existingBook.CoverImagePath = imagePath;
                                            existingBook.CoverImageLink = imageLink;
                                            LoggerUtilities.Info(
                                                $"[Process] Uploaded cover image for book '{existingBook.Title}' to {imagePath}");
                                        }
                                        else
                                        {
                                            LoggerUtilities.Warning(
                                                $"[Process] Failed to upload cover image for book '{existingBook.Title}': {error}");
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        LoggerUtilities.Warning(
                                            $"[Process] Failed to upload cover image for book '{existingBook.Title}': {ex.Message}");
                                    }
                            }

                            // Remove existing category mappings and add new ones
                            _db.BookCategoryMaps.RemoveRange(existingBook.BookCategoryMaps ??
                                                             new List<BookCategoryMap>());

                            List<BookCategoryMapImportRow> categoryMaps = bookCategoryMapImportRows
                                .Where(bcm =>
                                    $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}" ==
                                    bookKey)
                                .ToList();

                            foreach (BookCategoryMapImportRow bcm in categoryMaps)
                            {
                                // Resolve category by ID, or fall back to name if ID not present on this system
                                int? resolvedCategoryId = null;
                                if (bcm.BookCategoryId > 0)
                                {
                                    BookCategory? catById = await _db.BookCategories.FindAsync(bcm.BookCategoryId);
                                    if (catById != null) resolvedCategoryId = catById.Id;
                                }

                                if (resolvedCategoryId == null && !string.IsNullOrWhiteSpace(bcm.BookCategoryName))
                                {
                                    BookCategory? catByName = await _db.BookCategories
                                        .FirstOrDefaultAsync(bc =>
                                            bc.Name.Trim().ToLower() == bcm.BookCategoryName.Trim().ToLower());
                                    if (catByName != null) resolvedCategoryId = catByName.Id;
                                }

                                if (resolvedCategoryId.HasValue)
                                    _db.BookCategoryMaps.Add(new BookCategoryMap
                                    {
                                        BookId = existingBook.Id,
                                        BookCategoryId = resolvedCategoryId.Value
                                    });
                            }

                            // For metadata-only imports, skip document removal and upload
                            // For full imports, remove existing documents and add new ones
                            if (!isMetadataOnly)
                            {
                                if (existingBookDocuments != null && existingBookDocuments.Any())
                                {
                                    // Get IDs of existing book documents
                                    int[] existingDocIds = existingBookDocuments.Select(bd => bd.Id).ToArray();

                                    // Delete related BookHistory records first (to avoid FK constraint violation)
                                    List<BookHistory> relatedHistories =
                                        await _db.BookHistories.Where(bh => existingDocIds.Contains(bh.BookDocumentId))
                                            .ToListAsync();
                                    _db.BookHistories.RemoveRange(relatedHistories);

                                    // Delete related Bookmark records (to avoid FK constraint violation)
                                    List<Bookmark> relatedBookmarks =
                                        await _db.Bookmarks.Where(bm => existingDocIds.Contains(bm.BookDocumentId))
                                            .ToListAsync();
                                    _db.Bookmarks.RemoveRange(relatedBookmarks);

                                    // Now remove the book documents
                                    _db.BookDocuments.RemoveRange(existingBookDocuments);
                                }

                                List<BookDocumentImportRow> documents = bookDocumentImportRows
                                    .Where(bd =>
                                        $"{bd.BookTitle.Trim().ToLower()}|{bd.BookAuthorName.Trim().ToLower()}" ==
                                        bookKey)
                                    .ToList();

                                foreach (BookDocumentImportRow bd in documents)
                                    // Upload ebook if extracted files exist
                                    if (extractedFiles != null && !string.IsNullOrWhiteSpace(bd.DocumentPath))
                                    {
                                        string normalizedDocPath = bd.DocumentPath.TrimStart('/').Replace('\\', '/');
                                        if (extractedFiles.TryGetValue(normalizedDocPath, out string? tempDocPath) &&
                                            System.IO.File.Exists(tempDocPath))
                                            try
                                            {
                                                await using FileStream docFs = new(tempDocPath, FileMode.Open,
                                                    FileAccess.Read);
                                                string originalFileName = Path.GetFileName(tempDocPath);
                                                FormFile formFile = new(docFs, 0, docFs.Length, "file",
                                                    originalFileName)
                                                {
                                                    Headers = new HeaderDictionary(),
                                                    ContentType = "application/octet-stream"
                                                };

                                                string customName = Path.GetFileNameWithoutExtension(originalFileName);
                                                // Remove prefix pattern like "123_EPUB_" or "123_PDF_" (old format)
                                                customName = Regex.Replace(customName, @"^\d+_(EPUB|PDF)_", "");
                                                // Remove suffix pattern "_bookId_guid" to get clean custom name
                                                customName = Regex.Replace(customName, @"_\d+_[0-9a-f-]+$", "");

                                                (List<BookDocument> uploadedDocs, List<string> warnings) =
                                                    await _bookService.ProcessNewEbookUploadAsync(formFile,
                                                        existingBook.Id,
                                                        customName);

                                                foreach (BookDocument doc in uploadedDocs)
                                                {
                                                    doc.CreatedAt = bd.CreatedAt;
                                                    doc.UpdatedAt = bd.UpdatedAt;
                                                    _db.BookDocuments.Add(doc);
                                                    LoggerUtilities.Info(
                                                        $"[Process] Uploaded ebook {doc.FormatType} for book '{existingBook.Title}' to {doc.DocumentPath}");
                                                }

                                                foreach (string warning in warnings)
                                                    LoggerUtilities.Warning($"[Process] {warning}");
                                            }
                                            catch (Exception ex)
                                            {
                                                LoggerUtilities.Warning(
                                                    $"[Process] Failed to upload ebook for book '{existingBook.Title}': {ex.Message}");
                                            }
                                    }
                                    else
                                    {
                                        BookDocument newDoc = new BookDocument
                                        {
                                            BookId = existingBook.Id,
                                            FormatType = bd.FormatType,
                                            DocumentPath = bd.DocumentPath,
                                            DocumentLink = bd.DocumentLink,
                                            CreatedAt = bd.CreatedAt,
                                            UpdatedAt = bd.UpdatedAt
                                        };
                                        _db.BookDocuments.Add(newDoc);
                                    }
                            }
                            // End of metadata-only check for document handling

                            updated++;
                            break;
                        }
                        case "Skip":
                            skipped++;
                            break;
                        case "Merge":
                        {
                            if (existingBook != null)
                            {
                                // MERGE: Keep existing book metadata, only merge categories and add new documents
                                LoggerUtilities.Info(
                                    $"[Process] Merging book '{existingBook.Title}' - adding new documents and categories");

                                // Merge category mappings (add new from Excel, keep existing from DB)
                                List<BookCategoryMapImportRow> categoryMaps2 = bookCategoryMapImportRows
                                    .Where(bcm =>
                                        $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}" ==
                                        bookKey)
                                    .ToList();

                                HashSet<int> existingCategoryIds = existingBook.BookCategoryMaps?
                                    .Select(bcm => bcm.BookCategoryId)
                                    .ToHashSet() ?? [];

                                foreach (BookCategoryMapImportRow bcm in categoryMaps2)
                                    if (!existingCategoryIds.Contains(bcm.BookCategoryId))
                                    {
                                        // Resolve category by ID, or fallback to name
                                        int? resolvedCategoryId = null;
                                        if (bcm.BookCategoryId > 0)
                                        {
                                            BookCategory? categoryById =
                                                await _db.BookCategories.FindAsync(bcm.BookCategoryId);
                                            if (categoryById != null) resolvedCategoryId = categoryById.Id;
                                        }

                                        if (resolvedCategoryId == null &&
                                            !string.IsNullOrWhiteSpace(bcm.BookCategoryName))
                                        {
                                            BookCategory? categoryByName = await _db.BookCategories
                                                .FirstOrDefaultAsync(bc =>
                                                    bc.Name.Trim().ToLower() == bcm.BookCategoryName.Trim().ToLower());
                                            if (categoryByName != null) resolvedCategoryId = categoryByName.Id;
                                        }

                                        if (resolvedCategoryId.HasValue)
                                        {
                                            _db.BookCategoryMaps.Add(new BookCategoryMap
                                            {
                                                BookId = existingBook.Id,
                                                BookCategoryId = resolvedCategoryId.Value
                                            });
                                            LoggerUtilities.Info(
                                                $"[Process] Added category mapping for merged book '{existingBook.Title}': CategoryId={resolvedCategoryId.Value}");
                                        }
                                    }

                                // For metadata-only imports, skip document merge operations
                                if (!isMetadataOnly)
                                {
                                    // Merge documents: Add new documents only (don't replace existing ones)
                                    List<BookDocumentImportRow> documents2 = bookDocumentImportRows
                                        .Where(bd =>
                                            $"{bd.BookTitle.Trim().ToLower()}|{bd.BookAuthorName.Trim().ToLower()}" ==
                                            bookKey)
                                        .ToList();

                                    // Build set of existing document paths to avoid duplicates
                                    HashSet<string> existingDocPaths = existingBookDocuments?
                                        .Select(bd => bd.DocumentPath.ToLower())
                                        .ToHashSet() ?? [];

                                    foreach (BookDocumentImportRow bdImport in documents2)
                                        // Only add if this DocumentPath doesn't already exist
                                        if (!existingDocPaths.Contains(bdImport.DocumentPath.ToLower()))
                                        {
                                            // Add new document with file upload if available
                                            if (extractedFiles != null &&
                                                !string.IsNullOrWhiteSpace(bdImport.DocumentPath))
                                            {
                                                string normalizedDocPath =
                                                    bdImport.DocumentPath.TrimStart('/').Replace('\\', '/');
                                                if (extractedFiles.TryGetValue(normalizedDocPath,
                                                        out string? tempDocPath) &&
                                                    System.IO.File.Exists(tempDocPath))
                                                    try
                                                    {
                                                        await using FileStream docFs = new(tempDocPath, FileMode.Open,
                                                            FileAccess.Read);
                                                        string originalFileName = Path.GetFileName(tempDocPath);
                                                        FormFile formFile = new(docFs, 0, docFs.Length, "file",
                                                            originalFileName)
                                                        {
                                                            Headers = new HeaderDictionary(),
                                                            ContentType = "application/octet-stream"
                                                        };

                                                        string customName =
                                                            Path.GetFileNameWithoutExtension(originalFileName);
                                                        customName = Regex.Replace(customName, @"^\d+_(EPUB|PDF)_", "");
                                                        customName = Regex.Replace(customName, @"_\d+_[0-9a-f-]+$", "");

                                                        (List<BookDocument> uploadedDocs, List<string> warnings) =
                                                            await _bookService.ProcessNewEbookUploadAsync(formFile,
                                                                existingBook.Id, customName);

                                                        foreach (BookDocument doc in uploadedDocs)
                                                        {
                                                            doc.CreatedAt = bdImport.CreatedAt;
                                                            doc.UpdatedAt = bdImport.UpdatedAt;
                                                            _db.BookDocuments.Add(doc);
                                                            LoggerUtilities.Info(
                                                                $"[Process] Added new ebook {doc.FormatType} for merged book '{existingBook.Title}' to {doc.DocumentPath}");
                                                        }

                                                        foreach (string warning in warnings)
                                                            LoggerUtilities.Warning($"[Process] {warning}");
                                                    }
                                                    catch (Exception ex)
                                                    {
                                                        LoggerUtilities.Warning(
                                                            $"[Process] Failed to add ebook for merged book '{existingBook.Title}': {ex.Message}");
                                                    }
                                            }
                                            else
                                            {
                                                // No extracted file, just add metadata
                                                BookDocument newDoc = new BookDocument
                                                {
                                                    BookId = existingBook.Id,
                                                    FormatType = bdImport.FormatType,
                                                    DocumentPath = bdImport.DocumentPath,
                                                    DocumentLink = bdImport.DocumentLink,
                                                    CreatedAt = bdImport.CreatedAt,
                                                    UpdatedAt = bdImport.UpdatedAt
                                                };
                                                _db.BookDocuments.Add(newDoc);
                                                LoggerUtilities.Info(
                                                    $"[Process] Added document metadata for merged book '{existingBook.Title}': {bdImport.DocumentPath}");
                                            }
                                        }
                                        else
                                        {
                                            LoggerUtilities.Info(
                                                $"[Process] Skipped duplicate document for merged book '{existingBook.Title}': {bdImport.DocumentPath}");
                                        }
                                }
                                // End of metadata-only check for document merge

                                updated++;
                            }
                            else
                            {
                                // No existing book, treat as add
                                Book newBook = new()
                                {
                                    Title = item.Title,
                                    AuthorName = item.AuthorName,
                                    Description = item.Description,
                                    PublicationYear = item.PublicationYear.HasValue
                                        ? DateOnly.FromDateTime(new DateTime(item.PublicationYear.Value, 1, 1))
                                        : null,
                                    LanguageCode = item.LanguageCode,
                                    CoverImagePath = item.CoverImagePath,
                                    CoverImageLink = item.CoverImageLink,
                                    UserId = resolvedUserId,
                                    Status = item.Status,
                                    CreatedAt = item.CreatedAt,
                                    UpdatedAt = item.UpdatedAt
                                };

                                _db.Books.Add(newBook);
                                await _db.SaveChangesAsync();

                                // Upload cover image if extracted files exist
                                if (extractedFiles != null && !string.IsNullOrWhiteSpace(newBook.CoverImagePath))
                                {
                                    string normalizedPath = newBook.CoverImagePath.TrimStart('/').Replace('\\', '/');
                                    if (extractedFiles.TryGetValue(normalizedPath, out string? tempFilePath) &&
                                        System.IO.File.Exists(tempFilePath))
                                        try
                                        {
                                            await using FileStream fs = new(tempFilePath, FileMode.Open,
                                                FileAccess.Read);
                                            await _dropboxHelper.CreateAsync(newBook.CoverImagePath, fs);
                                            string previewLink =
                                                await _dropboxHelper.GetPreviewLinkAsync(newBook.CoverImagePath);
                                            newBook.CoverImageLink = previewLink;
                                            LoggerUtilities.Info(
                                                $"[Process] Uploaded cover image for new book '{newBook.Title}' (Merge->Add) to {newBook.CoverImagePath}");
                                        }
                                        catch (Exception ex)
                                        {
                                            LoggerUtilities.Warning(
                                                $"[Process] Failed to upload cover image for new book '{newBook.Title}': {ex.Message}");
                                        }
                                }

                                // Auto create discussion forum for the book
                                Discussion discussion = new()
                                {
                                    Title = CreateDiscussionForumTitle(newBook.Title),
                                    Description =
                                        $"A dedicated forum for in-depth discussion of \"{newBook.Title}\", written by {newBook.AuthorName}. Explore key ideas, pose inquiries, and engage with fellow readers.",
                                    BookId = newBook.Id,
                                    CreatedAt = DateTime.Now.ToLocalTime(),
                                    UpdatedAt = DateTime.Now.ToLocalTime()
                                };
                                _db.Discussions.Add(discussion);

                                // Add category mappings
                                List<BookCategoryMapImportRow> categoryMaps3 = bookCategoryMapImportRows
                                    .Where(bcm =>
                                        $"{bcm.BookTitle.Trim().ToLower()}|{bcm.BookAuthorName.Trim().ToLower()}" ==
                                        bookKey)
                                    .ToList();

                                bool addedCategoryMapping2 = false;
                                foreach (BookCategoryMapImportRow bcm in categoryMaps3)
                                {
                                    // Resolve category: prefer matching by ID, fall back to matching by name
                                    int? resolvedCategoryId = null;
                                    if (bcm.BookCategoryId > 0)
                                    {
                                        BookCategory? catById = await _db.BookCategories.FindAsync(bcm.BookCategoryId);
                                        if (catById != null) resolvedCategoryId = catById.Id;
                                    }

                                    if (resolvedCategoryId == null && !string.IsNullOrWhiteSpace(bcm.BookCategoryName))
                                    {
                                        BookCategory? catByName = await _db.BookCategories
                                            .FirstOrDefaultAsync(bc =>
                                                bc.Name.Trim().ToLower() == bcm.BookCategoryName.Trim().ToLower());
                                        if (catByName != null) resolvedCategoryId = catByName.Id;
                                    }

                                    if (resolvedCategoryId.HasValue)
                                        _db.BookCategoryMaps.Add(new BookCategoryMap
                                        {
                                            BookId = newBook.Id,
                                            BookCategoryId = resolvedCategoryId.Value
                                        });
                                    if (resolvedCategoryId.HasValue) addedCategoryMapping2 = true;
                                }

                                // If no mapping found, fall back to General category for a newly added book
                                if (!addedCategoryMapping2)
                                {
                                    BookCategory? generalCategory = await _bookCategoryService.GetGeneralCategory();
                                    if (generalCategory != null)
                                        _db.BookCategoryMaps.Add(new BookCategoryMap
                                        {
                                            BookId = newBook.Id,
                                            BookCategoryId = generalCategory.Id
                                        });
                                }

                                // Add book documents
                                List<BookDocumentImportRow> documents3 = bookDocumentImportRows
                                    .Where(bd =>
                                        $"{bd.BookTitle.Trim().ToLower()}|{bd.BookAuthorName.Trim().ToLower()}" ==
                                        bookKey)
                                    .ToList();

                                foreach (BookDocumentImportRow bd in documents3)
                                {
                                    BookDocument newDoc = new BookDocument
                                    {
                                        BookId = newBook.Id,
                                        FormatType = bd.FormatType,
                                        DocumentPath = bd.DocumentPath,
                                        DocumentLink = bd.DocumentLink,
                                        CreatedAt = bd.CreatedAt,
                                        UpdatedAt = bd.UpdatedAt
                                    };

                                    // Upload ebook if extracted files exist
                                    if (extractedFiles != null && !string.IsNullOrWhiteSpace(newDoc.DocumentPath))
                                    {
                                        string normalizedDocPath =
                                            newDoc.DocumentPath.TrimStart('/').Replace('\\', '/');
                                        if (extractedFiles.TryGetValue(normalizedDocPath, out string? tempDocPath) &&
                                            System.IO.File.Exists(tempDocPath))
                                            try
                                            {
                                                await using FileStream docFs = new(tempDocPath, FileMode.Open,
                                                    FileAccess.Read);
                                                // Ensure path starts with / for Dropbox API
                                                string dropboxDocPath = newDoc.DocumentPath.StartsWith("/")
                                                    ? newDoc.DocumentPath
                                                    : "/" + newDoc.DocumentPath;
                                                await _dropboxHelper.CreateAsync(dropboxDocPath, docFs);
                                                string docPreviewLink =
                                                    await _dropboxHelper.GetPreviewLinkAsync(dropboxDocPath);
                                                newDoc.DocumentLink = docPreviewLink;
                                                LoggerUtilities.Info(
                                                    $"[Process] Uploaded ebook {newDoc.FormatType} for new book '{newBook.Title}' (Merge->Add) to {dropboxDocPath}");
                                            }
                                            catch (Exception ex)
                                            {
                                                LoggerUtilities.Warning(
                                                    $"[Process] Failed to upload ebook for new book '{newBook.Title}': {ex.Message}");
                                            }
                                    }

                                    _db.BookDocuments.Add(newDoc);
                                }

                                added++;
                            }

                            break;
                        }
                    }
                }
                catch (Exception)
                {
                    unresolved++;
                }

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();
            LoggerUtilities.Info("[End] transaction committed. added=" + added + ", updated=" + updated + ", skipped=" +
                                 skipped + ", unresolved=" + unresolved);

            // Delete old files from Dropbox after successful transaction
            foreach (var docInfo in documentsToDelete)
                try
                {
                    BookDocument tmpDoc = new()
                    {
                        Id = docInfo.Id,
                        BookId = docInfo.BookId,
                        DocumentPath = docInfo.DocumentPath,
                        DocumentLink = docInfo.DocumentLink ?? string.Empty
                    };
                    await _bookService.DeleteBookDocumentAsync(tmpDoc, false);
                    LoggerUtilities.Info(
                        $"[Process] Deleted old ebook file for overwritten book: {docInfo.DocumentPath}");
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning(
                        $"[Process] Failed to delete old ebook file {docInfo.DocumentPath}: {ex.Message}");
                }

            foreach (string coverPath in coverImagesToDelete)
                try
                {
                    await _dropboxService.DeleteImageAsync(coverPath);
                    LoggerUtilities.Info($"[Process] Deleted old cover image for overwritten book: {coverPath}");
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning(
                        $"[Process] Failed to delete old cover image {coverPath}: {ex.Message}");
                }

            // Cleanup temp folder after successful import
            if (!string.IsNullOrWhiteSpace(tempFolder) && Directory.Exists(tempFolder))
                try
                {
                    Directory.Delete(tempFolder, true);
                    LoggerUtilities.Info($"[Process] Cleaned up temp folder: {tempFolder}");
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning($"[Process] Failed to delete temp folder {tempFolder}: {ex.Message}");
                }
        }
        catch (Exception)
        {
            await transaction.RollbackAsync();
            LoggerUtilities.Error("[End] failed and rolled back.");
            TempData[SweetAlert.Danger] = "An error occurred during import. Changes have been rolled back.";

            // Cleanup temp folder on failure too
            if (!string.IsNullOrWhiteSpace(tempFolder) && Directory.Exists(tempFolder))
                try
                {
                    Directory.Delete(tempFolder, true);
                    LoggerUtilities.Info($"[Process] Cleaned up temp folder after rollback: {tempFolder}");
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Warning($"[Process] Failed to delete temp folder {tempFolder}: {ex.Message}");
                }

            return PartialView("_ImportSummary",
                new BookImportSummaryVm { Added = 0, Updated = 0, Skipped = 0, Unresolved = model.Items.Count });
        }

        BookImportSummaryVm bookImportSummaryVm = new()
        {
            Added = added,
            Updated = updated,
            Skipped = skipped,
            Unresolved = unresolved
        };
        return PartialView("_ImportSummary", bookImportSummaryVm);
    }

    /// <summary>
    ///     Display export UI to select books and prepare a paginated result
    ///     listing. Selected items can then be exported as CSV inside a ZIP.
    /// </summary>
    public async Task<IActionResult> Export(string? search, string? sort, string? dir, string? filter, int page = 1)
    {
        LoggerUtilities.Info("[Start] started. search=" + search + ", sort=" + sort + ", dir=" + dir + ", filter=" +
                             filter + ", page=" + page);
        List<BookCategory> bookCategories = await _bookCategoryService.GetAllCategories();
        SelectList bookCategorySelectList = new(bookCategories, "Id", "Name");
        ViewBag.BookCategories = bookCategorySelectList;
        ViewBag.Languages = LanguageHelper.ToLanguageSelectList();

        ViewBag.Search = search = search?.Trim() ?? "";
        ViewBag.Filter = filter = filter?.Trim() ?? "";

        // Parse filter: C:5 for category, L:en for language
        int filterId = 0;
        string? languageCode = null;
        bool hasCategoryFilter = false;
        bool hasLanguageFilter = false;

        if (!string.IsNullOrWhiteSpace(filter))
        {
            if (filter.StartsWith("C:") && int.TryParse(filter.Substring(2), out filterId))
            {
                hasCategoryFilter = true;
            }
            else if (filter.StartsWith("L:"))
            {
                languageCode = filter.Substring(2);
                hasLanguageFilter = true;
            }
        }

        List<Book> searched = await _db.Books
            .Include(b => b.BookCategoryMaps)
            .Include(b => b.BookCategories)
            .Where(b => (b.Title.Contains(search) ||
                         b.AuthorName!.Contains(search) ||
                         b.PublicationYear.ToString()!.Contains(search)) &&
                        b.BookCategoryMaps != null &&
                        (!hasCategoryFilter || b.BookCategoryMaps.Any(bcm => bcm.BookCategoryId == filterId)) &&
                        (!hasLanguageFilter || b.LanguageCode.ToString() == languageCode) &&
                        b.Status == nameof(BookStatusType.Available))
            .ToListAsync();

        if (page < 1) return RedirectToAction(null, new { search, sort, dir, filter, page = 1 });

        IPagedList<Book> model = SortAndPaginateBooks(searched, sort, dir, page);

        if (page > model.PageCount && model.PageCount > 0)
            return RedirectToAction(null, new { search, sort, dir, filter, page = model.PageCount });

        if (Request.IsAjax())
        {
            LoggerUtilities.Info("[Process] returning partial view _Export for page " + page);
            return PartialView("_Export", model);
        }

        LoggerUtilities.Info("[End] completed. page=" + page + ", totalItems=" + model.TotalItemCount);
        return View(model);
    }

    /// <summary>
    ///     Generate an AI-assisted book description for the specified book
    ///     using the <see cref="IBookService" /> (which uses Gemini or
    ///     another AI provider) and return the result as JSON.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> GenerateDescription(int id)
    {
        LoggerUtilities.Info("[Start] started. bookId=" + id);
        try
        {
            if (!ModelState.IsValid || id <= 0)
                return Json(new { success = false, message = "Invalid book ID." });

            // Use BookService to generate description using Gemini
            (string? generatedDescription, string? error) = await _bookService.GenerateDescriptionAsync(id);

            if (!string.IsNullOrEmpty(error))
                return Json(new { success = false, message = error });

            LoggerUtilities.Info("[End] succeeded. bookId=" + id);
            return Json(new { success = true, description = generatedDescription });
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error("[End] failed. bookId=" + id + ". Error: " + ex.Message);
            return Json(new { success = false, message = $"An error occurred: {ex.Message}" });
        }
    }

    /// <summary>
    ///     Export selected (or all) books, including category maps and
    ///     document metadata, into three CSV files (Books.csv,
    ///     BookCategoryMaps.csv, BookDocuments.csv) and bundle them into a
    ///     downloadable ZIP archive.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> ExportZip(string[] ids, string? exportType)
    {
        LoggerUtilities.Info("[Start] started. idsCount=" + (ids?.Length ?? 0));
        // Requirement: Export to CSV the table have, Book, BookCategoryMap and BookDocument, after that zip 3 of the CSV file and download
        List<Book> books;
        List<BookCategoryMap> bookCategoryMaps;
        List<BookDocument> bookDocuments;

        if (ids != null && ids.Length > 0)
        {
            List<int> bookIds = ids.Select(id => int.Parse(id)).ToList();

            books = await _db.Books
                .Where(b => bookIds.Contains(b.Id))
                .ToListAsync();

            bookCategoryMaps = await _db.BookCategoryMaps
                .Where(bcm => bookIds.Contains(bcm.BookId))
                .ToListAsync();

            bookDocuments = await _db.BookDocuments
                .Where(bd => bookIds.Contains(bd.BookId))
                .ToListAsync();
        }
        else
        {
            books = await _db.Books.ToListAsync();
            bookCategoryMaps = await _db.BookCategoryMaps.ToListAsync();
            bookDocuments = await _db.BookDocuments.ToListAsync();
        }

        using MemoryStream zipStream = new();
        using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Create, true))
        {
            // Build a lookup of user emails to include in the exported CSV so imports can resolve owners by email.
            Dictionary<int, string> userEmailsById = new Dictionary<int, string>();
            List<int> userIds = books.Where(b => b.UserId.HasValue).Select(b => b.UserId!.Value).Distinct().ToList();
            if (userIds.Count > 0)
            {
                List<User> users = await _db.Users.Where(u => userIds.Contains(u.Id)).ToListAsync();
                userEmailsById = users.ToDictionary(u => u.Id, u => u.Email ?? string.Empty);
            }

            // Books CSV
            ZipArchiveEntry booksEntry = zipArchive.CreateEntry("Books.csv");
            await using (Stream entryStream = booksEntry.Open())
            await using (StreamWriter streamWriter = new(entryStream, Encoding.UTF8))
            await using (CsvWriter csvWriter = new(streamWriter, CultureInfo.InvariantCulture))
            {
                // Write header (removed Id)
                csvWriter.WriteField("Title");
                csvWriter.WriteField("AuthorName");
                csvWriter.WriteField("Description");
                csvWriter.WriteField("PublicationYear");
                csvWriter.WriteField("LanguageCode");
                csvWriter.WriteField("CoverImagePath");
                csvWriter.WriteField("CoverImageLink");
                csvWriter.WriteField("Status");
                csvWriter.WriteField("UserId");
                csvWriter.WriteField("UserEmail");
                csvWriter.WriteField("CreatedAt");
                csvWriter.WriteField("UpdatedAt");
                await csvWriter.NextRecordAsync();

                foreach (Book book in books)
                {
                    csvWriter.WriteField(book.Title);
                    csvWriter.WriteField(book.AuthorName);
                    csvWriter.WriteField(book.Description);
                    csvWriter.WriteField(book.PublicationYear?.Year.ToString() ?? string.Empty);
                    csvWriter.WriteField(book.LanguageCode);
                    csvWriter.WriteField(book.CoverImagePath);
                    csvWriter.WriteField(book.CoverImageLink);
                    csvWriter.WriteField(book.Status);
                    csvWriter.WriteField(book.UserId?.ToString() ?? string.Empty);
                    // Write the user's email if available (helps import resolve owner by email)
                    string userEmail = string.Empty;
                    if (book.UserId.HasValue && userEmailsById.TryGetValue(book.UserId.Value, out string? email))
                        userEmail = email;
                    csvWriter.WriteField(userEmail);
                    csvWriter.WriteField(book.CreatedAt.ToUniversalTime().ToString("o"));
                    csvWriter.WriteField(book.UpdatedAt.ToUniversalTime().ToString("o"));
                    await csvWriter.NextRecordAsync();
                }

                await csvWriter.FlushAsync();
                await streamWriter.FlushAsync();
            }

            // Similar code for BookCategoryMaps and BookDocuments...
            // BookCategoryMaps CSV
            ZipArchiveEntry bcmEntry = zipArchive.CreateEntry("BookCategoryMaps.csv");
            await using (Stream entryStream = bcmEntry.Open())
            await using (StreamWriter streamWriter = new(entryStream, Encoding.UTF8))
            await using (CsvWriter csvWriter = new(streamWriter, CultureInfo.InvariantCulture))
            {
                // Write header (use BookTitle and BookAuthorName instead of BookId)
                csvWriter.WriteField("BookTitle");
                csvWriter.WriteField("BookAuthorName");
                csvWriter.WriteField("BookCategoryId");
                csvWriter.WriteField("BookCategoryName");
                await csvWriter.NextRecordAsync();

                foreach (BookCategoryMap bcm in bookCategoryMaps)
                {
                    Book? book = books.FirstOrDefault(b => b.Id == bcm.BookId);
                    if (book != null)
                    {
                        csvWriter.WriteField(book.Title);
                        csvWriter.WriteField(book.AuthorName);
                        csvWriter.WriteField(bcm.BookCategoryId);
                        // Write category name to help imports on different instances
                        string categoryName = string.Empty;
                        BookCategory? cat = await _db.BookCategories.FindAsync(bcm.BookCategoryId);
                        if (cat != null) categoryName = cat.Name;
                        csvWriter.WriteField(categoryName);
                        await csvWriter.NextRecordAsync();
                    }
                }

                await csvWriter.FlushAsync();
                await streamWriter.FlushAsync();
            }

            // BookDocuments CSV
            ZipArchiveEntry bdEntry = zipArchive.CreateEntry("BookDocuments.csv");
            await using (Stream entryStream = bdEntry.Open())
            await using (StreamWriter streamWriter = new(entryStream, Encoding.UTF8))
            await using (CsvWriter csvWriter = new(streamWriter, CultureInfo.InvariantCulture))
            {
                // Write header (use BookTitle and BookAuthorName instead of BookId, removed Id)
                csvWriter.WriteField("BookTitle");
                csvWriter.WriteField("BookAuthorName");
                csvWriter.WriteField("FormatType");
                csvWriter.WriteField("DocumentPath");
                csvWriter.WriteField("DocumentLink");
                csvWriter.WriteField("CreatedAt");
                csvWriter.WriteField("UpdatedAt");
                await csvWriter.NextRecordAsync();

                foreach (BookDocument bd in bookDocuments)
                {
                    Book? book = books.FirstOrDefault(b => b.Id == bd.BookId);
                    if (book != null)
                    {
                        csvWriter.WriteField(book.Title);
                        csvWriter.WriteField(book.AuthorName);
                        csvWriter.WriteField(bd.FormatType);
                        csvWriter.WriteField(bd.DocumentPath);
                        csvWriter.WriteField(bd.DocumentLink);
                        csvWriter.WriteField(bd.CreatedAt.ToUniversalTime().ToString("o"));
                        csvWriter.WriteField(bd.UpdatedAt.ToUniversalTime().ToString("o"));
                        await csvWriter.NextRecordAsync();
                    }
                }

                await csvWriter.FlushAsync();
                await streamWriter.FlushAsync();
            }
        }

        // Generate integrity manifest for export verification
        zipStream.Position = 0;
        using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Update, true))
        {
            LoggerUtilities.Info("[Process] Generating export integrity manifest");

            // Compute SHA256 hash for each CSV file
            Dictionary<string, string> fileHashes = new();
            string[] csvFiles = { "Books.csv", "BookCategoryMaps.csv", "BookDocuments.csv" };

            foreach (string csvFile in csvFiles)
            {
                ZipArchiveEntry? entry = zipArchive.GetEntry(csvFile);
                if (entry != null)
                {
                    await using Stream entryStream = entry.Open();
                    using SHA256 sha256 = SHA256.Create();
                    byte[] hashBytes = await sha256.ComputeHashAsync(entryStream);
                    string hash = Convert.ToBase64String(hashBytes);
                    fileHashes[csvFile] = hash;
                    LoggerUtilities.Info($"[Process] Computed hash for {csvFile}: {hash.Substring(0, 16)}...");
                }
            }

            // Generate export timestamp
            DateTime exportTimestamp = DateTime.UtcNow;

            // Compute integrity key: SHA256(hash1 + hash2 + hash3 + timestamp)
            string combinedData = string.Join("|",
                fileHashes.GetValueOrDefault("Books.csv", ""),
                fileHashes.GetValueOrDefault("BookCategoryMaps.csv", ""),
                fileHashes.GetValueOrDefault("BookDocuments.csv", ""),
                exportTimestamp.ToString("o"));

            using SHA256 sha256Final = SHA256.Create();
            byte[] integrityBytes = sha256Final.ComputeHash(Encoding.UTF8.GetBytes(combinedData));
            string integrityKey = Convert.ToBase64String(integrityBytes);

            // Create manifest object
            var manifest = new
            {
                Version = "1.0",
                ExportTimestamp = exportTimestamp.ToString("o"),
                ExportType = string.IsNullOrWhiteSpace(exportType) ? "metadata" : exportType,
                BookCount = books.Count,
                FileHashes = fileHashes,
                IntegrityKey = integrityKey
            };

            // Write manifest to ZIP
            ZipArchiveEntry manifestEntry = zipArchive.CreateEntry(".epiread_manifest.json");
            await using (Stream manifestStream = manifestEntry.Open())
            await using (StreamWriter manifestWriter = new(manifestStream, Encoding.UTF8))
            {
                string manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                await manifestWriter.WriteAsync(manifestJson);
                await manifestWriter.FlushAsync();
            }

            LoggerUtilities.Info(
                $"[Process] Export manifest created with integrity key: {integrityKey.Substring(0, 16)}...");
        }

        // If export type requests full export, append referenced files (cover images & ebook files)
        if (!string.IsNullOrWhiteSpace(exportType) &&
            exportType.Equals("full", StringComparison.OrdinalIgnoreCase))
        {
            LoggerUtilities.Info("[Process] Full export: including cover images and ebook files");

            // Reset stream position so we can reopen the archive in Update mode.
            zipStream.Position = 0;
            using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Update, true))
            {
                using HttpClient httpClient = new();

                // Avoid duplicate files in ZIP
                HashSet<string> addedPaths = new(StringComparer.OrdinalIgnoreCase);

                // Add cover images
                foreach (Book book in books)
                    try
                    {
                        string? imagePath = book.CoverImagePath;
                        if (string.IsNullOrWhiteSpace(imagePath))
                            continue;

                        string zipEntryName = imagePath.TrimStart('/').Replace('\\', '/');
                        if (addedPaths.Contains(zipEntryName))
                            continue;
                        ZipArchiveEntry imageEntry = zipArchive.CreateEntry(zipEntryName);
                        await using (Stream entryStream = imageEntry.Open())
                        await using (Stream remoteStream = await _dropboxHelper.OpenReadAsync(imagePath))
                        {
                            await remoteStream.CopyToAsync(entryStream);
                        }

                        addedPaths.Add(zipEntryName);
                    }
                    catch (Exception ex)
                    {
                        LoggerUtilities.Error(ex, $"[Process] Error fetching cover image for bookId={book.Id}");
                    }

                // Add ebook files
                foreach (BookDocument bd in bookDocuments)
                    try
                    {
                        // Skip only when both document link and path are missing. We want to
                        // process fallbacks when documentLink is missing but local preview is available.
                        if (string.IsNullOrWhiteSpace(bd.DocumentLink) && string.IsNullOrWhiteSpace(bd.DocumentPath))
                            continue;

                        if (!string.IsNullOrWhiteSpace(bd.DocumentPath))
                        {
                            try
                            {
                                string zipEntryName = bd.DocumentPath.TrimStart('/').Replace('\\', '/');
                                if (!addedPaths.Contains(zipEntryName))
                                {
                                    ZipArchiveEntry docEntry = zipArchive.CreateEntry(zipEntryName);
                                    await using (Stream entryStream = docEntry.Open())
                                    await using (Stream remoteStream = await _dropboxHelper.OpenReadAsync(bd.DocumentPath))
                                    {
                                        await remoteStream.CopyToAsync(entryStream);
                                    }

                                    addedPaths.Add(zipEntryName);
                                }

                                // success, go to next document
                                continue;
                            }
                            catch (FileNotFoundException)
                            {
                                LoggerUtilities.Warning($"[Process] Storage file missing for documentId={bd.Id}");
                            }
                        }

                        // Fallback: if there's no document link we try BookService to fetch a preview (cached) file
                        if (!string.IsNullOrWhiteSpace(bd.DocumentPath))
                        {
                            string extension = Path.GetExtension(bd.DocumentPath).ToLowerInvariant();
                            string? localFile = null;

                            if (extension == ".epub")
                            {
                                (localFile, string? error) = await _bookService.GetOrDownloadEpubForPreviewAsync(bd);
                                if (!string.IsNullOrWhiteSpace(error))
                                    LoggerUtilities.Warning(
                                        $"[Process] Fallback EPUB preview failed for documentId={bd.Id}; {error}");
                            }
                            else if (extension == ".pdf")
                            {
                                (localFile, string? error) = await _bookService.GetOrDownloadPdfForPreviewAsync(bd);
                                if (!string.IsNullOrWhiteSpace(error))
                                    LoggerUtilities.Warning(
                                        $"[Process] Fallback PDF preview failed for documentId={bd.Id}; {error}");
                            }

                            if (!string.IsNullOrWhiteSpace(localFile))
                                if (System.IO.File.Exists(localFile))
                                {
                                    string zipEntryName = bd.DocumentPath.TrimStart('/').Replace('\\', '/');
                                    if (!addedPaths.Contains(zipEntryName))
                                    {
                                        ZipArchiveEntry docEntry = zipArchive.CreateEntry(zipEntryName);
                                        await using (Stream entryStream = docEntry.Open())
                                        await using (FileStream localFs = new(localFile, FileMode.Open,
                                                         FileAccess.Read))
                                        {
                                            await localFs.CopyToAsync(entryStream);
                                        }

                                        addedPaths.Add(zipEntryName);
                                    }
                                }
                        }
                        // success path is already handled above; no-op — continue handled
                    }
                    catch (Exception ex)
                    {
                        LoggerUtilities.Error(ex, $"[Process] Error fetching documentId={bd.Id}");
                    }
            }
        }

        zipStream.Position = 0;
        string zipFileName = $"Books_Export_{DateTime.Now.ToLocalTime():yyyyMMdd_HHmmss}.zip";
        LoggerUtilities.Info("[End] completed. fileName=" + zipFileName + ", records=" + books.Count);
        return File(zipStream.ToArray(), "application/zip", zipFileName);
    }

    /// <summary>
    ///     Sort the provided books list according to the requested sort
    ///     column and direction, then paginate the results for the UI.
    /// </summary>
    private IPagedList<Book> SortAndPaginateBooks(List<Book>? searched, string? sort, string? dir,
        int page = 1)
    {
        // Ensure searched is not null (method may be called with null from callers)
        searched = searched ?? new List<Book>();
        // (2) Sorting --------------------------
        ViewBag.Sort = sort;
        ViewBag.Dir = dir;

        Func<Book, object> fn = sort switch
        {
            "Title" => s => s.Title,
            "Author Name" => s => s.AuthorName,
            "Year" => s => s.PublicationYear.ToString() ?? "N/A",
            "Language" => s => s.LanguageCode,
            _ => s => s.Id
        };

        IOrderedEnumerable<Book> sorted = dir == "des"
            ? searched.AsEnumerable().OrderByDescending(fn)
            : searched.AsEnumerable().OrderBy(fn);

        // (3) Paging ---------------------------
        IPagedList<Book> pagedList = sorted.ToPagedList(page, 10);

        return pagedList;
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
