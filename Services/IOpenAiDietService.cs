using System.Threading;
using System.Threading.Tasks;
using WebProje.Models;

namespace WebProje.Services;

public interface IOpenAiDietService
{
    Task<AiDietPlanResult?> GeneratePlanAsync(DietPlanRequestContext context, CancellationToken cancellationToken = default);
}

public sealed record AiDietPlanResult(AiDietPlanSuggestion Suggestion, string? Model, string Provider);
