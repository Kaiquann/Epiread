namespace EpiRead.Services.Interfaces;

/// <summary>
///     Service interface for book-related operations: file handling, storage,
///     conversion, AI-generated descriptions and database CRUD operations.
/// </summary>
/// <author>Chong Jun Xiang</author>
public interface IBookService
{
    /// <summary>
    ///     Exposes the web root path for local file operations.
    /// </summary>
    string WebRootPath { get; }

    // Database CRUD operations
    /// <summary>
    ///     Retrieves a book by its ID with related category mappings and categories.
    /// </summary>
    /// <param name="bookId">The ID of the book to retrieve.</param>
    /// <returns>The Book entity if found; otherwise, null.</returns>
    Task<Book?> GetBookByIdAsync(int bookId);

    /// <summary>
    ///     Searches and filters books based on search criteria, filters, and status.
    /// </summary>
    /// <param name="search">Search term to match against title, author name, or publication year.</param>
    /// <param name="filterId">Category filter ID (0 for no category filter).</param>
    /// <param name="languageCode">Language filter code (null for no language filter).</param>
    /// <param name="status">Book status to filter by (e.g., "Available", "Trashed").</param>
    /// <returns>List of books matching the filter criteria.</returns>
    Task<List<Book>> GetFilteredBooksAsync(string search, int filterId, string? languageCode, string status);

    /// <summary>
    ///     Creates a new book with category mappings in the database.
    /// </summary>
    /// <param name="book">The Book entity to create.</param>
    /// <param name="categoryIds">List of category IDs to associate with the book.</param>
    /// <returns>Tuple: (created Book entity if successful, error message if any).</returns>
    Task<(Book? book, string? error)> CreateBookAsync(Book book, List<int> categoryIds);

    /// <summary>
    ///     Updates an existing book and its category mappings.
    /// </summary>
    /// <param name="book">The Book entity to update.</param>
    /// <param name="categoryIds">Updated list of category IDs to associate with the book.</param>
    /// <returns>Tuple: (updated Book entity if successful, error message if any).</returns>
    Task<(Book? book, string? error)> UpdateBookAsync(Book book, List<int> categoryIds);

    /// <summary>
    ///     Soft deletes (trashes) one or more books by setting their status to "Trashed".
    /// </summary>
    /// <param name="bookIds">Array of book IDs to trash.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    Task<(bool success, string? error)> TrashBooksAsync(int[] bookIds);

    /// <summary>
    ///     Recovers one or more trashed books by setting their status back to "Available".
    /// </summary>
    /// <param name="bookIds">Array of book IDs to recover.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    Task<(bool success, string? error)> RecoverBooksAsync(int[] bookIds);

    /// <summary>
    ///     Permanently deletes one or more books and their associated documents and category mappings.
    /// </summary>
    /// <param name="bookIds">Array of book IDs to delete.</param>
    /// <returns>Tuple: (true if successful, error message if any).</returns>
    Task<(bool success, string? error)> DeleteBooksAsync(int[] bookIds);

    /// <summary>
    ///     Validates the uploaded ebook file for type and size constraints.
    /// </summary>
    /// <param name="file">The uploaded form file to validate.</param>
    /// <returns>An error message if validation fails; otherwise, an empty string.</returns>
    string ValidateEbook(IFormFile file);

    /// <summary>
    ///     Generates a unique filename for the uploaded ebook file.
    /// </summary>
    /// <param name="file">The uploaded form file.</param>
    /// <param name="name">The custom name for the ebook.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="extension">The file extension (including the dot).</param>
    /// <returns>A unique filename string.</returns>
    string GenerateEbookFileName(IFormFile file, string name, int bookId, string extension);

    /// <summary>
    ///     Converts a PDF file to EPUB format using Aspose.PDF and saves it to the specified logical path using
    ///     IWebHostEnvironment.
    /// </summary>
    /// <param name="pdfFileName">The source PDF filename (already saved in logical path).</param>
    /// <param name="logicalPath">The logical path under web root (e.g., 'ebooks').</param>
    /// <param name="epubFileName">The target EPUB filename.</param>
    /// <param name="error">Error message if conversion fails.</param>
    /// <returns>True if conversion succeeds, otherwise false.</returns>
    bool ConvertPdfToEpub(string pdfFileName, string logicalPath, string epubFileName, out string error);

    /// <summary>
    ///     Processes a new ebook file upload, including PDF to EPUB conversion if applicable.
    ///     Returns a list of BookDocument entities (PDF and optionally EPUB).
    /// </summary>
    /// <param name="file">The uploaded ebook file.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="customName">The custom name for the ebook.</param>
    /// <returns>Tuple: (List of BookDocument entities, List of warning messages).</returns>
    Task<(List<BookDocument> documents, List<string> warnings)> ProcessNewEbookUploadAsync(
        IFormFile file, int bookId, string customName);

    /// <summary>
    ///     Uploads the ebook file to Dropbox and returns Dropbox path and preview link.
    /// </summary>
    /// <param name="file">The uploaded form file.</param>
    /// <param name="dropboxFolder">Dropbox folder path (e.g., "/ebooks/bookId").</param>
    /// <param name="fileName">The target filename.</param>
    /// <returns>Tuple: (Dropbox path, preview link)</returns>
    Task<(string dropboxPath, string previewLink)> SaveEbookFileAsync(IFormFile file, string dropboxFolder,
        string fileName);

    /// <summary>
    ///     Deletes the ebook file from Dropbox using DropboxHelper.
    /// </summary>
    /// <param name="dropboxFolder">Dropbox folder path (e.g., "/ebooks/bookId").</param>
    /// <param name="fileName">The filename to delete.</param>
    /// <returns>True if deletion succeeds, otherwise false.</returns>
    Task<bool> DeleteEbookFileAsync(string dropboxFolder, string fileName);

    /// <summary>
    ///     Renames an ebook file in Dropbox by moving it to a new path with the updated name.
    /// </summary>
    /// <param name="oldDropboxPath">Current Dropbox file path (e.g., "/ebooks/bookId/oldfile.epub").</param>
    /// <param name="newCustomName">The new custom name for the file (e.g., "111").</param>
    /// <param name="bookId">The book ID.</param>
    /// <returns>Tuple: (new Dropbox path if successful, error message if any).</returns>
    Task<(string? newDropboxPath, string? error)> RenameEbookFileAsync(string oldDropboxPath,
        string newCustomName, int bookId);


    /// <summary>
    ///     Saves a book document (ebook file) to Dropbox and creates a BookDocument entity.
    ///     This method handles the complete workflow: validation, upload, and entity creation.
    /// </summary>
    /// <param name="file">The uploaded ebook file.</param>
    /// <param name="bookId">The ID of the book.</param>
    /// <param name="customName">The custom name for the ebook.</param>
    /// <param name="formatType">The format type (e.g., "PDF", "EPUB").</param>
    /// <returns>Tuple: (BookDocument entity if successful, error message if any).</returns>
    Task<(BookDocument? bookDocument, string? error)> CreateBookDocumentAsync(
        IFormFile file, int bookId, string customName, string formatType);

    /// <summary>
    ///     Deletes a book document from Dropbox and optionally from local cache.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity to delete.</param>
    /// <param name="deleteLocalCache">Whether to delete local cached files as well.</param>
    /// <returns>True if deletion succeeds, otherwise false.</returns>
    Task<bool> DeleteBookDocumentAsync(BookDocument bookDocument, bool deleteLocalCache = true);

    /// <summary>
    ///     Updates a BookDocument's name by renaming the file in Dropbox.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity to update.</param>
    /// <param name="newCustomName">The new custom name for the file.</param>
    /// <param name="bookId">The book ID.</param>
    /// <returns>Tuple: (updated BookDocument if successful, error message if any).</returns>
    Task<(BookDocument? updatedDocument, string? error)> UpdateBookDocumentNameAsync(
        BookDocument bookDocument, string newCustomName, int bookId);


    /// <summary>
    ///     Gets or downloads an EPUB file for preview. If the file exists locally, returns the path.
    ///     If not, downloads it from Dropbox using the DocumentLink and caches it locally.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity containing the EPUB file information.</param>
    /// <returns>Tuple: (local file path if successful, error message if any).</returns>
    Task<(string? localFilePath, string? error)> GetOrDownloadEpubForPreviewAsync(
        BookDocument bookDocument);

    /// <summary>
    ///     Gets or downloads a PDF file for preview. If the file exists locally, returns the path.
    ///     If not, downloads it from Dropbox using the DocumentLink and caches it locally.
    /// </summary>
    /// <param name="bookDocument">The BookDocument entity containing the PDF file information.</param>
    /// <returns>Tuple: (local file path if successful, error message if any).</returns>
    Task<(string? localFilePath, string? error)> GetOrDownloadPdfForPreviewAsync(
        BookDocument bookDocument);

    /// <summary>
    ///     Generates a book description using AI based on book metadata.
    /// </summary>
    /// <param name="bookId">The ID of the book to generate description for.</param>
    /// <returns>Tuple: (generated description if successful, error message if any).</returns>
    Task<(string? generatedDescription, string? error)> GenerateDescriptionAsync(int bookId);
}