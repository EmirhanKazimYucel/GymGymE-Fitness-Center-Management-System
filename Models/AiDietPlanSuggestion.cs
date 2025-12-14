using System.Collections.Generic;

namespace WebProje.Models;

public class AiDietPlanSuggestion
{
    public int? SuggestedCalories { get; set; }
    public double? HydrationLiters { get; set; }
    public MacroDistribution? MacroSplit { get; set; }
    public IReadOnlyList<string> FocusTips { get; set; } = Array.Empty<string>();
    public IReadOnlyList<DietMealIdea> Meals { get; set; } = Array.Empty<DietMealIdea>();
    public IReadOnlyList<string> Cautions { get; set; } = Array.Empty<string>();
    public string? MotivationMessage { get; set; }
    public string? PlanTitle { get; set; }
}
