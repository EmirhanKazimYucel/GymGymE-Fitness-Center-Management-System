using System;
using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class GymInfoViewModel
{
    [Required(ErrorMessage = "Salon adı gerekli."), MaxLength(128)]
    [Display(Name = "Salon Adı")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    [Display(Name = "Adres")]
    public string? Address { get; set; }

    [MaxLength(64)]
    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [EmailAddress(ErrorMessage = "Geçerli bir e-posta giriniz."), MaxLength(128)]
    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Url(ErrorMessage = "Geçerli bir web adresi giriniz."), MaxLength(256)]
    [Display(Name = "Web Sitesi")]
    public string? Website { get; set; }

    [MaxLength(128)]
    [Display(Name = "Hafta içi saatleri")]
    public string? WeekdayHours { get; set; }

    [MaxLength(128)]
    [Display(Name = "Hafta sonu saatleri")]
    public string? WeekendHours { get; set; }

    [MaxLength(512)]
    [Display(Name = "Hakkında / Açıklama")]
    public string? About { get; set; }

    [MaxLength(512)]
    [Display(Name = "Öne çıkan imkanlar")]
    public string? Facilities { get; set; }

    public DateTime? UpdatedAtUtc { get; set; }
}
