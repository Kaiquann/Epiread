namespace EpiRead.Services.Interfaces;

public interface IImageStorageHelper
{
    Task UploadAsync(IFormFile file, string path);
    Task DeleteAsync(string path);
    Task<string> GetPublicLinkAsync(string path);
}
