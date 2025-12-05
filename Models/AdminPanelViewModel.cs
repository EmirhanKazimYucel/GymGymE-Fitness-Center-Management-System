namespace WebProje.Models;

public class AdminPanelViewModel
{
    public IReadOnlyList<AppointmentRequest> PendingAppointments { get; init; } = Array.Empty<AppointmentRequest>();
    public IReadOnlyList<AppointmentRequest> RecentDecisions { get; init; } = Array.Empty<AppointmentRequest>();
    public IReadOnlyList<Coach> Coaches { get; init; } = Array.Empty<Coach>();
    public NewCoachInputModel NewCoach { get; init; } = new();
}
