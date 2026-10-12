namespace EpiRead.Services.Interfaces;

/// <summary>
///     Provides methods to upload and delete images in Dropbox.
///     Implementations of this interface handle validation, file name generation,
///     and communication with Dropbox to persist or remove image files.
/// </summary>
/// <remarks>
///     The service returns the Dropbox path, a preview link when available, and
///     error messages as needed. Use <see cref="SaveImageAsync" /> when you
///     need to upload images and receive a preview link.
/// </remarks>
public interface IDropboxService
{
    /// <summary>
    ///     Uploads an image file to a Dropbox folder after validating the file.
    /// </summary>
    /// <param name="file">The uploaded image file to be validated and uploaded.</param>
    /// <param name="imagePath">The Dropbox folder path to store the image (e.g., "images/books").</param>
    /// <returns>
    ///     A tuple with <c>imagePath</c> set to the Dropbox path when successful,
    ///     <c>imageLink</c> containing a preview link if available, and <c>error</c>
    ///     containing an error message when an operation fails.
    /// </returns>
    Task<(string? imagePath, string? imageLink, string? error)> SaveImageAsync(
        IFormFile file, string imagePath);

    /// <summary>
    ///     Deletes an image from Dropbox.
    /// </summary>
    /// <param name="coverImagePath">The Dropbox path of the image to delete. If null or whitespace, no operation is performed.</param>
    /// <returns>
    ///     <c>true</c> when the operation succeeds or if the input path is null/empty; otherwise <c>false</c>.
    /// </returns>
    Task<bool> DeleteImageAsync(string? coverImagePath);
}