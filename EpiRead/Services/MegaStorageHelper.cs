using CG.Web.MegaApiClient;
using Microsoft.AspNetCore.WebUtilities;

namespace EpiRead.Services;

public sealed class MegaStorageHelper : IFileStorageHelper, IImageStorageHelper, IAsyncDisposable
{
    private readonly MegaApiClient _client = new();
    private readonly MegaStorageConfig _config;
    private readonly SemaphoreSlim _loginLock = new(1, 1);
    private readonly SemaphoreSlim _operationLock = new(1, 1);
    private bool _loggedIn;

    public MegaStorageHelper(MegaStorageConfig config)
    {
        _config = config;
    }

    public async Task CreateAsync(string path, Stream stream)
    {
        string normalizedPath = NormalizePath(path);
        long initialPosition = stream.CanSeek ? stream.Position : 0;
        await RunSerializedAsync(async () =>
        {
            if (stream.CanSeek) stream.Position = initialPosition;
            List<INode> nodes = (await _client.GetNodesAsync()).ToList();
            string[] parts = normalizedPath.Split('/');
            INode parent = await GetOrCreateFolderAsync(parts[..^1], nodes);
            INode? existing = FindChild(nodes, parent, parts[^1], NodeType.File);
            if (existing != null) await _client.DeleteAsync(existing, false);
            await _client.UploadAsync(stream, parts[^1], parent);
        });
    }

    public async Task UploadAsync(IFormFile file, string path)
    {
        await using Stream stream = file.OpenReadStream();
        await CreateAsync(path, stream);
    }

    public async Task DeleteAsync(string path)
    {
        string normalizedPath = NormalizePath(path);
        await RunSerializedAsync(async () =>
        {
            List<INode> nodes = (await _client.GetNodesAsync()).ToList();
            INode? node = FindNode(normalizedPath, nodes);
            if (node != null) await _client.DeleteAsync(node, false);
        });
    }

    public async Task MoveAsync(string oldPath, string newPath)
    {
        string normalizedOldPath = NormalizePath(oldPath);
        string normalizedNewPath = NormalizePath(newPath);
        await RunSerializedAsync(async () =>
        {
            List<INode> nodes = (await _client.GetNodesAsync()).ToList();
            INode node = FindNode(normalizedOldPath, nodes) ??
                         throw new FileNotFoundException("The MEGA file was not found.", oldPath);
            string[] newParts = normalizedNewPath.Split('/');
            INode destination = await GetOrCreateFolderAsync(newParts[..^1], nodes);
            INode moved = await _client.MoveAsync(node, destination);
            if (!string.Equals(moved.Name, newParts[^1], StringComparison.Ordinal))
                await _client.RenameAsync(moved, newParts[^1]);
        });
    }

    public Task<string> GetPreviewLinkAsync(string path) => GetPublicLinkAsync(path);

    public Task<string> GetPublicLinkAsync(string path)
    {
        string url = QueryHelpers.AddQueryString("/Storage/Mega", "path", NormalizePath(path));
        return Task.FromResult(url);
    }

    public async Task<Stream> OpenReadAsync(string path)
    {
        string normalizedPath = NormalizePath(path);
        return await RunSerializedAsync(async () =>
        {
            List<INode> nodes = (await _client.GetNodesAsync()).ToList();
            INode node = FindNode(normalizedPath, nodes) ??
                         throw new FileNotFoundException("The MEGA file was not found.", path);
            return await _client.DownloadAsync(node);
        });
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

    private async Task<INode> GetRootFolderAsync(List<INode> nodes)
    {
        INode accountRoot = nodes.Single(n => n.Type == NodeType.Root);
        string rootName = string.IsNullOrWhiteSpace(_config.RootFolder) ? "EpiRead" : _config.RootFolder.Trim();
        INode? root = nodes.FirstOrDefault(n => n.ParentId == accountRoot.Id && n.Type == NodeType.Directory &&
                                              string.Equals(n.Name, rootName, StringComparison.Ordinal));
        if (root != null) return root;
        root = await _client.CreateFolderAsync(rootName, accountRoot);
        nodes.Add(root);
        return root;
    }

    private async Task<INode> GetOrCreateFolderAsync(IEnumerable<string> pathParts, List<INode> nodes)
    {
        INode current = await GetRootFolderAsync(nodes);
        foreach (string part in pathParts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            INode? child = FindChild(nodes, current, part, NodeType.Directory);
            if (child == null)
            {
                child = await _client.CreateFolderAsync(part, current);
                nodes.Add(child);
            }
            current = child;
        }
        return current;
    }

    private INode? FindNode(string normalizedPath, List<INode> nodes)
    {
        INode accountRoot = nodes.Single(n => n.Type == NodeType.Root);
        string rootName = string.IsNullOrWhiteSpace(_config.RootFolder) ? "EpiRead" : _config.RootFolder.Trim();
        INode? current = FindChild(nodes, accountRoot, rootName, NodeType.Directory);
        if (current == null) return null;
        foreach (string part in normalizedPath.Split('/'))
        {
            INode? child = FindChild(nodes, current, part, null);
            if (child == null) return null;
            current = child;
        }
        return current;
    }

    private static INode? FindChild(IEnumerable<INode> nodes, INode parent, string name, NodeType? type)
    {
        return nodes.FirstOrDefault(n => n.ParentId == parent.Id && (type == null || n.Type == type) &&
                                         string.Equals(n.Name, name, StringComparison.Ordinal));
    }

    private async Task RunSerializedAsync(Func<Task> action)
    {
        await RunSerializedAsync(async () =>
        {
            await action();
            return true;
        });
    }

    private async Task<T> RunSerializedAsync<T>(Func<Task<T>> action)
    {
        await _operationLock.WaitAsync();
        try
        {
            await EnsureLoggedInAsync();
            TimeSpan[] delays = [TimeSpan.Zero, TimeSpan.FromMilliseconds(750), TimeSpan.FromSeconds(2)];
            for (int attempt = 0; ; attempt++)
            {
                if (delays[attempt] > TimeSpan.Zero) await Task.Delay(delays[attempt]);
                try
                {
                    return await action();
                }
                catch (ApiException ex) when (attempt < delays.Length - 1 && IsTransient(ex.ApiResultCode))
                {
                    LoggerUtilities.Warning($"[Process] Transient MEGA API response {ex.ApiResultCode}; retrying.");
                }
            }
        }
        finally
        {
            _operationLock.Release();
        }
    }

    private static bool IsTransient(ApiResultCode code) => code is
        ApiResultCode.RequestFailedRetry or
        ApiResultCode.RequestIncomplete or
        ApiResultCode.TooManyRequests or
        ApiResultCode.ToManyRequestsForThisResource;

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
        _operationLock.Dispose();
    }
}
