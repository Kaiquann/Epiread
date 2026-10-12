using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class BookHistory
{
    [Key] public int Id { get; set; }

    public int BookProgress { get; set; } = 0;

    public int CurrentPage { get; set; } = 0;

    public int TotalPages { get; set; } = 0;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(User))] public int UserId { get; set; }

    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    [ForeignKey(nameof(BookDocument))] public int BookDocumentId { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public virtual Book? Book { get; set; }

    public virtual BookDocument? BookDocument { get; set; }
}