using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using WebProje.Models;

namespace WebProje.Services;

public interface IOpenAiImageService
{
    Task<IReadOnlyList<DietMealVisual>> GenerateMealVisualsAsync(
        DietPlanRequestContext context,
        IReadOnlyList<DietMealIdea> meals,
        CancellationToken cancellationToken = default);
}
