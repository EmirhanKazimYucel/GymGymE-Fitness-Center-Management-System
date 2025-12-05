using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace WebProje.Models;

public class Service
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    public int? DurationMinutes { get; set; }

    [Column(TypeName = "numeric(10,2)")]
    public decimal? Price { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public ICollection<CoachService> CoachServices { get; set; } = new List<CoachService>();
}
