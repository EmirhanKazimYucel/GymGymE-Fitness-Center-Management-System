using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public DateOnly? BirthDate { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Role { get; set; } = RoleNames.User;

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public double? HeightCm { get; set; }

    public double? WeightKg { get; set; }

    [MaxLength(256)]
    public string? AvatarPath { get; set; }

    public double? TargetWeightKg { get; set; }

    public DietGoal DietGoal { get; set; } = DietGoal.Unspecified;

    public ActivityLevel ActivityLevel { get; set; } = ActivityLevel.Moderate;

    [MaxLength(512)]
    public string? HealthConditions { get; set; }

    [MaxLength(512)]
    public string? Allergies { get; set; }

    [MaxLength(512)]
    public string? SpecialNotes { get; set; }
}
