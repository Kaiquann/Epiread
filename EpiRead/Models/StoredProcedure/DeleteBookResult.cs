namespace EpiRead.Models.StoredProcedure;

/// <summary>
///     DTO class for mapping the result set returned by sp_DeleteBooks stored procedure.
///     Contains file path information for external Dropbox cleanup.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DeleteBookResult
{
    /// <summary>
    ///     Indicates whether the stored procedure execution was successful (1) or failed (0).
    /// </summary>
    public int Success { get; set; }

    /// <summary>
    ///     Error message if the operation failed, otherwise null.
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    ///     Type of file to delete: "Document" or "CoverImage".
    /// </summary>
    public string? FileType { get; set; }

    /// <summary>
    ///     Dropbox file path (e.g., "/ebooks/bookId/filename.epub" or "/cover-images/filename.jpg").
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    ///     Dropbox preview/download link for the file.
    /// </summary>
    public string? FileLink { get; set; }

    /// <summary>
    ///     Book ID associated with the file.
    /// </summary>
    public int? BookId { get; set; }

    /// <summary>
    ///     Document ID (for BookDocuments), null for cover images.
    /// </summary>
    public int? DocumentId { get; set; }
}