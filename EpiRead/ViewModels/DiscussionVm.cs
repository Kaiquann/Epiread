using System.ComponentModel.DataAnnotations;

namespace EpiRead.ViewModels;

public class DiscussionVm
{
    public Book? Book { get; set; }
    public List<DiscussionPost> DiscussionPosts { get; set; } = new();
    public BookCategory? BookCategory { get; set; }
    public double AverageRating { get; set; }
    public int DiscussionId { get; set; }
    public User? User { get; set; }
    public string? Search { get; set; }
    public string? SortBy { get; set; }
}

public class DiscussionPostVm
{
    public DiscussionPost Post { get; set; }
    public List<DiscussionComment> Comments { get; set; } = new();
    public User? User { get; set; }

    public int? ParentCommentId { get; set; }
    public List<DiscussionComment>? Replies { get; set; }
}

public class CreatePostVm
{
    [Required] public int DiscussionId { get; set; }

    [Required] public int BookId { get; set; }

    [Required(ErrorMessage = "Please enter a title.")]
    [StringLength(30, ErrorMessage = "Title cannot exceed 30 characters.")]
    public string Title { get; set; }
}