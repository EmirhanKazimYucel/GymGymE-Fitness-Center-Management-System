using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class NewServiceInputModel
{
    [Required(ErrorMessage = "Ad gerekli"), MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    public int? DurationMinutes { get; set; }

    public decimal? Price { get; set; }
}
