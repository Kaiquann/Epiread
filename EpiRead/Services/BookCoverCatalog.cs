using System.Text.Json;

namespace EpiRead.Services;

// Recovery copies of the original library covers, independent of external storage.
public sealed class BookCoverCatalog
{
    private readonly Dictionary<string, string> _covers;

    public BookCoverCatalog(IWebHostEnvironment environment)
    {
        string path = Path.Combine(environment.WebRootPath, "images", "books", "catalog.json");
        using var stream = File.OpenRead(path);
        _covers = new Dictionary<string, string>(
            JsonSerializer.Deserialize<Dictionary<string, string>>(stream) ?? new(),
            StringComparer.OrdinalIgnoreCase);
    }

    public string? Find(string title) => _covers.GetValueOrDefault(title.Trim());
}
