using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace WebProje.Models;

public class UserProfileViewModel
{
    [Required, MaxLength(64)]
    [Display(Name = "Ad")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    [Display(Name = "Soyad")]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    [Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Telefon")]
    public string? PhoneNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "Doğum Tarihi")]
    public DateOnly? BirthDate { get; set; }

    [Range(80, 250, ErrorMessage = "Boy 80-250 cm arasında olmalıdır.")]
    [Display(Name = "Boy (cm)")]
    public double? HeightCm { get; set; }

    [Range(30, 250, ErrorMessage = "Kilo 30-250 kg arasında olmalıdır.")]
    [Display(Name = "Kilo (kg)")]
    public double? WeightKg { get; set; }

    [Range(30, 250, ErrorMessage = "Hedef kilo 30-250 kg arasında olmalıdır.")]
    [Display(Name = "Hedef Kilo (kg)")]
    public double? TargetWeightKg { get; set; }

    [Display(Name = "Motivasyon / Hedef")]
    public DietGoal DietGoal { get; set; } = DietGoal.Unspecified;

    [Display(Name = "Aktivite Seviyesi")]
    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Moderate;

    [MaxLength(512)]
    [Display(Name = "Sağlık Durumları / Hastalıklar")]
    public string? HealthConditions { get; set; }

    [MaxLength(512)]
    [Display(Name = "Alerjiler veya İntoleranslar")]
    public string? Allergies { get; set; }

    [MaxLength(512)]
    [Display(Name = "Özel Notlar / Tercihler")]
    public string? SpecialNotes { get; set; }

    [Display(Name = "Profil Fotoğrafı")]
    public IFormFile? AvatarUpload { get; set; }

    public string? AvatarUrl { get; set; }
    public double? BodyMassIndex { get; set; }
    public string BmiCategory { get; set; } = "—";
}
