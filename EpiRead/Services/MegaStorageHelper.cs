using CG.Web.MegaApiClient;
using Microsoft.AspNetCore.WebUtilities;

namespace EpiRead.Services;

public sealed class MegaStorageHelper : IFileStorageHelper, IImageStorageHelper, IAsyncDisposable
{
    private readonly MegaApiClient _client = new();
    private readonly MegaStorageConfig _config;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private bool _loggedIn;

    public MegaStorageHelper(MegaStorageConfig config)
    {
        _config = config;
    }

    public async Task CreateAsync(string path, Stream stream)
    {
        string normalizedPath = NormalizePath(path);
        string[] parts = normalizedPath.Split('/');
        INode parent = await GetOrCreateFolderAsync(parts[..^1]);
        INode? existing = await FindChildAsync(parent, parts[^1], NodeType.File);
        if (existing != null) await _client.DeleteAsync(existing, false);
        await _client.UploadAsync(stream, parts[^1], parent);
    }

    public async Task UploadAsync(IFormFile file, string path)
    {
        await using Stream stream = file.OpenReadStream();
        await CreateAsync(path, stream);
    }

    public async Task DeleteAsync(string path)
    {
        INode? node = await FindNodeAsync(path);
        if (node != null) await _client.DeleteAsync(node, false);
    }

    public async Task MoveAsync(string oldPath, string newPath)
    {
        INode node = await FindNodeAsync(oldPath) ??
                     throw new FileNotFoundException("The MEGA file was not found.", oldPath);
        string[] newParts = NormalizePath(newPath).Split('/');
        INode destination = await GetOrCreateFolderAsync(newParts[..^1]);
        INode moved = await _client.MoveAsync(node, destination);
        if (!string.Equals(moved.Name, newParts[^1], StringComparison.Ordinal))
            await _client.RenameAsync(moved, newParts[^1]);
    }

    public Task<string> GetPreviewLinkAsync(string path) => GetPublicLinkAsync(path);

    public Task<string> GetPublicLinkAsync(string path)
    {
        string url = QueryHelpers.AddQueryString("/Storage/Mega", "path", NormalizePath(path));
        return Task.FromResult(url);
    }

    public async Task<Stream> OpenReadAsync(string path)
    {
        INode node = await FindNodeAsync(path) ??
                     throw new FileNotFoundException("The MEGA file was not found.", path);
        return await _client.DownloadAsync(node);
    }

    private async Task EnsureLoggedInAsync()
    {
        if (_loggedIn) return;
        await _loginLock.WaitAsync();
        try
        {
            if (_loggedIn) return;
            if (string.IsNullOrWhiteSpace(_config.Email) || string.IsNullOrWhiteSpace(_config.Password))
                throw new InvalidOperationException("MEGA storage credentials are not configured.");
            await _client.LoginAsync(_config.Email, _config.Password);
            _loggedIn = true;
        }
        finally
        {
            _loginLock.Release();
        }
    }

    private async Task<INode> GetRootFolderAsync()
    {
        await EnsureLoggedInAsync();
        IEnumerable<INode> nodes = await _client.GetNodesAsync();
        INode accountRoot = nodes.Single(n => n.Type == NodeType.Root);
        string rootName = string.IsNullOrWhiteSpace(_config.RootFolder) ? "EpiRead" : _config.RootFolder.Trim();
        INode? root = nodes.FirstOrDefault(n => n.ParentId == accountRoot.Id && n.Type == NodeType.Directory &&
                                              string.Equals(n.Name, rootName, StringComparison.Ordinal));
        return root ?? await _client.CreateFolderAsync(rootName, accountRoot);
    }

    private async Task<INode> GetOrCreateFolderAsync(IEnumerable<string> pathParts)
    {
        INode current = await GetRootFolderAsync();
        foreach (string part in pathParts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            INode? child = await FindChildAsync(current, part, NodeType.Directory);
            current = child ?? await _client.CreateFolderAsync(part, current);
        }
        return current;
    }

    private async Task<INode?> FindNodeAsync(string path)
    {
        string[] parts = NormalizePath(path).Split('/');
        INode current = await GetRootFolderAsync();
        foreach (string part in parts)
        {
            INode? child = await FindChildAsync(current, part, null);
            if (child == null) return null;
            current = child;
        }
        return current;
    }

    private async Task<INode?> FindChildAsync(INode parent, string name, NodeType? type)
    {
        IEnumerable<INode> nodes = await _client.GetNodesAsync(parent);
        return nodes.FirstOrDefault(n => (type == null || n.Type == type) &&
                                         string.Equals(n.Name, name, StringComparison.Ordinal));
    }

    private static string NormalizePath(string path)
    {
        string normalized = path.Replace('\\', '/').Trim('/');
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Split('/').Any(p => p is "." or ".."))
            throw new ArgumentException("Invalid storage path.", nameof(path));
        return normalized;
    }

    public async ValueTask DisposeAsync()
    {
        if (_loggedIn) await _client.LogoutAsync();
        _loginLock.Dispose();
    }
}
