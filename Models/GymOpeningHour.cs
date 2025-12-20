using System;
using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class GymOpeningHour
{
    public int Id { get; set; }

    [Required]
    public DayOfWeek DayOfWeek { get; set; }

    public TimeSpan? OpenTime { get; set; }

    public TimeSpan? CloseTime { get; set; }

    public bool IsClosed { get; set; }

    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
