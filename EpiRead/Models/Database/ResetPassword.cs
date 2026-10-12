using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace EpiRead.Models.Database;

public class ResetPassword
{
    [Key] public int Id { get; set; }

    [MaxLength(255)] public string Token { get; set; } = string.Empty;

    public DateTime ExpiresAt { get; set; } = DateTime.Now.ToLocalTime().AddMinutes(15);

    public DateTime CreatedAt { get; set; } = DateTime.Now.ToLocalTime();

    public DateTime UpdatedAt { get; set; } = DateTime.Now.ToLocalTime();

    // FK
    [ForeignKey(nameof(User))] public int UserId { get; set; }

    // Navigation
    public virtual User? User { get; set; }
}