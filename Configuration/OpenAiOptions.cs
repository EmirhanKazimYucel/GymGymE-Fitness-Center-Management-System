namespace WebProje.Configuration;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string? ApiKey { get; set; }
    public string BaseUrl { get; set; } = "https://api.openai.com/v1/";
    public string Model { get; set; } = "gpt-4.1-mini";
    public double Temperature { get; set; } = 0.4;
    public int MaxOutputTokens { get; set; } = 900;
    public int TimeoutSeconds { get; set; } = 40;
}
