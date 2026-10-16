namespace EpiRead.Services.Interfaces;

public interface IFileStorageHelper
{
    Task CreateAsync(string path, Stream stream);
    Task DeleteAsync(string path);
    Task MoveAsync(string oldPath, string newPath);
    Task<string> GetPreviewLinkAsync(string path);
}
