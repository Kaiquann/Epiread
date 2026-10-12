using System.ComponentModel.DataAnnotations;

namespace EpiRead.Models.Share;

/// <summary>
///     View model for initializing a chunk upload session.
/// </summary>
public class ChunkUploadInitVm
{
    /// <summary>
    ///     The name of the file being uploaded.
    /// </summary>
    [Required(ErrorMessage = "Filename is required.")]
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    ///     The total size of the file in bytes.
    /// </summary>
    [Required(ErrorMessage = "File size is required.")]
    [Range(1, long.MaxValue, ErrorMessage = "File size must be greater than 0.")]
    public long FileSize { get; set; }

    /// <summary>
    ///     The total number of chunks.
    /// </summary>
    [Required(ErrorMessage = "Total chunks is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Total chunks must be at least 1.")]
    public int TotalChunks { get; set; }
}

/// <summary>
///     View model for uploading a chunk of a file.
/// </summary>
public class ChunkUploadVm
{
    /// <summary>
    ///     The unique upload session ID.
    /// </summary>
    [Required(ErrorMessage = "Upload ID is required.")]
    public string UploadId { get; set; } = string.Empty;

    /// <summary>
    ///     The index of the current chunk (0-based).
    /// </summary>
    [Required(ErrorMessage = "Chunk index is required.")]
    [Range(0, int.MaxValue, ErrorMessage = "Chunk index must be 0 or greater.")]
    public int ChunkIndex { get; set; }

    /// <summary>
    ///     The chunk data.
    /// </summary>
    [Required(ErrorMessage = "Chunk data is required.")]
    public IFormFile? ChunkData { get; set; }
}

/// <summary>
///     View model for completing a chunk upload.
/// </summary>
public class ChunkUploadCompleteVm
{
    /// <summary>
    ///     The unique upload session ID.
    /// </summary>
    [Required(ErrorMessage = "Upload ID is required.")]
    public string UploadId { get; set; } = string.Empty;
}

/// <summary>
///     Response model for chunk upload operations.
/// </summary>
public class ChunkUploadResponseVm
{
    public bool Success { get; set; }
    public string? UploadId { get; set; }
    public string? Message { get; set; }
    public int? ChunksReceived { get; set; }
    public int? TotalChunks { get; set; }
}

/// <summary>
///     Represents a chunk upload session.
/// </summary>
public class ChunkUploadSession
{
    public string UploadId { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public int TotalChunks { get; set; }
    public string TempFolder { get; set; } = string.Empty;
    public HashSet<int> ReceivedChunks { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}