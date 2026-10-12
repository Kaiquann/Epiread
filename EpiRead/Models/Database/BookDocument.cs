using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class BookDocument
{
    [Key] public int Id { get; set; }

    [MaxLength(20)] public string FormatType { get; set; } = string.Empty;

    [MaxLength(255)] public string DocumentPath { get; set; } = string.Empty;

    [MaxLength(500)] public string DocumentLink { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    // Navigation
    public virtual Book? Book { get; set; }

    public virtual ICollection<BookHistory>? BookHistories { get; set; }

    public virtual ICollection<Bookmark>? Bookmarks { get; set; }
}