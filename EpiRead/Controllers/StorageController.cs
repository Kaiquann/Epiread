using Microsoft.AspNetCore.StaticFiles;
using Microsoft.AspNetCore.Mvc;

namespace EpiRead.Controllers;

[Route("Storage")]
public class StorageController : Controller
{
    private readonly IFileStorageHelper _storage;
    private readonly FileExtensionContentTypeProvider _contentTypes = new();

    public StorageController(IFileStorageHelper storage)
    {
        _storage = storage;
    }

    [HttpGet("Mega")]
    [ResponseCache(Duration = 3600, Location = ResponseCacheLocation.Any)]
    public async Task<IActionResult> Mega([FromQuery] string path)
    {
        if (string.IsNullOrWhiteSpace(path)) return BadRequest();
        try
        {
            Stream stream = await _storage.OpenReadAsync(path);
            string contentType = _contentTypes.TryGetContentType(path, out string? detected)
                ? detected
                : "application/octet-stream";
            return File(stream, contentType, enableRangeProcessing: true);
        }
        catch (FileNotFoundException)
        {
            return NotFound();
        }
        catch (ArgumentException)
        {
            return BadRequest();
        }
    }
}
