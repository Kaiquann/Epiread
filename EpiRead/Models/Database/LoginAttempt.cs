using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class LoginAttempt
{
    [Key] public int Id { get; set; }

    public int TryAttempt { get; set; }

    [MaxLength(255)] public string? Ip { get; set; } = string.Empty;

    public bool IsBlocked { get; set; } = false;

    public DateTime ExpiredAt { get; set; } = DateTime.Now.ToLocalTime().AddMinutes(15);

    // FK
    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual User? User { get; set; }
}