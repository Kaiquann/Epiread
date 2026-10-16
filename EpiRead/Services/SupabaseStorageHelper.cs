using Amazon.S3;
using Amazon.S3.Model;

namespace EpiRead.Services;

public class SupabaseStorageHelper : IFileStorageHelper, IImageStorageHelper, IDisposable
{
    private readonly IAmazonS3 _client;
    private readonly SupabaseStorageConfig _config;

    public SupabaseStorageHelper(SupabaseStorageConfig config)
    {
        _config = config;

        AmazonS3Config clientConfig = new()
        {
            ServiceURL = config.ServiceUrl.TrimEnd('/'),
            AuthenticationRegion = config.Region,
            ForcePathStyle = true
        };

        _client = new AmazonS3Client(config.AccessKeyId, config.SecretAccessKey, clientConfig);
    }

    public async Task CreateAsync(string path, Stream stream)
    {
        string key = NormalizePath(path);
        PutObjectRequest request = new()
        {
            BucketName = _config.Bucket,
            Key = key,
            InputStream = stream,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request);
    }

    public async Task UploadAsync(IFormFile file, string path)
    {
        await using Stream stream = file.OpenReadStream();
        string key = NormalizePath(path);
        PutObjectRequest request = new()
        {
            BucketName = _config.Bucket,
            Key = key,
            InputStream = stream,
            ContentType = file.ContentType,
            AutoCloseStream = false
        };

        await _client.PutObjectAsync(request);
    }

    public async Task DeleteAsync(string path)
    {
        await _client.DeleteObjectAsync(_config.Bucket, NormalizePath(path));
    }

    public async Task MoveAsync(string oldPath, string newPath)
    {
        string oldKey = NormalizePath(oldPath);
        string newKey = NormalizePath(newPath);

        await _client.CopyObjectAsync(new CopyObjectRequest
        {
            SourceBucket = _config.Bucket,
            SourceKey = oldKey,
            DestinationBucket = _config.Bucket,
            DestinationKey = newKey
        });

        await _client.DeleteObjectAsync(_config.Bucket, oldKey);
    }

    public Task<string> GetPreviewLinkAsync(string path)
    {
        return GetPublicLinkAsync(path);
    }

    public Task<string> GetPublicLinkAsync(string path)
    {
        string baseUrl = _config.PublicUrl.TrimEnd('/');
        string encodedPath = string.Join('/', NormalizePath(path).Split('/').Select(Uri.EscapeDataString));
        return Task.FromResult($"{baseUrl}/{encodedPath}");
    }

    private static string NormalizePath(string path)
    {
        return path.Replace('\\', '/').TrimStart('/');
    }

    public void Dispose()
    {
        _client.Dispose();
        GC.SuppressFinalize(this);
    }
}
