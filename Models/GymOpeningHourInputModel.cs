using System;
using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class GymOpeningHourInputModel
{
    public DayOfWeek DayOfWeek { get; set; }

    [Display(Name = "Kapalı")]
    public bool IsClosed { get; set; }

    [Display(Name = "Açılış"), DataType(DataType.Time)]
    public string? OpenTime { get; set; }

    [Display(Name = "Kapanış"), DataType(DataType.Time)]
    public string? CloseTime { get; set; }
}
