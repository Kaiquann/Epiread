using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using Aspose.Pdf;
using CommonUtilities.Utilities.Database;
using EpiRead.Models.StoredProcedure;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace EpiRead.Services;

/// <summary>
///     Provides helper methods for validating, saving, converting, and managing ebook files and cover images.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class BookService : IBookService
{
    private const int MaxEbookSize = 50 * 1024 * 1024; // 50MB
    private static readonly string[] AllowedExtensions = [".epub", ".pdf"];
    private static readonly Regex FileNameRegex = new(@"^.+\.(epub|pdf)$", RegexOptions.IgnoreCase);
    private readonly IConfiguration _configuration;
    private readonly DB _db;
    private readonly IDropboxHelper _dropboxHelper;
    private readonly IDropboxService _dropboxService;
    private readonly IWebHostEnvironment _environment;
    private readonly IGeminiHelper _geminiHelper;

    public BookService(IWebHostEnvironment environment, IDropboxHelper dropboxHelper, DB db, IGeminiHelper geminiHelper,
        IDropboxService dropboxService, IConfiguration configuration)
    {
        _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        _dropboxHelper = dropboxHelper ?? throw new ArgumentNullException(nameof(dropboxHelper));
        _dropboxService = dropboxService ?? throw new ArgumentNullException(nameof(dropboxService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _geminiHelper = geminiHelper ?? throw new ArgumentNullException(nameof(geminiHelper));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    ///     Exposes the web root path for local file operations.
    /// </summary>
    public string WebRootPath => _environment.WebRootPath;

    // Database CRUD operations
    /// <summary>
    ///     Retrieves a book by its ID with related category mappings and categories.
    /// </summary>
    /// <param name="bookId">The ID of the book to retrieve.</param>
    /// <returns>The Book entity if found; otherwise, null.</returns>
    public async Task<Book?> GetBookByIdAsync(int bookId)
    {
        LoggerUtilities.Info($"[Start] bookId={bookId}");
        // Database operation: retrieve book with related categories
        Book? book = await _db.Books
            .Include(b => b.BookCategoryMaps)!
            .ThenInclude(bcm => bcm.BookCategory)
            .FirstOrDefaultAsync(b => b.Id == bookId);
        if (book == null)
            LoggerUtilities.Warning($"[Process] Book not found for bookId={bookId}");
        else
            LoggerUtilities.Info($"[Process] Retrieved book for bookId={bookId}");
        LoggerUtilities.Info($"[End] bookId={bookId}");
        return book;
    }

    /// <summary>
    ///     Searches and filters books based on search criteria, filters, and status.
    /// </summary>
    /// <param name="search">Search term to match against title, author name, or publication year.</param>
    /// <param name="filterId">Category filter ID (0 for no category filter).</param>
    /// <param name="languageCode">Language filter code (null for no language filter).</param>
    /// <param name="status">Book status to filter by (e.g., "Available", "Trashed").</param>
    /// <returns>List of books matching the filter criteria.</returns>
    public async Task<List<Book>> GetFilteredBooksAsync(string search, int filterId, string? languageCode,
        string status)
    {
        LoggerUtilities.Info(
            $"[Start] search={search}, filterId={filterId}, languageCode={languageCode}, status={status}");
        // Parse filter flags
        bool hasCategoryFilter = filterId > 0;
        bool hasLanguageFilter = !string.IsNullOrWhiteSpace(languageCode);

        // Build query
        List<Book> filtered = await _db.Books
            .Include(b => b.BookCategoryMaps)
            .Include(b => b.BookCategories)
            .Where(b => (b.Title.Contains(search) ||
                         b.AuthorName.Contains(search) ||
                         b.PublicationYear.ToString()!.Contains(search)) &&
                        b.BookCategoryMaps != null &&
                        (!hasCategoryFilter || b.BookCategoryMaps.Any(bcm => bcm.BookCategoryId == filterId)) &&
                        (!hasLanguageFilter || b.LanguageCode.ToString() == languageCode) &&
                        b.Status == status)
            .ToListAsync();

        LoggerUtilities.Info($"[Process] Retrieved {filtered.Count} filtered books");
        LoggerUtilities.Info("[End] Completed");
        return filtered;
    }

    /// <summary>
    ///     Creates a new book with category mappings in the database.
    /// </summary>
    /// <param name="book">The Book entity to create.</param>
    /// <param name="categoryIds">List of category IDs to associate with the book.</param>
    /// <returns>Tuple: (created Book entity if successful, error message if any).</returns>
    public async Task<(Book? book, string? error)> CreateBookAsync(Book book, List<int> categoryIds)
    {
        LoggerUtilities.Info(
            $"[Start] title={book.Title}, categoryIds={string.Join(',', categoryIds)}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Add book to database
            await _db.Books.AddAsync(book);
            await _db.SaveChangesAsync();

            // Add category mappings
            if (categoryIds.Count > 0)
            {
                foreach (int categoryId in categoryIds)
                {
                    BookCategoryMap bookCategoryMap = new()
                    {
                        BookId = book.Id,
                        BookCategoryId = categoryId
                    };
                    await _db.BookCategoryMaps.AddAsync(bookCategoryMap);
                }

                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            LoggerUtilities.Info($"[Process] Successfully created book with Id={book.Id}");
            LoggerUtilities.Info($"[End] title={book.Title}");
            return (book, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            LoggerUtilities.Error(ex, $"[Danger] Error creating book title={book.Title}");
            return (null, $"Failed to create book: {ex.Message}");
        }
        finally
        {
            // End already logged on success or error path; keep a minimal end marker
            LoggerUtilities.Info($"[End] title={book.Title}");
        }
    }

    /// <summary>
    ///     Updates an existing book and its category mappings.
    /// </summary>
    /// <param name="book">The Book entity to update.</param>
    /// <param name="categoryIds">Updated list of category IDs to associate with the book.</param>
    /// <returns>Tuple: (updated Book entity if successful, error message if any).</returns>
    public async Task<(Book? book, string? error)> UpdateBookAsync(Book book, List<int> categoryIds)
    {
        LoggerUtilities.Info($"[Start] bookId={book.Id}, title={book.Title}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Update book
            _db.Books.Update(book);
            await _db.SaveChangesAsync();

            // Remove existing category mappings
            List<BookCategoryMap> existingMappings = await _db.BookCategoryMaps
                .Where(bcm => bcm.BookId == book.Id)
                .ToListAsync();
            _db.BookCategoryMaps.RemoveRange(existingMappings);
            await _db.SaveChangesAsync();

            // Add new category mappings
            if (categoryIds.Count > 0)
            {
                foreach (int categoryId in categoryIds)
                {
                    BookCategoryMap bookCategoryMap = new()
                    {
                        BookId = book.Id,
                        BookCategoryId = categoryId
                    };
                    await _db.BookCategoryMaps.AddAsync(bookCategoryMap);
                }

                await _db.SaveChangesAsync();
            }

            await transaction.CommitAsync();
            LoggerUtilities.Info($"[Process] Successfully updated book with Id={book.Id}");
            LoggerUtilities.Info($"[End] bookId={book.Id}");
            return (book, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            LoggerUtilities.Error(ex, $"[Danger] Error updating bookId={book.Id}");
            return (null, $"Failed to update book: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] bookId={book.Id}");
        }
    }

    /// <summary>
    ///     Soft deletes (trashes) one or more books by setting their status to "Trashed".
    /// </summary>
    /// <param name="bookIds">Array of book IDs to trash.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    public async Task<(bool success, string? error)> TrashBooksAsync(int[] bookIds)
    {
        LoggerUtilities.Info($"[Start] bookIds={string.Join(',', bookIds)}");
        if (bookIds == null || bookIds.Length == 0)
        {
            LoggerUtilities.Warning("[Process] No bookIds provided to TrashBooksAsync");
            LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds ?? Array.Empty<int>())}");
            return (false, "No bookIds provided.");
        }

        try
        {
            // Use a set-based update to avoid loading entities into memory and reduce EF change-tracking overhead.
            int updated = await _db.Books
                .Where(b => bookIds.Contains(b.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(b => b.Status, _ => nameof(BookStatusType.Trashed)));

            if (updated == 0)
            {
                LoggerUtilities.Warning($"[Process] No books found to trash for bookIds: {string.Join(',', bookIds)}");
                LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds)}");
                return (false, "No books found to trash.");
            }

            LoggerUtilities.Info($"[Process] Successfully trashed {updated} books");
            LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds)}");
            return (true, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error trashing bookIds={string.Join(',', bookIds)}");
            return (false, $"Failed to trash books: {ex.Message}");
        }
    }

    /// <summary>
    ///     Recovers one or more trashed books by setting their status back to "Available".
    /// </summary>
    /// <param name="bookIds">Array of book IDs to recover.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    public async Task<(bool success, string? error)> RecoverBooksAsync(int[] bookIds)
    {
        LoggerUtilities.Info($"[Start] bookIds={string.Join(',', bookIds)}");
        await using IDbContextTransaction transaction = await _db.Database.BeginTransactionAsync();
        try
        {
            // Find all books to recover
            List<Book> booksToRecover = await _db.Books
                .Where(b => bookIds.Contains(b.Id))
                .ToListAsync();

            if (booksToRecover.Count == 0)
            {
                LoggerUtilities.Warning(
                    $"[Process] No books found to recover for bookIds: {string.Join(',', bookIds)}");
                return (false, "No books found to recover.");
            }

            // Update status to Available
            foreach (Book book in booksToRecover) book.Status = nameof(BookStatusType.Available);

            _db.Books.UpdateRange(booksToRecover);
            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            LoggerUtilities.Info($"[Process] Successfully recovered {booksToRecover.Count} books");
            LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds)}");
            return (true, null);
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            LoggerUtilities.Error(ex,
                $"[Danger] Error recovering bookIds={string.Join(',', bookIds)}");
            return (false, $"Failed to recover books: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds)}");
        }
    }

    /// <summary>
    ///     Permanently deletes one or more books and their associated documents and category mappings.
    /// </summary>
    /// <param name="bookIds">Array of book IDs to delete.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    public async Task<(bool success, string? error)> DeleteBooksAsync(int[] bookIds)
    {
        LoggerUtilities.Info($"[Start] bookIds={string.Join(',', bookIds)}");
        try
        {
            // Get connection string from appsettings.json
            string? connectionString = _configuration.GetConnectionString("DBConnection");
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                LoggerUtilities.Error("[Danger] Database connection string 'DBConnection' not found");
                return (false, "Database connection not configured.");
            }

            // Prepare stored procedure parameters
            string bookIdsParam = string.Join(",", bookIds);
            List<SqlParameter> parameters = [new("@BookIds", bookIdsParam)];

            // Execute stored procedure using DatabaseUtilities
            LoggerUtilities.Info($"[Process] Executing sp_DeleteBooks for bookIds={bookIdsParam}");
            List<DeleteBookResult> results = await DatabaseUtilities.ExecuteAsync<DeleteBookResult>(
                connectionString,
                "sp_DeleteBooks",
                parameters
            );

            // Check if stored procedure execution was successful
            if (results.Count == 0)
            {
                LoggerUtilities.Error("[Danger] Stored procedure returned no results");
                return (false, "Failed to delete books: No response from database.");
            }

            DeleteBookResult firstResult = results[0];
            if (firstResult.Success == 0)
            {
                LoggerUtilities.Error($"[Danger] Stored procedure failed: {firstResult.ErrorMessage}");
                return (false, firstResult.ErrorMessage ?? "Failed to delete books.");
            }

            LoggerUtilities.Info("[Process] Stored procedure executed successfully, cleaning up external files");

            // Filter results to get file paths for cleanup
            List<DeleteBookResult> filesToDelete = results.Where(r => !string.IsNullOrWhiteSpace(r.FileType)).ToList();
            int documentDeleteCount = 0;
            int coverImageDeleteCount = 0;

            // Delete external resources from Dropbox
            foreach (DeleteBookResult fileInfo in filesToDelete)
                try
                {
                    if (fileInfo.FileType == "Document" && !string.IsNullOrWhiteSpace(fileInfo.FilePath))
                    {
                        // Create temporary BookDocument for deletion helper
                        BookDocument tmpDoc = new BookDocument
                        {
                            Id = fileInfo.DocumentId ?? 0,
                            BookId = fileInfo.BookId ?? 0,
                            DocumentPath = fileInfo.FilePath,
                            DocumentLink = fileInfo.FileLink ?? string.Empty
                        };
                        await DeleteBookDocumentAsync(tmpDoc);
                        documentDeleteCount++;
                        LoggerUtilities.Info($"[Process] Deleted document: {fileInfo.FilePath}");
                    }
                    else if (fileInfo.FileType == "CoverImage" && !string.IsNullOrWhiteSpace(fileInfo.FilePath))
                    {
                        await _dropboxService.DeleteImageAsync(fileInfo.FilePath);
                        coverImageDeleteCount++;
                        LoggerUtilities.Info($"[Process] Deleted cover image: {fileInfo.FilePath}");
                    }
                }
                catch (Exception ex)
                {
                    LoggerUtilities.Error(ex,
                        $"[Warning] Failed to delete {fileInfo.FileType} file: {fileInfo.FilePath}");
                    // Continue with other files even if one fails
                }

            LoggerUtilities.Info(
                $"[Process] Successfully deleted books. Documents: {documentDeleteCount}, Cover images: {coverImageDeleteCount}");
            LoggerUtilities.Info($"[End] bookIds={string.Join(',', bookIds)}");
            return (true, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error deleting bookIds={string.Join(',', bookIds)}");
            return (false, $"Failed to delete books: {ex.Message}");
        }
    }

    // Database CRUD operations

    /// <summary>
    ///     Validates the uploaded ebook file for type and size constraints.
    /// </summary>
    /// <param name="file">The uploaded form file to validate.</param>
    /// <returns>An error message if validation fails; otherwise, an empty string.</returns>
    public string ValidateEbook(IFormFile file)
    {
        // Local validation only — no DB/network logging per guideline
        string ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext) || !FileNameRegex.IsMatch(file.FileName))
            return "Only EPUB and PDF files are allowed.";

        if (file.Length > MaxEbookSize) return "Ebook file size cannot exceed 50MB.";

        // Validate file header/signature for additional safety
        try
        {
            using Stream fs = file.OpenReadStream();
            // Read first 8 bytes for header checks
            byte[] header = new byte[8];
            int read = fs.Read(header, 0, header.Length);

            // PDF signature: %PDF- (0x25 0x50 0x44 0x46 0x2D)
            if (ext == ".pdf")
            {
                if (read < 5) return "Invalid PDF file.";
                string sig = Encoding.ASCII.GetString(header, 0, 5);
                if (!sig.StartsWith("%PDF-")) return "Invalid PDF file: file header mismatch.";
            }

            // EPUB signature: PK.. (ZIP). Also verify 'mimetype' entry content.
            if (ext == ".epub")
            {
                if (read < 4) return "Invalid EPUB file.";
                // PK\x03\x04 is typical for ZIP files
                if (!(header[0] == 0x50 && header[1] == 0x4b && header[2] == 0x03 && header[3] == 0x04))
                    return "Invalid EPUB file: not a ZIP container.";

                // To verify it's a proper EPUB we must check mimetype file inside the ZIP
                try
                {
                    // We need a seekable stream for ZipArchive, copy to MemoryStream
                    fs.Seek(0, SeekOrigin.Begin);
                    using MemoryStream ms = new();
                    fs.CopyTo(ms);
                    ms.Seek(0, SeekOrigin.Begin);
                    using ZipArchive za = new(ms, ZipArchiveMode.Read, false);
                    ZipArchiveEntry? mimetypeEntry = za.GetEntry("mimetype");
                    if (mimetypeEntry == null) return "Invalid EPUB file: The mimetype entry missing.";
                    using Stream entryStream = mimetypeEntry.Open();
                    using StreamReader reader = new(entryStream);
                    string mime = reader.ReadToEnd().Trim();
                    if (!mime.Equals("application/epub+zip", StringComparison.OrdinalIgnoreCase))
                        return "Invalid EPUB: unexpected mimetype content.";
                }
                catch (Exception)
                {
                    return "Invalid EPUB file: failed to parse EPUB container.";
                }
            }
        }
        catch (Exception)
        {
            return "Failed to validate ebook file content.";
        }

        return string.Empty;
    }

    /// <summary>
    ///     Generates a unique filename for the uploaded ebook file.
    /// </summary>
    /// <param name="file">The uploaded form file.</param>
    /// <param name="name">The custom name for the ebook.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="extension">The file extension (including the dot).</param>
    /// <returns>A unique filename string.</returns>
    public string GenerateEbookFileName(IFormFile file, string name, int bookId, string extension)
    {
        // Local operation — no DB/network
        string fileName = $"{name}_{bookId}_{Guid.NewGuid()}{extension}";
        return fileName;
    }

    /// <summary>
    ///     Converts a PDF file to EPUB format using Aspose.PDF and saves it to the specified logical path using
    ///     IWebHostEnvironment. This method is deprecated and no longer used in the main upload flow.
    ///     It can be used if manual PDF to EPUB conversion is required in the future.
    /// </summary>
    /// <param name="pdfFileName">The source PDF filename (already saved in logical path).</param>
    /// <param name="logicalPath">The logical path under web root (e.g., 'ebooks').</param>
    /// <param name="epubFileName">The target EPUB filename.</param>
    /// <param name="error">Error message if conversion fails.</param>
    /// <returns>True if conversion succeeds, otherwise false.</returns>
    [Obsolete("PDF to EPUB conversion is no longer automatic. Users upload files in their desired format.")]
    public bool ConvertPdfToEpub(string pdfFileName, string logicalPath, string epubFileName, out string error)
    {
        // Local file conversion only — no DB/network logging required.
        error = string.Empty;
        try
        {
            string directory = Path.Combine(_environment.WebRootPath, logicalPath);
            FileUtilities.EnsureDirectory(directory);
            string pdfFilePath = Path.Combine(directory, pdfFileName);
            string epubFilePath = Path.Combine(directory, epubFileName);
            Document pdfDoc = new(pdfFilePath);
            EpubSaveOptions options = new()
            {
                Title = epubFileName,
                ContentRecognitionMode = EpubSaveOptions.RecognitionMode.Flow
            };
            pdfDoc.Save(epubFilePath, options);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    /// <summary>
    ///     Processes a new ebook file upload, including PDF to EPUB conversion if applicable.
    ///     Returns a list of BookDocument entities (PDF and optionally EPUB).
    /// </summary>
    /// <param name="file">The uploaded ebook file.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="customName">The custom name for the ebook.</param>
    /// <returns>Tuple: (List of BookDocument entities, List of warning messages).</returns>
    public async Task<(List<BookDocument> documents, List<string> warnings)> ProcessNewEbookUploadAsync(
        IFormFile file, int bookId, string customName)
    {
        LoggerUtilities.Info($"[Start] file={file.FileName}, bookId={bookId}");
        List<BookDocument> documents = [];
        List<string> warnings = [];

        try
        {
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            // Determine the format type based on file extension
            string formatType = extension == ".epub" ? "EPUB" : "PDF";

            // Create and upload the document as-is without any conversion
            (BookDocument? bookDocument, string? uploadError) =
                await CreateBookDocumentAsync(file, bookId, customName, formatType);

            if (bookDocument != null)
            {
                documents.Add(bookDocument);
            }
            else if (!string.IsNullOrEmpty(uploadError))
            {
                warnings.Add(uploadError);
                LoggerUtilities.Warning(
                    $"[Process] Upload error for bookId={bookId}, error={uploadError}");
            }

            return (documents, warnings);
        }
        catch (Exception ex)
        {
            warnings.Add($"Error processing ebook upload: {ex.Message}");
            LoggerUtilities.Error(ex,
                $"[Danger] Error processing ebook upload for bookId: {bookId}");
            return (documents, warnings);
        }
        finally
        {
            LoggerUtilities.Info($"[End] bookId={bookId}");
        }
    }

    /// <summary>
    ///     Uploads the ebook file to Dropbox and returns Dropbox path and preview link.
    /// </summary>
    /// <param name="file">The uploaded form file.</param>
    /// <param name="dropboxFolder">Dropbox folder path (e.g., "/ebooks/bookId").</param>
    /// <param name="fileName">The target filename.</param>
    /// <returns>Tuple: (Dropbox path, preview link)</returns>
    public async Task<(string dropboxPath, string previewLink)> SaveEbookFileAsync(IFormFile file, string dropboxFolder,
        string fileName)
    {
        LoggerUtilities.Info($"[Start] Upload to Dropbox: {dropboxFolder}/{fileName}");
        // Ensure dropboxFolder starts with '/'
        string dropboxPath = $"{dropboxFolder.TrimEnd('/')}/{fileName}";
        await using Stream stream = file.OpenReadStream();
        // Network operation: upload to Dropbox
        await _dropboxHelper.CreateAsync(dropboxPath, stream);
        // Network operation: get preview link
        string previewLink = await _dropboxHelper.GetPreviewLinkAsync(dropboxPath);
        LoggerUtilities.Info($"[Process] Uploaded to Dropbox: {dropboxPath}");
        LoggerUtilities.Info($"[End] Completed upload for {file.FileName}");
        return (dropboxPath, previewLink);
    }

    /// <summary>
    ///     Deletes the ebook file from Dropbox using DropboxHelper.
    /// </summary>
    /// <param name="dropboxFolder">Dropbox folder path (e.g., "/ebooks/bookId").</param>
    /// <param name="fileName">The filename to delete.</param>
    /// <returns>True if deletion succeeds, otherwise false.</returns>
    public async Task<bool> DeleteEbookFileAsync(string dropboxFolder, string fileName)
    {
        LoggerUtilities.Info($"[Start] Deleting from Dropbox: {dropboxFolder}/{fileName}");
        string dropboxPath = $"{dropboxFolder.TrimEnd('/')}/{fileName}";
        try
        {
            // Network operation: delete file
            await _dropboxHelper.DeleteAsync(dropboxPath);
            LoggerUtilities.Info($"[Process] Deleted: {dropboxPath}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error deleting: {dropboxPath}");
            return false;
        }
        finally
        {
            LoggerUtilities.Info($"[End] Finished delete attempt for {fileName}");
        }
    }

    /// <summary>
    ///     Renames an ebook file in Dropbox by moving it to a new path with the updated name.
    /// </summary>
    /// <param name="oldDropboxPath">Current Dropbox file path (e.g., "/ebooks/bookId/oldfile.epub").</param>
    /// <param name="newCustomName">The new custom name for the file (e.g., "111").</param>
    /// <param name="bookId">The book ID.</param>
    /// <returns>Tuple: (new Dropbox path if successful, error message if any).</returns>
    public async Task<(string? newDropboxPath, string? error)> RenameEbookFileAsync(string oldDropboxPath,
        string newCustomName, int bookId)
    {
        LoggerUtilities.Info(
            $"[Start] Rename path: {oldDropboxPath} -> newName={newCustomName}");
        try
        {
            string extension = Path.GetExtension(oldDropboxPath);
            string directory = Path.GetDirectoryName(oldDropboxPath)?.Replace("\\", "/") ?? "/ebooks";
            string guid = Guid.NewGuid().ToString();
            string newFileName = $"{newCustomName}_{bookId}_{guid}{extension}";
            string newDropboxPath = $"{directory.TrimEnd('/')}/{newFileName}";

            // Network operation: move file in Dropbox
            await _dropboxHelper.MoveAsync(oldDropboxPath, newDropboxPath);
            LoggerUtilities.Info($"[Process] Moved to {newDropboxPath}");
            return (newDropboxPath, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[Danger] Error renaming: {oldDropboxPath}");
            return (null, ex.Message);
        }
        finally
        {
            LoggerUtilities.Info($"[End] Completed rename attempt for {oldDropboxPath}");
        }
    }

    /// <summary>
    ///     Saves a book document (ebook file) to Dropbox and creates a BookDocument entity.
    ///     This method handles the complete workflow: validation, upload, and entity creation.
    /// </summary>
    /// <param name="file">The uploaded ebook file.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="customName">The custom name for the ebook.</param>
    /// <param name="formatType">The format type (e.g., "PDF", "EPUB").</param>
    /// <returns>Tuple: (BookDocument entity if successful, error message if any).</returns>
    public async Task<(BookDocument? bookDocument, string? error)> CreateBookDocumentAsync(
        IFormFile file, int bookId, string customName, string formatType)
    {
        LoggerUtilities.Info($"[Start] bookId={bookId}, file={file.FileName}");
        try
        {
            // Validate ebook (local check)
            string validationError = ValidateEbook(file);
            if (!string.IsNullOrEmpty(validationError)) return (null, validationError);

            // Generate filename (local)
            string extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            string fileName = GenerateEbookFileName(file, customName, bookId, extension);

            // Network operation: upload to Dropbox
            string dropboxFolder = $"/ebooks/{bookId}";
            LoggerUtilities.Info($"[Process] Uploading to Dropbox for bookId={bookId}");
            (string dropboxPath, string previewLink) = await SaveEbookFileAsync(file, dropboxFolder, fileName);

            // Create BookDocument entity (local object only)
            BookDocument bookDocument = new()
            {
                BookId = bookId,
                FormatType = formatType,
                DocumentPath = dropboxPath.TrimStart('/'),
                DocumentLink = previewLink,
                CreatedAt = DateTime.Now.ToLocalTime(),
                UpdatedAt = DateTime.Now.ToLocalTime()
            };

            return (bookDocument, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error creating document for bookId={bookId}");
            return (null, $"Failed to create book document: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] Finished processing for bookId={bookId}");
        }
    }

    /// <summary>
    ///     Deletes a book document from Dropbox and optionally from local cache.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity to delete.</param>
    /// <param name="deleteLocalCache">Whether to delete local cached files as well.</param>
    /// <returns>True if deletion succeeds, otherwise false.</returns>
    public async Task<bool> DeleteBookDocumentAsync(BookDocument bookDocument, bool deleteLocalCache = true)
    {
        LoggerUtilities.Info(
            $"[Start] bookId={bookDocument.BookId}, documentId={bookDocument.Id}");
        try
        {
            string fileName = Path.GetFileName(bookDocument.DocumentPath);
            string dropboxFolder = $"/ebooks/{bookDocument.BookId}";

            // Network operation: delete from Dropbox
            LoggerUtilities.Info(
                $"[Process] Deleting {fileName} from Dropbox for bookId={bookDocument.BookId}");
            await DeleteEbookFileAsync(dropboxFolder, fileName);

            // Optionally delete local cached files (local op, no log)
            if (deleteLocalCache)
            {
                string logicalPath = Path.Combine("uploads", "ebooks");
                string localDirectory = Path.Combine(WebRootPath, logicalPath);
                FileUtilities.DeleteFile(localDirectory, fileName);
            }

            LoggerUtilities.Info(
                $"[Process] Deleted BookDocument documentId={bookDocument.Id}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error deleting documentId={bookDocument.Id}");
            return false;
        }
        finally
        {
            LoggerUtilities.Info(
                $"[End] Finished delete attempt for documentId={bookDocument.Id}");
        }
    }

    /// <summary>
    ///     Updates a BookDocument's name by renaming the file in Dropbox.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity to update.</param>
    /// <param name="newCustomName">The new custom name for the file.</param>
    /// <param name="bookId">The book ID.</param>
    /// <returns>Tuple: (updated BookDocument if successful, error message if any).</returns>
    public async Task<(BookDocument? updatedDocument, string? error)> UpdateBookDocumentNameAsync(
        BookDocument bookDocument, string newCustomName, int bookId)
    {
        LoggerUtilities.Info(
            $"[Start] documentId={bookDocument.Id}, newName={newCustomName}");
        try
        {
            string currentPath = bookDocument.DocumentPath;
            string currentFileName = Path.GetFileName(currentPath);

            // Extract current custom name (part before first underscore)
            string currentCustomName = currentFileName.Contains("_")
                ? currentFileName.Substring(0, currentFileName.IndexOf("_", StringComparison.Ordinal))
                : Path.GetFileNameWithoutExtension(currentFileName);

            // Skip if name hasn't changed
            if (currentCustomName.Equals(newCustomName, StringComparison.OrdinalIgnoreCase))
                // No network/DB operations needed; return early without extra logs.
                return (bookDocument, null);

            // Build full Dropbox path
            string dropboxCurrentPath = currentPath.StartsWith("/")
                ? currentPath
                : $"/ebooks/{bookId}/{currentFileName}";

            // Network operation: rename in Dropbox
            LoggerUtilities.Info(
                $"[Process] Renaming in Dropbox for documentId={bookDocument.Id}");
            (string? newDropboxPath, string? error) =
                await RenameEbookFileAsync(dropboxCurrentPath, newCustomName, bookId);

            if (!string.IsNullOrEmpty(newDropboxPath))
            {
                // Update entity (local)
                bookDocument.DocumentPath = newDropboxPath.TrimStart('/');
                bookDocument.UpdatedAt = DateTime.Now.ToLocalTime();
                return (bookDocument, null);
            }

            LoggerUtilities.Warning(
                $"[Process] Failed to rename documentId={bookDocument.Id}, error={error}");
            return (null, error);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error updating documentId={bookDocument.Id}");
            return (null, $"Failed to update book document name: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] documentId={bookDocument.Id}");
        }
    }


    /// <summary>
    ///     Gets or downloads an EPUB file for preview. If the file exists locally, returns the path.
    ///     If not, downloads it from Dropbox using the DocumentLink and caches it locally.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity containing the EPUB file information.</param>
    /// <returns>Tuple: (local file path if successful, error message if any).</returns>
    public async Task<(string? localFilePath, string? error)> GetOrDownloadEpubForPreviewAsync(
        BookDocument bookDocument)
    {
        LoggerUtilities.Info($"[Start] documentId={bookDocument.Id}");
        try
        {
            // Validate that it's an EPUB file
            string extension = Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant();
            if (extension != ".epub")
            {
                LoggerUtilities.Warning(
                    $"[Process] Document is not EPUB for documentId={bookDocument.Id}, extension={extension}");
                return (null, "The specified document is not an EPUB file.");
            }

            // Determine local path in wwwroot
            string logicalPath = Path.Combine("uploads", "ebooks");
            string localDirectory = Path.Combine(WebRootPath, logicalPath);
            FileUtilities.EnsureDirectory(localDirectory);
            string fileName = Path.GetFileName(bookDocument.DocumentPath);
            string localEpubPath = Path.Combine(localDirectory, fileName);

            // Check if file already exists locally
            if (File.Exists(localEpubPath))
            {
                LoggerUtilities.Info(
                    $"[Process] Local cache hit for documentId={bookDocument.Id}");
                return (localEpubPath, null);
            }

            // Download from DocumentLink if not exists
            try
            {
                using HttpClient httpClient = new();
                HttpResponseMessage response = await httpClient.GetAsync(bookDocument.DocumentLink);
                response.EnsureSuccessStatusCode();

                await using FileStream fileStream = new(localEpubPath, FileMode.Create, FileAccess.Write);
                await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
                await remoteStream.CopyToAsync(fileStream);

                LoggerUtilities.Info(
                    $"[Process] Downloaded EPUB for documentId={bookDocument.Id}");
                return (localEpubPath, null);
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error(ex,
                    $"[Danger] Failed to download EPUB for documentId={bookDocument.Id}");
                return (null, $"Failed to download EPUB file: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error processing EPUB preview for documentId={bookDocument.Id}");
            return (null, $"Error processing EPUB preview: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] documentId={bookDocument.Id}");
        }
    }

    /// <summary>
    ///     Gets or downloads a PDF file for preview. If the file exists locally, returns the path.
    ///     If not, downloads it from Dropbox using the DocumentLink and caches it locally.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity containing the PDF file information.</param>
    /// <returns>Tuple: (local file path if successful, error message if any).</returns>
    public async Task<(string? localFilePath, string? error)> GetOrDownloadPdfForPreviewAsync(
        BookDocument bookDocument)
    {
        LoggerUtilities.Info($"[Start] documentId={bookDocument.Id}");
        try
        {
            // Validate that it's a PDF file
            string extension = Path.GetExtension(bookDocument.DocumentPath).ToLowerInvariant();
            if (extension != ".pdf")
            {
                LoggerUtilities.Warning(
                    $"[Process] Document is not PDF for documentId={bookDocument.Id}, extension={extension}");
                return (null, "The specified document is not a PDF file.");
            }

            // Determine local path in wwwroot
            string logicalPath = Path.Combine("uploads", "ebooks");
            string localDirectory = Path.Combine(WebRootPath, logicalPath);
            FileUtilities.EnsureDirectory(localDirectory);
            string fileName = Path.GetFileName(bookDocument.DocumentPath);
            string localPdfPath = Path.Combine(localDirectory, fileName);

            // Check if file already exists locally
            if (File.Exists(localPdfPath))
            {
                LoggerUtilities.Info(
                    $"[Process] Local cache hit for documentId={bookDocument.Id}");
                return (localPdfPath, null);
            }

            // Download from DocumentLink if not exists
            try
            {
                using HttpClient httpClient = new();
                HttpResponseMessage response = await httpClient.GetAsync(bookDocument.DocumentLink);
                response.EnsureSuccessStatusCode();

                await using FileStream fileStream = new(localPdfPath, FileMode.Create, FileAccess.Write);
                await using Stream remoteStream = await response.Content.ReadAsStreamAsync();
                await remoteStream.CopyToAsync(fileStream);

                LoggerUtilities.Info(
                    $"[Process] Downloaded PDF for documentId={bookDocument.Id}");
                return (localPdfPath, null);
            }
            catch (Exception ex)
            {
                LoggerUtilities.Error(ex,
                    $"[Danger] Failed to download PDF for documentId={bookDocument.Id}");
                return (null, $"Failed to download PDF file: {ex.Message}");
            }
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error processing PDF preview for documentId={bookDocument.Id}");
            return (null, $"Error processing PDF preview: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] documentId={bookDocument.Id}");
        }
    }

    /// <summary>
    ///     Generates a book description using AI based on book metadata.
    ///     Compiles the book title, author name, publication year, language, and categories
    ///     into a prompt and sends it to Gemini for processing.
    /// </summary>
    /// <param name="bookId">The ID of the book to generate description for.</param>
    /// <returns>Tuple: (generated description if successful, error message if any).</returns>
    public async Task<(string? generatedDescription, string? error)> GenerateDescriptionAsync(int bookId)
    {
        LoggerUtilities.Info($"[Start] bookId: {bookId}");
        try
        {
            // Retrieve the book with its categories
            Book? book = await GetBookByIdAsync(bookId);

            if (book == null)
            {
                LoggerUtilities.Warning($"[Process] Book not found for bookId: {bookId}");
                return (null, "Book not found.");
            }

            // Compile book metadata for the prompt
            string categoryNames = string.Empty;
            if (book.BookCategoryMaps != null && book.BookCategoryMaps.Count > 0)
            {
                List<string> catNames = book.BookCategoryMaps
                    .Where(bcm => bcm.BookCategory != null)
                    .Select(bcm => bcm.BookCategory!.Name)
                    .ToList();
                categoryNames = string.Join(", ", catNames);
            }

            string year = book.PublicationYear?.Year.ToString() ?? "Unknown";

            // Build a comprehensive prompt for Gemini with explicit format requirements
            string prompt = $"""
                             You are a professional book description writer. 
                             Your task is to craft a concise, engaging, and informative description for the following book based on its metadata.

                             BOOK DETAILS:
                             - Title: {book.Title}
                             - Author: {book.AuthorName}
                             - Publication Year: {year}
                             - Language: {book.LanguageCode}
                             - Categories: {(string.IsNullOrWhiteSpace(categoryNames) ? "Not specified" : categoryNames)}
                             - Existing Description: {(string.IsNullOrWhiteSpace(book.Description) ? "None provided" : book.Description)}

                             WRITING GUIDELINES:
                             1. Write a single, cohesive paragraph of 2–3 complete sentences.
                             2. Use a professional and inviting tone suitable for book summaries on bookstore or library platforms.
                             3. Highlight the book’s central theme, appeal, or significance — avoid plot spoilers.
                             4. Do not include:
                                - Preambles, explanations, or options.
                                - Quotes, markdown symbols, lists, or formatting characters (*, -, >, etc.).
                                - Questions or alternative suggestions.
                             5. Output only the final description text — nothing else.

                             Now, write the final description below:
                             """;


            // Network operation: call Gemini
            LoggerUtilities.Info($"[Process] Calling Gemini for bookId: {bookId}");
            string generatedDescription = await _geminiHelper.GenerateTextAsync(prompt);

            if (string.IsNullOrWhiteSpace(generatedDescription))
            {
                LoggerUtilities.Warning(
                    $"[Process] Gemini returned empty for bookId: {bookId}");
                return (null, "Failed to generate description from AI.");
            }

            LoggerUtilities.Info(
                $"[Process] Gemini generated description for bookId: {bookId}");
            return (generatedDescription, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex,
                $"[Danger] Error generating description for bookId: {bookId}");
            return (null, $"Error generating description: {ex.Message}");
        }
        finally
        {
            LoggerUtilities.Info($"[End] bookId: {bookId}");
        }
    }
}