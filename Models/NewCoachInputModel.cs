using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class NewCoachInputModel
{
    [Required(ErrorMessage = "İsim gereklidir"), MaxLength(128)]
    public string FullName { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? ExpertiseTags { get; set; }

    [MaxLength(256)]
    public string? ServicesOffered { get; set; }

    [MaxLength(512)]
    public string? Bio { get; set; }

    public List<int> ServiceIds { get; set; } = new();
}
