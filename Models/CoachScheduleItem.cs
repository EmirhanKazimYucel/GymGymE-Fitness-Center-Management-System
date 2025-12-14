namespace WebProje.Models;

public class CoachScheduleItem
{
    public string Coach { get; init; } = string.Empty;
    public IReadOnlyList<CoachScheduleSlot> Slots { get; init; } = Array.Empty<CoachScheduleSlot>();
}

public class CoachScheduleSlot
{
    public DateOnly Date { get; init; }
    public string TimeSlot { get; init; } = string.Empty;
    public string Member { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
}
