using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace EpiRead.Models.Database;

[Index(nameof(UserName), IsUnique = true)]
public class User
{
    [Key] public int Id { get; set; }

    [MaxLength(100)] public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)] public string LastName { get; set; } = string.Empty;

    [MaxLength(100)] public string UserName { get; set; } = string.Empty;

    [MaxLength(255)] public string Email { get; set; } = string.Empty;

    [MaxLength(255)] public string Password { get; set; } = string.Empty;

    public DateOnly? DateOfBirth { get; set; }

    [MaxLength(50)] public string Language { get; set; } = nameof(LanguageCodeType.EN);

    [MaxLength(255)] public string? ProfileImagePath { get; set; }

    [MaxLength(500)] public string? ProfileImageLink { get; set; }

    [MaxLength(50)] public string Role { get; set; } = nameof(UserRoleType.Member);

    [MaxLength(50)] public string? GoogleMfaSecretKey { get; set; }

    public bool MfaEnabled { get; set; } = false;

    [MaxLength(50)] public string Status { get; set; } = nameof(UserStatusType.Active);

    [MaxLength(255)] public string? BlockReason { get; set; }

    public DateTime? DeletedOn { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // Navigation properties
    public virtual ICollection<Book>? Books { get; set; }

    public virtual ICollection<LoginHistory>? LoginHistory { get; set; }

    public virtual ICollection<ChatSession>? ChatSessions { get; set; }

    public virtual ICollection<ChatMessage>? ChatMessages { get; set; }

    public virtual ICollection<SearchHistory>? SearchHistories { get; set; }

    public virtual ICollection<BookHistory>? BookHistories { get; set; }

    public virtual ICollection<PreferenceBook>? PreferenceBooks { get; set; }

    public virtual ICollection<Bookmark>? Bookmarks { get; set; }

    public virtual ICollection<Rating>? Ratings { get; set; }

    public virtual ICollection<DiscussionPost>? DiscussionPosts { get; set; }

    public virtual ICollection<DiscussionComment>? DiscussionComments { get; set; }

    public virtual ICollection<DiscussionLikeCount>? DiscussionLikeCounts { get; set; }
}