using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace EpiRead.Areas.Author.ViewModels;

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

    [DisplayName("Category")] public List<int> CategoryIds { get; set; } = new();

    [DisplayName("Cover Image")] public IFormFile? CoverImage { get; set; }
}