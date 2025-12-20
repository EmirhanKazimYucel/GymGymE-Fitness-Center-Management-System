namespace WebProje.Models;

public class AdminPanelViewModel
{
    public IReadOnlyList<AppointmentRequest> PendingAppointments { get; init; } = Array.Empty<AppointmentRequest>();
    public IReadOnlyList<AppointmentRequest> RecentDecisions { get; init; } = Array.Empty<AppointmentRequest>();
    public IReadOnlyList<CoachListItem> Coaches { get; init; } = Array.Empty<CoachListItem>();
    public IReadOnlyList<Service> Services { get; init; } = Array.Empty<Service>();
    public NewCoachInputModel NewCoach { get; init; } = new();
    public NewServiceInputModel NewService { get; init; } = new();
    public IReadOnlyList<CoachScheduleItem> CoachSchedules { get; init; } = Array.Empty<CoachScheduleItem>();
    public GymInfoViewModel GymInfo { get; init; } = new();
    public IReadOnlyList<GymOpeningHourInputModel> GymHours { get; init; } = Array.Empty<GymOpeningHourInputModel>();
    public IReadOnlyList<BookingUsageMetric> BookingUsageMetrics { get; init; } = Array.Empty<BookingUsageMetric>();
}
