using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class Bookmark
{
    [Key] public int Id { get; set; }

    public int Page { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey("User")] public int UserId { get; set; }

    [ForeignKey("Book")] public int BookId { get; set; }

    [ForeignKey("BookDocument")] public int BookDocumentId { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public virtual Book? Book { get; set; }

    public virtual BookDocument? BookDocument { get; set; }
}