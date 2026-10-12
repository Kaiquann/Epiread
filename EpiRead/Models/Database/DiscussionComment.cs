using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class DiscussionComment
{
    [Key] public int Id { get; set; }

    public string Content { get; set; } = string.Empty;

    public int ViewCount { get; set; } = 0;

    [MaxLength(50)] public string Status { get; set; } = nameof(DiscussionCommentStatusType.Active);

    public DateTime? DeletedOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(DiscussionPost))] public int DiscussionPostId { get; set; }

    [ForeignKey(nameof(ParentComment))] public int? ReplyCommentId { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual DiscussionPost? DiscussionPost { get; set; }

    public virtual DiscussionComment? ParentComment { get; set; }

    public virtual ICollection<DiscussionComment>? Replies { get; set; }

    public virtual ICollection<DiscussionLikeCount>? DiscussionLikeCounts { get; set; }

    public virtual DiscussionAttachment? DiscussionAttachment { get; set; }

    public virtual User? User { get; set; }
}