namespace WebProje.Models;

public class DashboardViewModel
{
    public string UserFullName { get; init; } = string.Empty;
    public string UserEmail { get; init; } = string.Empty;
    public IReadOnlyList<UserAppointmentItem> Appointments { get; init; } = Array.Empty<UserAppointmentItem>();
    public IReadOnlyList<UserAppointmentItem> UpcomingAppointments { get; init; } = Array.Empty<UserAppointmentItem>();
}

public class UserAppointmentItem
{
    public DateOnly Date { get; init; }
    public string TimeSlot { get; init; } = string.Empty;
    public string Coach { get; init; } = string.Empty;
    public string Service { get; init; } = string.Empty;
    public string Status { get; init; } = AppointmentStatus.Pending;
}
