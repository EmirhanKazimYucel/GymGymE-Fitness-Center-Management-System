namespace WebProje.Models;

public class DashboardViewModel
{
    public string UserFullName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public IReadOnlyList<UserAppointmentItem> Appointments { get; init; } = Array.Empty<UserAppointmentItem>();
    public IReadOnlyList<UserAppointmentItem> UpcomingAppointments { get; init; } = Array.Empty<UserAppointmentItem>();
    public IReadOnlyList<WeeklyLeaderboardEntry> WeeklyLeaderboard { get; init; } = Array.Empty<WeeklyLeaderboardEntry>();
    public IReadOnlyList<string> ActivityTypes { get; init; } = Array.Empty<string>();
}

public class UserAppointmentItem
{
    public DateOnly Date { get; init; }
    public string TimeSlot { get; init; } = string.Empty;
    public string Coach { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string Status { get; init; } = AppointmentStatus.Pending;
}

public class WeeklyLeaderboardEntry
{
    public int Rank { get; init; }
    public string DisplayName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? AvatarUrl { get; init; }
    public string Initials { get; init; } = string.Empty;
    public int CompletedCount { get; init; }
}
