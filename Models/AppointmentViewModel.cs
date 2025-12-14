using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class AppointmentViewModel
{
    [Display(Name = "Ad Soyad")]
    public string UserFullName { get; set; } = string.Empty;

    [Display(Name = "E-posta")]
    public string UserEmail { get; set; } = string.Empty;

    [Display(Name = "Telefon")]
    public string? UserPhone { get; set; }

    [Required(ErrorMessage = "Hizmet seçiniz.")]
    [Display(Name = "Hizmet")]
    public int? SelectedServiceId { get; set; }

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

    [Display(Name = "Not / Hedef")]
    [MaxLength(256, ErrorMessage = "Not 256 karakteri aşamaz.")]
    public string? Notes { get; set; }

    public IReadOnlyList<string> AvailableCoaches { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> TimeSlots { get; set; } = Array.Empty<string>();
    public IReadOnlyList<ServiceOption> Services { get; set; } = Array.Empty<ServiceOption>();
    public IDictionary<int, IReadOnlyList<string>> ServiceCoachMap { get; set; }
        = new Dictionary<int, IReadOnlyList<string>>();
}
