using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class PreferenceBook
{
    [Key] public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(Book))] public int BookId { get; set; }

    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual Book? Book { get; set; }

    public virtual User? User { get; set; }
}