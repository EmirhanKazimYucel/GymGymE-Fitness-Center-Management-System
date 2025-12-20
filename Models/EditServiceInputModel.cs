using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class EditServiceInputModel
{
    [Range(1, int.MaxValue, ErrorMessage = "Geçersiz hizmet kimliği.")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Ad gerekli"), MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Description { get; set; }

    [Range(0, 1440, ErrorMessage = "Süre negatif olamaz.")]
    public int? DurationMinutes { get; set; }

    [Range(0, 100000, ErrorMessage = "Ücret negatif olamaz.")]
    public decimal? Price { get; set; }
}
