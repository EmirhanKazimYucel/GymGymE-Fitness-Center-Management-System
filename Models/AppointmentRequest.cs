using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class AppointmentRequest
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string FullName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    public DateOnly Date { get; set; }

    [Required, MaxLength(16)]
    public string TimeSlot { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string Coach { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Notes { get; set; }

    [Required, MaxLength(32)]
    public string Status { get; set; } = AppointmentStatus.Pending;

    public DateTime RequestedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? DecisionAtUtc { get; set; }

    [MaxLength(128)]
    public string? DecisionBy { get; set; }
}

public static class AppointmentStatus
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
}
