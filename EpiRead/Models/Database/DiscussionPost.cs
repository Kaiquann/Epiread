using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class DiscussionPost
{
    [Key] public int Id { get; set; }

    [MaxLength(100)] public string Title { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public int ViewCount { get; set; } = 0;

    [MaxLength(50)] public string Status { get; set; } = nameof(DiscussionPostStatusType.Active);

    [MaxLength(255)] public string? Reason { get; set; } = string.Empty;

    public DateTime? BannedOn { get; set; }

    public DateTime? DeletedOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(Discussion))] public int DiscussionId { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual Discussion? Discussion { get; set; }

    public virtual ICollection<DiscussionComment>? DiscussionComments { get; set; }

    public virtual ICollection<DiscussionLikeCount>? DiscussionLikeCounts { get; set; }

    public virtual ICollection<DiscussionAttachment>? DiscussionAttachments { get; set; }

    public virtual User? User { get; set; }
}