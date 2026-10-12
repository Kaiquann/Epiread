using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class ChatSession
{
    [Key] public Guid Id { get; set; }

    [MaxLength(255)] public string Title { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual User? User { get; set; }

    public virtual ICollection<ChatMessage>? ChatMessages { get; set; }
}