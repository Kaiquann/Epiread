namespace EpiRead.Services;

/// <summary>
///     File: DropboxService.cs
///     Provides a concrete Dropbox-backed implementation for image upload and
///     deletion operations used across the application. This service delegates
///     validation and Dropbox API calls to helper components and centralizes
///     logging for network operations involving Dropbox.
/// </summary>
/// <author>Chong Jun Xiang</author>
public class DropboxService : IDropboxService
{
    private readonly IImageDropboxHelper _imageDropboxHelper;
    private readonly IImageHelper _imageHelper;

    public DropboxService(IImageDropboxHelper imageDropboxHelper, IImageHelper imageHelper)
    {
        _imageDropboxHelper = imageDropboxHelper ?? throw new ArgumentNullException(nameof(imageDropboxHelper));
        _imageHelper = imageHelper ?? throw new ArgumentNullException(nameof(imageHelper));
    }

    /// <inheritdoc />
    public async Task<(string? imagePath, string? imageLink, string? error)> SaveImageAsync(
        IFormFile file, string imagePath)
    {
        // note: only logging network operations (Dropbox)
        try
        {
            // Validate photo (no logging for validation per policy)
            string validationError = _imageHelper.ValidatePhoto(file);
            if (!string.IsNullOrEmpty(validationError))
                return (null, null, validationError);

            // Generate filename and construct Dropbox path
            string fileName = FileUtilities.GenerateFileName(file);
            string dropboxPath = $"/{imagePath.Trim('/')}/{fileName}";

            // Network operation: upload to Dropbox
            LoggerUtilities.Info($"[Start] Uploading image to Dropbox: {dropboxPath}");
            await _imageDropboxHelper.UploadPhotoToDropboxAsync(file, dropboxPath);

            // Network operation: get preview link
            LoggerUtilities.Info($"[Process] Getting preview link for: {dropboxPath}");
            string previewLink = await _imageDropboxHelper.GetDropboxImagePreviewLinkAsync(dropboxPath);

            LoggerUtilities.Info($"[End] Uploaded image to: {dropboxPath}");
            return (dropboxPath, previewLink, null);
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error uploading image for file: {file.FileName}");
            return (null, null, $"Failed to upload image to Dropbox: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteImageAsync(string? coverImagePath)
    {
        if (string.IsNullOrWhiteSpace(coverImagePath))
            return true;

        try
        {
            LoggerUtilities.Info($"[Start] Deleting image from Dropbox: {coverImagePath}");
            await _imageDropboxHelper.DeletePhotoFromDropboxAsync(coverImagePath);
            LoggerUtilities.Info($"[End] Deleted image from Dropbox: {coverImagePath}");
            return true;
        }
        catch (Exception ex)
        {
            LoggerUtilities.Error(ex, $"[End] Error deleting image: {coverImagePath}");
            return false;
        }
    }
}