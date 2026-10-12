using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class Book
{
    [Key] public int Id { get; set; }

    [MaxLength(255)] public string AuthorName { get; set; } = string.Empty;

    [MaxLength(255)] public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public DateOnly? PublicationYear { get; set; }

    [MaxLength(10)] public string LanguageCode { get; set; } = nameof(LanguageCodeType.EN);

    [MaxLength(255)] public string? CoverImagePath { get; set; }

    [MaxLength(500)] public string? CoverImageLink { get; set; }

    [MaxLength(50)] public string Status { get; set; } = nameof(BookStatusType.Available);

    public DateTime? DeletedOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(User))] public int? UserId { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public virtual ICollection<Discussion>? Discussions { get; set; }

    public virtual ICollection<BookCategory>? BookCategories { get; set; }

    public virtual ICollection<BookCategoryMap>? BookCategoryMaps { get; set; }
}