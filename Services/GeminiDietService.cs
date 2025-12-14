using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WebProje.Configuration;
using WebProje.Models;

namespace WebProje.Services;

public interface IGeminiDietService
{
    Task<GeminiDietPlanResult> GeneratePlanAsync(DietPlanRequestContext context, CancellationToken cancellationToken = default);
}

public sealed class GeminiDietService : IGeminiDietService
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        NumberHandling = JsonNumberHandling.AllowReadingFromString
    };

    private const string SystemInstructionText = "Sen sertifikalı bir diyetisyensin. Türkçe yanıt ver ve klinik teşhis koyma. Sağlık risklerinde doktora yönlendir. Yanıtında sadece JSON kullan, açıklama ekleme.";
    private static readonly SemaphoreSlim RateLimitGate = new(1, 1);
    private static DateTimeOffset _lastRequestUtc = DateTimeOffset.MinValue;
    private static readonly TimeSpan MinDelayBetweenRequests = TimeSpan.FromSeconds(4.1); // ~15 RPM

    private const string ResponseSchema = """
{
  "planTitle": "Kısa ve motive edici başlık",
  "motivationMessage": "1-2 cümle motive edici mesaj",
  "calories": 0,
  "hydrationLiters": 0,
  "macroSplit": {
    "carbs": 0,
    "protein": 0,
    "fat": 0
  },
  "focusTips": ["En fazla 5 kısa madde"],
  "meals": [
    {
      "meal": "Öğün adı",
      "description": "Türkçe açıklama",
      "accent": "#FFAA99"
    }
  ],
  "cautions": ["Varsa sağlık uyarıları"]
}
""";

    private readonly HttpClient _httpClient;
    private readonly GeminiOptions _options;
    private readonly ILogger<GeminiDietService> _logger;

    private bool UseOpenAiProvider => string.Equals(_options.Provider, "OpenAI", StringComparison.OrdinalIgnoreCase);

    public GeminiDietService(HttpClient httpClient, IOptions<GeminiOptions> options, ILogger<GeminiDietService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _options = options?.Value ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (_options.TimeoutSeconds > 0)
        {
            _httpClient.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
        }

        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<GeminiDietPlanResult> GeneratePlanAsync(DietPlanRequestContext context, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            return GeminiDietPlanResult.Disabled("Yapay zeka planı henüz aktif değil, klasik plan görüntüleniyor.");
        }

        var prompt = BuildPrompt(context);
        return UseOpenAiProvider
            ? await SendOpenAiRequestAsync(prompt, cancellationToken)
            : await SendGeminiRequestAsync(prompt, cancellationToken);
    }

    private static string BuildPrompt(DietPlanRequestContext context)
    {
        var userPayload = new
        {
            context.UserFullName,
            context.Email,
            context.AgeYears,
            context.HeightCm,
            context.WeightKg,
            context.TargetWeightKg,
            Goal = context.DietGoal.ToString(),
            Activity = context.ActivityLevel.ToString(),
            context.BodyMassIndex,
            context.BmiCategory,
            context.HealthConditions,
            context.Allergies,
            Notes = context.SpecialNotes
        };

        var sb = new StringBuilder();
        sb.AppendLine("Kullanıcı profili JSON:");
        sb.AppendLine(JsonSerializer.Serialize(userPayload, SerializerOptions));
        sb.AppendLine();
        sb.AppendLine("Görev: Yukarıdaki kullanıcı için Türkçe bir günlük beslenme planı öner. Cevabı yalnızca aşağıdaki JSON şemasına uygun olarak üret. Değer bulunamazsa mantıklı bir varsayım yap.");
        sb.AppendLine(ResponseSchema);
        return sb.ToString();
    }

    private Task<GeminiDietPlanResult> SendGeminiRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = BuildGeminiRequestBody(prompt);
        var requestJson = JsonSerializer.Serialize(requestBody, SerializerOptions);
        var endpoint = BuildGeminiEndpoint();

        _logger.LogDebug("Gemini isteği gövdesi: {Body}", Truncate(requestJson, 400));

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        return SendAndProcessResponseAsync(requestMessage, ExtractGeminiPayload, cancellationToken, "Gemini");
    }

    private Task<GeminiDietPlanResult> SendOpenAiRequestAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = BuildOpenAiRequestBody(prompt, _options.Model);
        var requestJson = JsonSerializer.Serialize(requestBody, SerializerOptions);
        var endpoint = BuildOpenAiEndpoint();

        var requestMessage = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
        if (!string.IsNullOrWhiteSpace(_options.OpenAiProject))
        {
            requestMessage.Headers.TryAddWithoutValidation("OpenAI-Project", _options.OpenAiProject);
        }
        if (!string.IsNullOrWhiteSpace(_options.OpenAiOrganization))
        {
            requestMessage.Headers.TryAddWithoutValidation("OpenAI-Organization", _options.OpenAiOrganization);
        }

        return SendAndProcessResponseAsync(requestMessage, ExtractOpenAiPayload, cancellationToken, "OpenAI");
    }

    private async Task<GeminiDietPlanResult> SendAndProcessResponseAsync(HttpRequestMessage requestMessage, Func<string, string?> payloadExtractor, CancellationToken cancellationToken, string providerName)
    {
        try
        {
            await EnforceRateLimitAsync(cancellationToken);
            using var response = await _httpClient.SendAsync(requestMessage, cancellationToken);
            var responseText = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI isteği {StatusCode} ile sonuçlandı: {Body}", (int)response.StatusCode, Truncate(responseText, 500));
                var friendlyError = BuildFriendlyError(response.StatusCode, responseText);
                return GeminiDietPlanResult.Failure(friendlyError ?? "Yapay zeka isteği başarısız oldu. Lütfen daha sonra tekrar deneyin.");
            }

            var payloadText = payloadExtractor(responseText);
            if (string.IsNullOrWhiteSpace(payloadText))
            {
                return GeminiDietPlanResult.Failure("Yapay zeka cevabı alınamadı. Klasik plana döndük.");
            }

            var normalized = TryExtractJsonBlock(payloadText);

            GeminiDietPlanPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<GeminiDietPlanPayload>(normalized, SerializerOptions);
            }
            catch (JsonException jsonEx)
            {
                _logger.LogWarning(jsonEx, "Yapay zeka cevabı beklenen JSON formatında değil.");
                return GeminiDietPlanResult.Failure("Yapay zeka yanıtı çözümlenemedi, klasik plana devam ediliyor.");
            }

            if (payload is null)
            {
                return GeminiDietPlanResult.Failure("Yapay zeka yanıtı boş döndü.");
            }

            var suggestion = ToSuggestion(payload);
            return GeminiDietPlanResult.Successful(suggestion, _options.Model, providerName);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Yapay zeka isteği sırasında beklenmedik bir hata oluştu.");
            return GeminiDietPlanResult.Failure("Yapay zeka isteği tamamlanamadı, klasik plana devam ediliyor.");
        }
        finally
        {
            requestMessage.Dispose();
        }
    }

    private static async Task EnforceRateLimitAsync(CancellationToken cancellationToken)
    {
        await RateLimitGate.WaitAsync(cancellationToken);
        try
        {
            var now = DateTimeOffset.UtcNow;
            var elapsed = now - _lastRequestUtc;
            if (elapsed < MinDelayBetweenRequests)
            {
                var delay = MinDelayBetweenRequests - elapsed;
                if (delay > TimeSpan.Zero)
                {
                    await Task.Delay(delay, cancellationToken);
                }
            }
            _lastRequestUtc = DateTimeOffset.UtcNow;
        }
        finally
        {
            RateLimitGate.Release();
        }
    }

    private static object BuildGeminiRequestBody(string prompt)
    {
        var mergedPrompt = $"{SystemInstructionText}\n\n{prompt}";
        return new
        {
            contents = new object[]
            {
                new
                {
                    role = "user",
                    parts = new object[]
                    {
                        new { text = mergedPrompt }
                    }
                }
            },
            generationConfig = new
            {
                temperature = 0.35,
                maxOutputTokens = 896
            }
        };
    }

    private static object BuildOpenAiRequestBody(string prompt, string? model)
    {
        return new
        {
            model = string.IsNullOrWhiteSpace(model) ? "gpt-4o-mini" : model,
            temperature = 0.35,
            max_tokens = 896,
            response_format = new { type = "json_object" },
            messages = new object[]
            {
                new { role = "system", content = SystemInstructionText },
                new { role = "user", content = prompt }
            }
        };
    }

    private static string? ExtractGeminiPayload(string responseText)
    {
        var geminiResponse = JsonSerializer.Deserialize<GeminiGenerateContentResponse>(responseText, SerializerOptions);
        return geminiResponse?.Candidates?
            .SelectMany(c => c.Content?.Parts ?? new List<GeminiResponsePart>())
            .Select(p => p.Text)
            .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t));
    }

    private static string? ExtractOpenAiPayload(string responseText)
    {
        var openAiResponse = JsonSerializer.Deserialize<OpenAiChatCompletionResponse>(responseText, SerializerOptions);
        return openAiResponse?.Choices?
            .Select(choice => choice.Message?.Content)
            .FirstOrDefault(text => !string.IsNullOrWhiteSpace(text));
    }

    // --- KRİTİK DÜZELTME BURADA YAPILDI ---
    private string BuildGeminiEndpoint()
    {
        // 1. URL'i appsettings'e bakmaksızın "v1beta" olarak sabitliyoruz.
        // Bu, "Not Found" hatasının en büyük sebebidir.
        var baseUrl = "https://generativelanguage.googleapis.com/v1beta";

        // 2. Model adını alıyoruz
        var model = _options.Model?.Trim();
        if (string.IsNullOrWhiteSpace(model))
        {
             model = "gemini-1.5-flash-latest";
        }

        // 3. EĞER model "gemini-1.5-flash" ise (takma ad), bunu TAM SÜRÜM adıyla değiştiriyoruz.
        // API bazen kısa ismi tanıyamıyor, bu yüzden "-latest" ekleyerek sorunu çözüyoruz.
        if (model.Equals("gemini-1.5-flash", StringComparison.OrdinalIgnoreCase))
        {
            model = "gemini-1.5-flash-latest";
        }

        return $"{baseUrl}/models/{model}:generateContent?key={_options.ApiKey}";
    }
    // ----------------------------------------

    private string BuildOpenAiEndpoint()
    {
        var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
            ? "https://api.openai.com/v1"
            : _options.BaseUrl.TrimEnd('/');
        return $"{baseUrl}/chat/completions";
    }

    private static string TryExtractJsonBlock(string payloadText)
    {
        var trimmed = payloadText.Trim();
        if (trimmed.StartsWith("```"))
        {
            var start = trimmed.IndexOf('{');
            var end = trimmed.LastIndexOf('}');
            if (start >= 0 && end >= start)
            {
                return trimmed[start..(end + 1)];
            }
        }
        return trimmed;
    }

    private static string Truncate(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value ?? string.Empty;
        }

        return value[..maxLength] + "…";
    }

    private static string? BuildFriendlyError(HttpStatusCode statusCode, string responseText)
    {
        var parsedError = TryParseError(responseText);
        var normalizedStatus = parsedError?.Status ?? parsedError?.Type ?? statusCode.ToString();
        var code = parsedError?.Code ?? (int)statusCode;
        var codeText = parsedError?.CodeText ?? parsedError?.Type;

        if (code == 429 || string.Equals(normalizedStatus, "RESOURCE_EXHAUSTED", StringComparison.OrdinalIgnoreCase) || string.Equals(codeText, "rate_limit_exceeded", StringComparison.OrdinalIgnoreCase))
        {
            return "Yapay zeka servisinin kota limitleri dolmuş görünüyor. Sağlayıcınızın faturalandırma/kota ayarlarını kontrol edip tekrar deneyin.";
        }

        if (code == 403 || string.Equals(normalizedStatus, "PERMISSION_DENIED", StringComparison.OrdinalIgnoreCase))
        {
            return "Bu yapay zeka modeli için yetki verilmedi. Sağlayıcı panelinden erişim izni açıp tekrar deneyin.";
        }

        if (code == 401 || string.Equals(normalizedStatus, "UNAUTHENTICATED", StringComparison.OrdinalIgnoreCase))
        {
            return "API anahtarı doğrulanamadı. Anahtarı güncelleyip uygulamayı yeniden başlatmayı deneyin.";
        }

        if (!string.IsNullOrWhiteSpace(parsedError?.Message))
        {
            return $"Yapay zeka API hatası: {parsedError.Message.Trim()}";
        }

        return null;
    }

    private static AiErrorDetails? TryParseError(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            if (!document.RootElement.TryGetProperty("error", out var errorElement))
            {
                return null;
            }

            int? numericCode = null;
            string? codeText = null;
            if (errorElement.TryGetProperty("code", out var codeElement))
            {
                if (codeElement.ValueKind == JsonValueKind.Number && codeElement.TryGetInt32(out var parsedCode))
                {
                    numericCode = parsedCode;
                }
                else if (codeElement.ValueKind == JsonValueKind.String)
                {
                    codeText = codeElement.GetString();
                    if (int.TryParse(codeText, out var parsedFromText))
                    {
                        numericCode = parsedFromText;
                    }
                }
            }

            var status = errorElement.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString()
                : null;
            var type = errorElement.TryGetProperty("type", out var typeElement) && typeElement.ValueKind == JsonValueKind.String
                ? typeElement.GetString()
                : null;
            var message = errorElement.TryGetProperty("message", out var messageElement) && messageElement.ValueKind == JsonValueKind.String
                ? messageElement.GetString()
                : null;

            return new AiErrorDetails
            {
                Code = numericCode,
                CodeText = codeText,
                Status = status,
                Type = type,
                Message = message
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static AiDietPlanSuggestion ToSuggestion(GeminiDietPlanPayload payload)
    {
        var macro = payload.MacroSplit;
        var macroDistribution = macro is null
            ? null
            : new MacroDistribution
            {
                CarbsPercent = macro.Carbs ?? 0,
                ProteinPercent = macro.Protein ?? 0,
                FatPercent = macro.Fat ?? 0
            };

        var focusTips = payload.FocusTips?.Where(t => !string.IsNullOrWhiteSpace(t)).Select(t => t.Trim()).ToArray()
                         ?? Array.Empty<string>();

        var mealIdeas = payload.Meals?.Select(m => new DietMealIdea
        {
            Meal = m.Meal?.Trim() ?? string.Empty,
            Description = m.Description?.Trim() ?? string.Empty,
            Accent = string.IsNullOrWhiteSpace(m.Accent) ? "#ff80ab" : m.Accent.Trim()
        }).Where(m => !string.IsNullOrWhiteSpace(m.Meal) && !string.IsNullOrWhiteSpace(m.Description)).ToArray()
                      ?? Array.Empty<DietMealIdea>();

        var cautions = payload.Cautions?.Where(c => !string.IsNullOrWhiteSpace(c)).Select(c => c.Trim()).ToArray()
                       ?? Array.Empty<string>();

        return new AiDietPlanSuggestion
        {
            SuggestedCalories = payload.Calories,
            HydrationLiters = payload.HydrationLiters,
            MacroSplit = macroDistribution,
            FocusTips = focusTips,
            Meals = mealIdeas,
            Cautions = cautions,
            MotivationMessage = payload.MotivationMessage,
            PlanTitle = payload.PlanTitle
        };
    }

    private sealed class GeminiGenerateContentResponse
    {
        [JsonPropertyName("candidates")]
        public List<GeminiCandidate>? Candidates { get; set; }
    }

    private sealed class OpenAiChatCompletionResponse
    {
        [JsonPropertyName("choices")]
        public List<OpenAiChatChoice>? Choices { get; set; }
    }

    private sealed class OpenAiChatChoice
    {
        [JsonPropertyName("message")]
        public OpenAiChatMessage? Message { get; set; }
    }

    private sealed class OpenAiChatMessage
    {
        [JsonPropertyName("content")]
        public string? Content { get; set; }
    }

    private sealed class AiErrorDetails
    {
        public int? Code { get; init; }
        public string? CodeText { get; init; }
        public string? Status { get; init; }
        public string? Type { get; init; }
        public string? Message { get; init; }
    }

    private sealed class GeminiCandidate
    {
        [JsonPropertyName("content")]
        public GeminiResponseContent? Content { get; set; }
    }

    private sealed class GeminiResponseContent
    {
        [JsonPropertyName("parts")]
        public List<GeminiResponsePart>? Parts { get; set; }
    }

    private sealed class GeminiResponsePart
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }
    }

    private sealed class GeminiDietPlanPayload
    {
        [JsonPropertyName("planTitle")]
        public string? PlanTitle { get; set; }

        [JsonPropertyName("motivationMessage")]
        public string? MotivationMessage { get; set; }

        [JsonPropertyName("calories")]
        public int? Calories { get; set; }

        [JsonPropertyName("hydrationLiters")]
        public double? HydrationLiters { get; set; }

        [JsonPropertyName("macroSplit")]
        public GeminiMacroBlock? MacroSplit { get; set; }

        [JsonPropertyName("focusTips")]
        public List<string>? FocusTips { get; set; }

        [JsonPropertyName("meals")]
        public List<GeminiMealIdea>? Meals { get; set; }

        [JsonPropertyName("cautions")]
        public List<string>? Cautions { get; set; }
    }

    private sealed class GeminiMacroBlock
    {
        [JsonPropertyName("carbs")]
        public int? Carbs { get; set; }

        [JsonPropertyName("protein")]
        public int? Protein { get; set; }

        [JsonPropertyName("fat")]
        public int? Fat { get; set; }
    }

    private sealed class GeminiMealIdea
    {
        [JsonPropertyName("meal")]
        public string? Meal { get; set; }

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("accent")]
        public string? Accent { get; set; }
    }
}

public sealed class GeminiDietPlanResult
{
    private GeminiDietPlanResult(bool success, AiDietPlanSuggestion? suggestion, string? modelUsed, string? errorMessage, string? providerName)
    {
        Success = success;
        Suggestion = suggestion;
        ModelUsed = modelUsed;
        ErrorMessage = errorMessage;
        ProviderName = providerName;
    }

    public bool Success { get; }
    public AiDietPlanSuggestion? Suggestion { get; }
    public string? ModelUsed { get; }
    public string? ErrorMessage { get; }
    public string? ProviderName { get; }

    public static GeminiDietPlanResult Successful(AiDietPlanSuggestion suggestion, string? modelUsed, string? providerName) =>
        new(true, suggestion, modelUsed, null, providerName);

    public static GeminiDietPlanResult Failure(string? message) =>
        new(false, null, null, message, null);

    public static GeminiDietPlanResult Disabled(string? message) => Failure(message);
}