using System;
using System.Globalization;
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

public sealed class OpenAiDietService : IOpenAiDietService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;

    public OpenAiDietService(HttpClient httpClient, IOptions<OpenAiOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public async Task<AiDietPlanResult?> GeneratePlanAsync(DietPlanRequestContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return null;
        }

        var request = BuildRequest(context);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(JsonSerializer.Serialize(request, SerializerOptions), Encoding.UTF8, "application/json")
        };
        httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"OpenAI returned {(int)response.StatusCode}: {errorBody}");
        }

        await using var payload = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(payload, cancellationToken: cancellationToken);

        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            return null;
        }

        var suggestion = JsonSerializer.Deserialize<AiDietPlanSuggestion>(content, SerializerOptions);
        if (suggestion is null)
        {
            return null;
        }

        return new AiDietPlanResult(suggestion, _options.Model, "OpenAI");
    }

    private object BuildRequest(DietPlanRequestContext context)
    {
        var userPrompt = BuildUserPrompt(context);
        var model = string.IsNullOrWhiteSpace(_options.Model) ? "gpt-4.1-mini" : _options.Model;

        return new
        {
            model,
            temperature = _options.Temperature,
            max_tokens = _options.MaxOutputTokens,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt() },
                new { role = "user", content = userPrompt }
            }
        };
    }

    private static string BuildSystemPrompt()
    {
        return "You are a registered dietitian creating Turkish daily nutrition plans. Respond only with valid JSON matching the requested schema. Percentages must be integers and sum to 100.";
    }

    private static string BuildUserPrompt(DietPlanRequestContext context)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Kullanıcı verileri:");
        Append(builder, "Ad", context.UserFullName);
        Append(builder, "E-posta", context.Email);
        Append(builder, "Yaş", context.AgeYears?.ToString(CultureInfo.InvariantCulture));
        Append(builder, "Cinsiyet", null); // intentionally left blank until data exists
        Append(builder, "Boy (cm)", context.HeightCm?.ToString("0.0", CultureInfo.InvariantCulture));
        Append(builder, "Kilo (kg)", context.WeightKg?.ToString("0.0", CultureInfo.InvariantCulture));
        Append(builder, "Hedef Kilo", context.TargetWeightKg?.ToString("0.0", CultureInfo.InvariantCulture));
        Append(builder, "BMI", context.BodyMassIndex?.ToString("0.0", CultureInfo.InvariantCulture));
        Append(builder, "BMI Kategorisi", context.BmiCategory);
        Append(builder, "Diyet Hedefi", context.DietGoal.ToString());
        Append(builder, "Aktivite Düzeyi", context.ActivityLevel.ToString());
        Append(builder, "Allerjiler", context.Allergies);
        Append(builder, "Sağlık Durumları", context.HealthConditions);
        Append(builder, "Notlar", context.SpecialNotes);

        builder.AppendLine();
        builder.AppendLine("Çıktı Talebi:");
        builder.AppendLine("Lütfen sadece aşağıdaki JSON şemasına uygun, tek satırda JSON döndür:");
        builder.AppendLine("{");
        builder.AppendLine("  \"suggestedCalories\": number,");
        builder.AppendLine("  \"hydrationLiters\": number,");
        builder.AppendLine("  \"macroSplit\": { \"carbsPercent\": int, \"proteinPercent\": int, \"fatPercent\": int },");
        builder.AppendLine("  \"focusTips\": [string, string, string],");
        builder.AppendLine("  \"meals\": [");
        builder.AppendLine("    { \"meal\": string, \"description\": string, \"accent\": string }");
        builder.AppendLine("  ],");
        builder.AppendLine("  \"cautions\": [string],");
        builder.AppendLine("  \"motivationMessage\": string,");
        builder.AppendLine("  \"planTitle\": string");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static void Append(StringBuilder builder, string label, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        builder.AppendLine($"- {label}: {value}");
    }
}
