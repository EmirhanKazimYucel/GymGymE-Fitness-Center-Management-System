using System;
using System.Collections.Generic;

namespace WebProje.Models;

public class DietPlanViewModel
{
    public string UserFullName { get; set; } = string.Empty;
    public double? BodyMassIndex { get; set; }
    public string BmiCategory { get; set; } = "—";
    public int SuggestedCalories { get; set; }
    public MacroDistribution MacroSplit { get; set; } = new();
    public double HydrationLiters { get; set; }
    public IReadOnlyList<string> FocusTips { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DietMealIdea> MealIdeas { get; set; } = Array.Empty<DietMealIdea>();
    public DietPlanAiMetadata Ai { get; set; } = new();
}

public class MacroDistribution
{
    public int CarbsPercent { get; set; }
    public int ProteinPercent { get; set; }
    public int FatPercent { get; set; }
}

public class DietMealIdea
{
    public string Meal { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Accent { get; set; } = "#ff80ab";
}

public class DietPlanAiMetadata
{
    public bool GeneratedByAi { get; set; }
    public string Source { get; set; } = "BarbieFit Standart";
    public string? Model { get; set; }
    public string? MotivationMessage { get; set; }
    public IReadOnlyList<string> Cautions { get; set; } = Array.Empty<string>();
    public string? ErrorMessage { get; set; }
}
