using System;
using System.Collections.Generic;

namespace WebProje.Models;

public class DietPlanViewModel
{
    public string UserFullName { get; init; } = string.Empty;
    public double? BodyMassIndex { get; init; }
    public string BmiCategory { get; init; } = "—";
    public int SuggestedCalories { get; init; }
    public MacroDistribution MacroSplit { get; init; } = new();
    public double HydrationLiters { get; init; }
    public IReadOnlyList<string> FocusTips { get; init; } = Array.Empty<string>();
    public IReadOnlyList<DietMealIdea> MealIdeas { get; init; } = Array.Empty<DietMealIdea>();
}

public class MacroDistribution
{
    public int CarbsPercent { get; init; }
    public int ProteinPercent { get; init; }
    public int FatPercent { get; init; }
}

public class DietMealIdea
{
    public string Meal { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string Accent { get; init; } = "#ff80ab";
}
