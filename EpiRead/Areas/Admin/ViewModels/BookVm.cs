using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EpiRead.Areas.Admin.ViewModels;

public class CreateBookVm
{
    [DisplayName("Book Title")]
    [StringLength(255)]
    [Required(ErrorMessage = "Book title is required")]
    public string Title { get; set; } = string.Empty;

    [DisplayName("Author")]
    [StringLength(255)]
    [Required(ErrorMessage = "Author name is required")]
    public string AuthorName { get; set; } = string.Empty;

    [DisplayName("Description")]
    [StringLength(5000)]
    public string? Description { get; set; }

    [DisplayName("Publication Year")]
    [PublicationYearRange(ErrorMessage = "Select a valid year")]
    public int? PublicationYear { get; set; }

    [DisplayName("Language")]
    [StringLength(10)]
    [Required(ErrorMessage = "Select at least one language")]
    public string Language { get; set; } = nameof(LanguageCodeType.EN);

    [DisplayName("Category")] public List<int> CategoryIds { get; set; } = new(); // Multiple BookCategory Ids

    [DisplayName("Cover Image")] public IFormFile? CoverImage { get; set; }
}

public class EditBookVm
{
    public int Id { get; set; }

    [DisplayName("Book Title")]
    [StringLength(255)]
    [Required(ErrorMessage = "Book title is required")]
    public string Title { get; set; } = string.Empty;

    [DisplayName("Author")]
    [StringLength(255)]
    [Required(ErrorMessage = "Author name is required")]
    public string AuthorName { get; set; } = string.Empty;

    [DisplayName("Description")]
    [StringLength(5000)]
    public string? Description { get; set; }

    [DisplayName("Publication Year")]
    [PublicationYearRange(ErrorMessage = "Select a valid year")]
    public int? PublicationYear { get; set; }

    [DisplayName("Language")]
    [StringLength(10)]
    [Required(ErrorMessage = "Select at least one language")]
    public string Language { get; set; } = nameof(LanguageCodeType.EN);

    [DisplayName("Cover Image Path")] public string? CoverImagePath { get; set; }

    [DisplayName("Cover Image Link")] public string? CoverImageLink { get; set; }

    [DisplayName("Status")] public string? Status { get; set; } = string.Empty;

    [DisplayName("Category")] public List<int> CategoryIds { get; set; } = new(); // Multiple BookCategory Ids

    [DisplayName("Cover Image")] public IFormFile? CoverImage { get; set; }
}

public class EditEbookVm
{
    public int BookId { get; set; }

    public Book Book { get; set; } = new();

    // Existing ebook documents
    public List<BookDocument>? ExistingEbooks { get; set; }

    // IDs of ebooks to delete
    public List<int>? EbooksToDelete { get; set; }

    // Updated custom names for existing ebooks (matched by BookDocument Id)
    public Dictionary<int, string>? UpdatedNames { get; set; }

    // New files to add
    [DisplayName("New Ebook Files")] public List<IFormFile>? NewEbookFiles { get; set; }

    // Custom names for new files
    public List<string>? NewCustomNames { get; set; }
}

public class BookUploadZipVm
{
    [Required(ErrorMessage = "Please select a ZIP file.")]
    [DataType(DataType.Upload)]
    public IFormFile? ZipFile { get; set; }
}

public class BookImportRow
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PublicationYear { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string? CoverImagePath { get; set; }

    public string? CoverImageLink { get; set; }

    // Optional ownership columns added to support User FK during import/export
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookCategoryMapImportRow
{
    public string BookTitle { get; set; } = string.Empty; // Match by title instead of ID
    public string BookAuthorName { get; set; } = string.Empty; // Also match by author for uniqueness

    public int BookCategoryId { get; set; }

    // Export now includes category name to allow imports on different systems where IDs differ
    public string? BookCategoryName { get; set; }
}

public class BookDocumentImportRow
{
    public string BookTitle { get; set; } = string.Empty; // Match by title instead of ID
    public string BookAuthorName { get; set; } = string.Empty; // Also match by author for uniqueness
    public string FormatType { get; set; } = string.Empty;
    public string DocumentPath { get; set; } = string.Empty;
    public string DocumentLink { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookImportConflictItem
{
    public BookImportRow Row { get; set; } = new();
    public string? ExistingBookTitle { get; set; }
    public Book? ExistingBook { get; set; } // Full existing book data for comparison
    public string ConflictType { get; set; } = string.Empty; // e.g. "New", "Duplicate", "Title Conflict"
    public string SuggestedAction { get; set; } = string.Empty; // e.g. "Add", "Overwrite", "Skip", "Merge"
    public List<string> Categories { get; set; } = new(); // Categories from imported CSV
}

public class BookImportConflictVm
{
    public List<BookImportConflictItem> Items { get; set; } = new();
}

public class BookImportConfirmItemVm
{
    public string Title { get; set; } = string.Empty;
    public string AuthorName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int? PublicationYear { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string? CoverImagePath { get; set; }

    public string? CoverImageLink { get; set; }

    // Optional ownership mapping from exported CSV
    public int? UserId { get; set; }
    public string? UserEmail { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Action { get; set; } = string.Empty; // Overwrite, Skip, Merge, Add
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookImportConfirmVm
{
    public List<BookImportConfirmItemVm> Items { get; set; } = new();
}

public class BookImportSummaryVm
{
    public int Added { get; set; } = 0;
    public int Skipped { get; set; } = 0;
    public int Unresolved { get; set; } = 0;
    public int Updated { get; set; } = 0;
}