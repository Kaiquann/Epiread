using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class DiscussionAttachment
{
    [Key] public int Id { get; set; }

    [MaxLength(20)] public string FileType { get; set; } = string.Empty;

    [MaxLength(255)] public string FilePath { get; set; } = string.Empty;

    [MaxLength(500)] public string FileLink { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(DiscussionComment))]
    public int? DiscussionCommentId { get; set; }

    [ForeignKey(nameof(DiscussionPost))] public int? DiscussionPostId { get; set; }

    // Navigation Properties
    public virtual DiscussionComment? DiscussionComment { get; set; }

    public virtual DiscussionPost? DiscussionPost { get; set; }
}