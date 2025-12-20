using System;
using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class GymInfo
{
    public int Id { get; set; }

    [Required, MaxLength(128)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(256)]
    public string? Address { get; set; }

    [MaxLength(64)]
    public string? Phone { get; set; }

    [MaxLength(128)]
    public string? Email { get; set; }

    [MaxLength(256)]
    public string? Website { get; set; }

    [MaxLength(128)]
    public string? WeekdayHours { get; set; }

    [MaxLength(128)]
    public string? WeekendHours { get; set; }

    [MaxLength(512)]
    public string? About { get; set; }

    [MaxLength(512)]
    public string? Facilities { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
