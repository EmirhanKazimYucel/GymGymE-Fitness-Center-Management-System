using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public enum DietGoal
{
    [Display(Name = "Belirtmedim")]
    Unspecified = 0,

    [Display(Name = "Kilo Vermek")]
    LoseWeight = 1,

    [Display(Name = "Kilo Almak")]
    GainWeight = 2,

    [Display(Name = "Kas Geliştirmek")]
    BuildMuscle = 3,

    [Display(Name = "Formu Koruma")]
    Maintain = 4
}

public enum ActivityLevel
{
    [Display(Name = "Sedanter / Masa başı")]
    Sedentary = 0,

    [Display(Name = "Hafif akt. (1-2 gün)")]
    Light = 1,

    [Display(Name = "Orta akt. (3-4 gün)")]
    Moderate = 2,

    [Display(Name = "Aktif (5+ gün)")]
    Active = 3,

    [Display(Name = "Atletik / Yüksek")]
    Athlete = 4
}
