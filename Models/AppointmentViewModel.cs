using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class AppointmentViewModel
{
    [Required(ErrorMessage = "Tarih seçiniz.")]
    [DataType(DataType.Date)]
    [Display(Name = "Tarih")]
    public DateOnly SelectedDate { get; set; }

    [Required(ErrorMessage = "Saat seçiniz.")]
    [Display(Name = "Saat")]
    public string SelectedTime { get; set; } = string.Empty;

    [Required(ErrorMessage = "Koç seçiniz.")]
    [Display(Name = "Koç")]
    public string SelectedCoach { get; set; } = string.Empty;

    public IReadOnlyList<string> AvailableCoaches { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TimeSlots { get; set; } = Array.Empty<string>();
}
