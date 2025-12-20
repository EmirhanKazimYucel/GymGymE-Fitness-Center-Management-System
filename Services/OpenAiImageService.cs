using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WebProje.Configuration;
using WebProje.Models;

namespace WebProje.Services;

public sealed class OpenAiImageService : IOpenAiImageService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] SupportedImageSizes =
    {
        "1024x1024",
        "1024x1792",
        "1792x1024"
    };
    private const string DefaultImageSize = "1024x1024";
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiImageService(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<IReadOnlyList<DietMealVisual>> GenerateMealVisualsAsync(
        DietPlanRequestContext context,
        IReadOnlyList<DietMealIdea> meals,
        CancellationToken cancellationToken = default)
    {
        if (context is null || meals is null || meals.Count == 0)
        {
            return Array.Empty<DietMealVisual>();
        }

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API anahtarı yapılandırılmadığı için görsel üretilemedi.");
        }

        var modelName = string.IsNullOrWhiteSpace(_options.ImageModel) ? "gpt-image-1" : _options.ImageModel;
        var requestedSize = string.IsNullOrWhiteSpace(_options.ImageSize)
            ? DefaultImageSize
            : _options.ImageSize.Trim();
        var size = SupportedImageSizes.FirstOrDefault(s => string.Equals(s, requestedSize, StringComparison.OrdinalIgnoreCase))
            ?? DefaultImageSize;
        var promptVariants = BuildCompositePromptVariants(context, meals).ToArray();
        var legend = BuildMealLegend(meals);
        var accent = meals.FirstOrDefault()?.Accent ?? "#ff80ab";

        for (var variantIndex = 0; variantIndex < promptVariants.Length; variantIndex++)
        {
            var prompt = promptVariants[variantIndex];
            try
            {
                var imagePayload = await RequestBase64ImageAsync(modelName, size, prompt, cancellationToken);
                if (string.IsNullOrWhiteSpace(imagePayload))
                {
                    continue;
                }

                var dataUrl = $"data:image/png;base64,{imagePayload}";
                return new[]
                {
                    new DietMealVisual
                    {
                        Meal = "Günlük Menü",
                        Description = legend,
                        ImageUrl = dataUrl,
                        Accent = accent,
                        Prompt = prompt
                    }
                };
            }
            catch (InvalidOperationException ex) when (IsSafetyRejection(ex.Message) && variantIndex < promptVariants.Length - 1)
            {
                continue;
            }
        }

        return Array.Empty<DietMealVisual>();
    }

    private async Task<string?> RequestBase64ImageAsync(string modelName, string size, string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = modelName,
            prompt,
            size,
            quality = "standard",
            n = 1,
            response_format = "b64_json"
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, "images/generations")
        {
            Content = new StringContent(JsonSerializer.Serialize(requestBody, SerializerOptions), Encoding.UTF8, "application/json")
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorPayload = await response.Content.ReadAsStringAsync(cancellationToken);
            var detail = SummarizeError(errorPayload, response.ReasonPhrase);
            throw new InvalidOperationException($"OpenAI görsel servisi {(int)response.StatusCode} yanıtı verdi: {detail}");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        if (!json.RootElement.TryGetProperty("data", out var dataArray) || dataArray.GetArrayLength() == 0)
        {
            return null;
        }

        return dataArray[0].GetProperty("b64_json").GetString();
    }

    private static IEnumerable<string> BuildCompositePromptVariants(DietPlanRequestContext context, IReadOnlyList<DietMealIdea> meals)
    {
        yield return BuildCompositePrompt(context, meals, includeUserContext: true);
        yield return BuildCompositePrompt(context, meals, includeUserContext: false);
    }

    private static string BuildCompositePrompt(DietPlanRequestContext context, IReadOnlyList<DietMealIdea> meals, bool includeUserContext)
    {
        var plateDescriptions = meals.Select(m =>
            $"{(string.IsNullOrWhiteSpace(m.Meal) ? "Öğün" : m.Meal)} tabagı: {m.Description}");
        var courseDetails = string.Join("; ", plateDescriptions);
        var plateCount = meals.Count;
        if (!includeUserContext)
        {
            return $"High-end food photography collage of {plateCount} plates on one large marble table. {courseDetails}. Pastel accessories, natural light, editorial styling, no text, no branding.";
        }

        var name = string.IsNullOrWhiteSpace(context.UserFullName) ? "member" : context.UserFullName;
        var bmi = context.BodyMassIndex.HasValue ? context.BodyMassIndex.Value.ToString("0.0") : "unknown";
        var goal = context.DietGoal.ToString();
        return $"DALL-E photorealistic overhead collage for {name} (BMI {bmi}, goal {goal}). Show {plateCount} distinct plates grouped on a single elegant table: {courseDetails}. Natural daylight, real ingredients, lifestyle magazine style.";
    }

    private static string BuildMealLegend(IReadOnlyList<DietMealIdea> meals)
    {
        if (meals.Count == 0)
        {
            return "Öğün detaylarına ulaşılamadı.";
        }

        return string.Join(" • ", meals.Select(m =>
        {
            var title = string.IsNullOrWhiteSpace(m.Meal) ? "Öğün" : m.Meal;
            return string.IsNullOrWhiteSpace(m.Description) ? title : $"{title}: {m.Description}";
        }));
    }

    private static string SummarizeError(string? payload, string? fallbackReason)
    {
        if (string.IsNullOrWhiteSpace(payload))
        {
            return string.IsNullOrWhiteSpace(fallbackReason) ? "Bilinmeyen hata" : fallbackReason!;
        }

        try
        {
            using var document = JsonDocument.Parse(payload);
            if (document.RootElement.TryGetProperty("error", out var errorElement))
            {
                if (errorElement.ValueKind == JsonValueKind.Object && errorElement.TryGetProperty("message", out var messageElement))
                {
                    var message = messageElement.GetString();
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message!;
                    }
                }

                var rawError = errorElement.ToString();
                if (!string.IsNullOrWhiteSpace(rawError))
                {
                    return rawError.Length > 200 ? rawError[..200] + "..." : rawError;
                }
            }
        }
        catch
        {
            // ignore JSON parse issues and fall back to the raw payload snippet
        }

        var trimmed = payload.Trim();
        return trimmed.Length > 200 ? trimmed[..200] + "..." : trimmed;
    }

    private static bool IsSafetyRejection(string? message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return message.IndexOf("safety system", StringComparison.OrdinalIgnoreCase) >= 0
            || message.IndexOf("safety", StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
