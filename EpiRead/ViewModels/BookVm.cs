using System.ComponentModel;

namespace EpiRead.ViewModels;

public class BookVm
{
    [DisplayName("Book ID")] public int Id { get; set; }
    [DisplayName("Author Name")] public string AuthorName { get; set; } = string.Empty;
    [DisplayName("Title")] public string Title { get; set; } = string.Empty;
    [DisplayName("Description")] public string? Description { get; set; } = string.Empty;
    [DisplayName("Publication Year")] public DateOnly? PublicationYear { get; set; }
    [DisplayName("Language Code")] public string? LanguageCode { get; set; } = string.Empty;
    [DisplayName("Cover Image Path")] public string? CoverImageLink { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public double AverageRating { get; set; }
    public bool CanRate { get; set; }
    public int? UserRatingValue { get; set; }
    public List<Book>? RelatedBooks { get; set; } = [];
    public bool IsPreferred { get; set; }
    public List<BookCategory>? BookCategory { get; set; } = [];
    public List<BookCategory>? TopBookCategory { get; set; } = [];
    public List<SearchHistory>? SearchHistory { get; set; } = [];
    public List<User> AuthorList { get; set; } = [];
    public List<BookDocument>? DocumentList { get; set; } = [];
}

public class BookCategoriesVm
{
    [DisplayName("Category ID")] public int Id { get; set; }

    [DisplayName("Category Name")] public string Name { get; set; } = string.Empty;

    [DisplayName("Slug")] public string Slug { get; set; } = string.Empty;

    [DisplayName("Description")] public string Description { get; set; } = string.Empty;

    [DisplayName("Created At")] public DateTime CreatedAt { get; set; } = DateTime.Now;

    [DisplayName("Updated At")] public DateTime UpdatedAt { get; set; } = DateTime.Now;

    [DisplayName("Parent Category ID")] public int? ParentId { get; set; }

    // Optional: for linking to subcategories or books
    public List<BookVm>? Books { get; set; }
}

public class BookHistoryVm
{
    public List<BookHistory>? BookHistoryList { get; set; } = [];

    public List<BookDocument>? DocumentList { get; set; } = [];

    public List<Bookmark> BookmarkList { get; set; } = [];

    // The current user's id running this view, used to filter bookmark checks on the view
    public int? CurrentUserId { get; set; }
}

public class ParentCategoryPopularityDto
{
    public string ParentCategoryName { get; set; }
    public int TotalPopularity { get; set; }
}