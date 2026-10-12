namespace EpiRead.Models.StoredProcedure;

/// <summary>
///     DTO class for mapping the result set returned by sp_DeleteBookCategories stored procedure.
///     Contains deletion status and count information.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DeleteBookCategoryResult
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
    ///     Number of categories successfully deleted.
    /// </summary>
    public int? DeletedCount { get; set; }
}