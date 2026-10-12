using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class Discussion
{
    [Key] public int Id { get; set; }

    [MaxLength(100)] public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    // Navigation
    public virtual Book? Book { get; set; }

    public virtual ICollection<DiscussionPost>? DiscussionPosts { get; set; }
}