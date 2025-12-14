using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class Coach
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ExpertiseTags { get; set; }

    [MaxLength(256)]
    public string? ServicesOffered { get; set; }

    [MaxLength(512)]
    public string? Bio { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CoachService> CoachServices { get; set; } = new List<CoachService>();
}
