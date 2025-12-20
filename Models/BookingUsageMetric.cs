using System;
using System.Collections.Generic;

namespace WebProje.Models;

public class BookingUsageMetric
{
    public string CategoryType { get; init; } = string.Empty; // "service" or "coach"
    public string Period { get; init; } = string.Empty; // weekly, monthly, yearly
    public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
    public IReadOnlyList<BookingUsageMetricSeries> Series { get; init; } = Array.Empty<BookingUsageMetricSeries>();
}

public class BookingUsageMetricSeries
{
    public string Label { get; init; } = string.Empty;
    public IReadOnlyList<int> Points { get; init; } = Array.Empty<int>();
    public int Total { get; init; }
}
