using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Models.Database;

[Index(nameof(Slug), IsUnique = true)]
public class BookCategory
{
    [Key] public int Id { get; set; }

    [MaxLength(100)] public string Name { get; set; } = string.Empty;

    [MaxLength(100)] public string Slug { get; set; } = string.Empty;

    [MaxLength(255)] public string? Description { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(BookCategory))] public int? ParentId { get; set; }

    // Navigation
    public virtual BookCategory? Parent { get; set; }

    public virtual ICollection<BookCategory>? SubBookCategories { get; set; }

    public virtual ICollection<Book>? Books { get; set; }

    public virtual ICollection<BookCategoryMap>? BookCategoryMaps { get; set; }
}