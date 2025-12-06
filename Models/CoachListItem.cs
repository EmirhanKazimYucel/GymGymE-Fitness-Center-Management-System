namespace WebProje.Models;

public class CoachListItem
{
    public int Id { get; init; }
    public string FullName { get; init; } = string.Empty;
    public string? Bio { get; init; }
    public string? ExpertiseTags { get; init; }
    public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
    public IReadOnlyList<int> ServiceIds { get; init; } = Array.Empty<int>();
}
