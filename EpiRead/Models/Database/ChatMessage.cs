using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class ChatMessage
{
    [Key] public Guid Id { get; set; }

    [MaxLength(50)]
    [RegularExpression("^(user|bot)$")]
    public string Sender { get; set; } = string.Empty;

    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(ChatSession))] public Guid ChatSessionId { get; set; }

    // Navigation
    public virtual ChatSession? ChatSession { get; set; }
}