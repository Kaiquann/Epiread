using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EpiRead.Areas.Author.ViewModels;

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