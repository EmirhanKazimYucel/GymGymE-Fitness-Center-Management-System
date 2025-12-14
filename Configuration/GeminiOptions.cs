namespace WebProje.Configuration;

public class GeminiOptions
{
    public const string SectionName = "Gemini";

    public string Provider { get; set; } = "Gemini";

    public string ApiKey { get; set; } = string.Empty;

    public string Model { get; set; } = "gemini-2.0-flash-exp";

    public string BaseUrl { get; set; } = string.Empty;

    public int TimeoutSeconds { get; set; } = 15;

    public string? OpenAiOrganization { get; set; }

    public string? OpenAiProject { get; set; }
}
