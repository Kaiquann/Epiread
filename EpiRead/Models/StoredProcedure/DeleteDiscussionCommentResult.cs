namespace EpiRead.Models.StoredProcedure;

/// <summary>
///     DTO class for mapping the result set returned by sp_DeleteDiscussionComment stored procedure.
///     Contains attachment file path information for external Dropbox cleanup.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DeleteDiscussionCommentResult
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
    ///     Dropbox file path for the attachment to delete (e.g., "/discussion-attachments/filename.jpg").
    /// </summary>
    public string? FilePath { get; set; }

    /// <summary>
    ///     Comment ID associated with the attachment.
    /// </summary>
    public int? CommentId { get; set; }
}