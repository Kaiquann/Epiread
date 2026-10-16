namespace EpiRead.Models.Config;

public class SupabaseStorageConfig
{
    public string ServiceUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string AccessKeyId { get; set; } = string.Empty;
    public string SecretAccessKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = string.Empty;
    public string PublicUrl { get; set; } = string.Empty;
}
