using System.ComponentModel.DataAnnotations;

namespace EpiRead.Models.Database;

public class Role
{
    [Key] public int Id { get; set; }

    [MaxLength(100)] public string Name { get; set; } = string.Empty;
}