using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace EpiRead.Areas.Admin.ViewModels;

public class CreateBookCategoryVm
{
    public int Id { get; set; }

    [DisplayName("Name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [DisplayName("Slug")]
    [StringLength(100)]
    [Lowercase(ErrorMessage = "Slug must be lowercase.")]
    [Remote("IsBookCategorySlugValid", nameof(BookCategory), "Admin",
        ErrorMessage = "Slug is invalid or already exists.", HttpMethod = "POST", AdditionalFields = nameof(Id))]
    public string Slug { get; set; } = string.Empty;

    [StringLength(255)] public string Description { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public SelectList? ParentBookCategories { get; set; }
}

public class EditBookCategoryVm
{
    public int Id { get; set; }

    [DisplayName("Name")]
    [StringLength(100)]
    public string Name { get; set; } = string.Empty;

    [DisplayName("Slug")]
    [StringLength(100)]
    [Remote("IsBookCategorySlugValid", nameof(BookCategory), "Admin",
        ErrorMessage = "Slug is invalid or already exists.", HttpMethod = "POST", AdditionalFields = nameof(Id))]
    public string Slug { get; set; } = string.Empty;

    [StringLength(255)] public string Description { get; set; } = string.Empty;

    public int? ParentId { get; set; }

    public SelectList? ParentBookCategories { get; set; }
}

public class BookCategoryUploadCsvVm
{
    [Required(ErrorMessage = "Please select a ZIP file.")]
    [DataType(DataType.Upload)]
    public IFormFile? CsvFile { get; set; }
}

public class BookCategoryImportRow
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ParentCategory { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookCategoryImportConflictItem
{
    public BookCategoryImportRow Row { get; set; } = new();
    public string? ExistingCategoryName { get; set; }
    public string ConflictType { get; set; } = string.Empty; // e.g. "New", "Duplicate", "Name Conflict"
    public string SuggestedAction { get; set; } = string.Empty; // e.g. "Add", "Overwrite", "Skip", "Merge"
}

public class BookCategoryImportConflictVm
{
    public List<BookCategoryImportConflictItem> Items { get; set; } = new();
}

public class BookCategoryImportConfirmItemVm
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string? ParentCategory { get; set; }
    public string Action { get; set; } = string.Empty; // Overwrite, Skip, Merge, Add
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class BookCategoryImportConfirmVm
{
    public List<BookCategoryImportConfirmItemVm> Items { get; set; } = new();
}

public class BookCategoryImportSummaryVm
{
    public int Added { get; set; } = 0;
    public int Skipped { get; set; } = 0;
    public int Unresolved { get; set; } = 0;
    public int Updated { get; set; } = 0;
}

public class BookCategoryShowVm
{
    public BookCategory BookCategory { get; set; } = null!;
    public IPagedList<Book> PagedBooks { get; set; } = null!;
}

public class BookCategoryTreeItemVm
{
    public BookCategory Category { get; set; } = null!;
    public int Level { get; set; } = 0;
    public string? ParentName { get; set; }
}