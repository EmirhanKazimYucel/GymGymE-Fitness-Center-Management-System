using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;

namespace WebProje.Models;

public static class BookingTimeSlots
{
    private static readonly IReadOnlyList<string> Slots = Array.AsReadOnly(new[]
    {
        "09:00",
        "11:00",
        "14:30",
        "16:00",
        "18:00"
    });

    public static IReadOnlyList<string> All => Slots;

    public static bool TryParseStart(string? slotLabel, out TimeSpan start)
    {
        if (!string.IsNullOrWhiteSpace(slotLabel))
        {
            var trimmed = slotLabel.Trim();
            var dashIndex = trimmed.IndexOf('-');
            if (dashIndex > 0)
            {
                trimmed = trimmed[..dashIndex].Trim();
            }

            if (TimeSpan.TryParse(trimmed, CultureInfo.InvariantCulture, out start))
            {
                return true;
            }
        }

        start = default;
        return false;
    }

    public static bool TryBuildInterval(string? slotLabel, int durationMinutes, out TimeSpan start, out TimeSpan end)
    {
        if (durationMinutes <= 0)
        {
            durationMinutes = 1;
        }

        if (TryParseStart(slotLabel, out start))
        {
            end = start.Add(TimeSpan.FromMinutes(durationMinutes));
            return true;
        }

        start = default;
        end = default;
        return false;
    }

    public static bool Overlaps(TimeSpan startA, TimeSpan endA, TimeSpan startB, TimeSpan endB)
    {
        return startA < endB && startB < endA;
    }
}
