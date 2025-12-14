using System;
using System.Linq;

namespace WebProje.Models;

public sealed class DietPlanRequestContext
{
    public string UserFullName { get; init; } = string.Empty;
    public string? Email { get; init; }
    public DateOnly? BirthDate { get; init; }
    public int? AgeYears { get; init; }
    public double? HeightCm { get; init; }
    public double? WeightKg { get; init; }
    public double? TargetWeightKg { get; init; }
    public DietGoal DietGoal { get; init; } = DietGoal.Unspecified;
    public ActivityLevel ActivityLevel { get; init; } = ActivityLevel.Moderate;
    public double? BodyMassIndex { get; init; }
    public string BmiCategory { get; init; } = "—";
    public string? HealthConditions { get; init; }
    public string? Allergies { get; init; }
    public string? SpecialNotes { get; init; }

    public static DietPlanRequestContext FromUser(AppUser user, double? bmi, string bmiCategory)
    {
        ArgumentNullException.ThrowIfNull(user);
        var fullName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(fullName))
        {
            fullName = user.Email;
        }

        return new DietPlanRequestContext
        {
            UserFullName = fullName,
            Email = user.Email,
            BirthDate = user.BirthDate,
            AgeYears = CalculateAge(user.BirthDate),
            HeightCm = user.HeightCm,
            WeightKg = user.WeightKg,
            TargetWeightKg = user.TargetWeightKg,
            DietGoal = user.DietGoal,
            ActivityLevel = user.ActivityLevel,
            BodyMassIndex = bmi,
            BmiCategory = bmiCategory,
            HealthConditions = user.HealthConditions,
            Allergies = user.Allergies,
            SpecialNotes = user.SpecialNotes
        };
    }

    private static int? CalculateAge(DateOnly? birthDate)
    {
        if (birthDate is null)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        var age = today.Year - birthDate.Value.Year;
        if (birthDate.Value > today.AddYears(-age))
        {
            age--;
        }

        return Math.Max(age, 0);
    }
}
