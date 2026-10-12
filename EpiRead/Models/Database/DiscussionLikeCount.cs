using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class DiscussionLikeCount
{
    [Key] public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(DiscussionPost))] public int? DiscussionPostId { get; set; }

    [ForeignKey(nameof(DiscussionComment))]
    public int? DiscussionCommentId { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual DiscussionPost? DiscussionPost { get; set; }

    public virtual DiscussionComment? DiscussionComment { get; set; }

    public virtual User? User { get; set; }
}