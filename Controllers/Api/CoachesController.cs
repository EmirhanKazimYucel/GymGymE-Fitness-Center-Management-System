using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

namespace WebProje.Controllers.Api;

[ApiController]
[Route("api/[controller]")]
public class CoachesController : ControllerBase
{
    private readonly FitnessContext _context;

    public CoachesController(FitnessContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Returns every coach along with the services they can deliver.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IEnumerable<CoachSummaryDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IEnumerable<CoachSummaryDto>>> GetAll()
    {
        var coaches = await _context.Coaches
            .Include(c => c.CoachServices)
                .ThenInclude(cs => cs.Service)
            .AsNoTracking()
            .OrderBy(c => c.FullName)
            .Select(c => new CoachSummaryDto
            {
                Id = c.Id,
                FullName = c.FullName,
                ExpertiseTags = c.ExpertiseTags,
                Bio = c.Bio,
                Services = c.CoachServices
                    .Select(cs => cs.Service.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToListAsync();

        return Ok(coaches);
    }

    /// <summary>
    /// Returns coaches that are free for the supplied date/time slot.
    /// </summary>
    [HttpGet("available")]
    [ProducesResponseType(typeof(IEnumerable<CoachAvailabilityDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IEnumerable<CoachAvailabilityDto>>> GetAvailable([FromQuery] DateOnly? date, [FromQuery] string? timeSlot, [FromQuery] int? serviceId)
    {
        if (date is null || string.IsNullOrWhiteSpace(timeSlot) || serviceId is null)
        {
            return BadRequest("date, timeSlot ve serviceId parametreleri zorunludur.");
        }

        var service = await _context.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == serviceId.Value);
        if (service is null)
        {
            return BadRequest("Geçersiz serviceId değeri.");
        }

        var normalizedSlot = timeSlot.Trim();
        var requestedDuration = NormalizeDuration(service.DurationMinutes);
        if (!BookingTimeSlots.TryBuildInterval(normalizedSlot, requestedDuration, out var requestedStart, out var requestedEnd))
        {
            return BadRequest("timeSlot değeri desteklenmiyor.");
        }

        var durationLookup = await _context.Services
            .Select(s => new { s.Name, Duration = NormalizeDuration(s.DurationMinutes) })
            .ToDictionaryAsync(x => x.Name, x => x.Duration, StringComparer.OrdinalIgnoreCase);

        var busyEntries = await _context.AppointmentRequests
            .Where(a => a.Date == date && a.Status != AppointmentStatus.Rejected)
            .Select(a => new { a.Coach, a.TimeSlot, a.ServiceName })
            .ToListAsync();

        var busyCoachNames = new List<string>();
        foreach (var entry in busyEntries)
        {
            var minutes = ResolveDuration(entry.ServiceName, durationLookup);
            if (!BookingTimeSlots.TryBuildInterval(entry.TimeSlot, minutes, out var start, out var end))
            {
                continue;
            }

            if (BookingTimeSlots.Overlaps(start, end, requestedStart, requestedEnd))
            {
                busyCoachNames.Add(entry.Coach);
            }
        }

        var busySet = new HashSet<string>(busyCoachNames, StringComparer.OrdinalIgnoreCase);

        var candidates = await _context.Coaches
            .Include(c => c.CoachServices)
                .ThenInclude(cs => cs.Service)
            .AsNoTracking()
            .Where(c => c.CoachServices.Any(cs => cs.ServiceId == serviceId.Value))
            .OrderBy(c => c.FullName)
            .Select(c => new CoachAvailabilityDto
            {
                Id = c.Id,
                FullName = c.FullName,
                ExpertiseTags = c.ExpertiseTags,
                Services = c.CoachServices
                    .Select(cs => cs.Service.Name)
                    .OrderBy(name => name)
                    .ToList()
            })
            .ToListAsync();

        if (busySet.Count > 0)
        {
            candidates = candidates
                .Where(dto => !busySet.Contains(dto.FullName))
                .ToList();
        }

        foreach (var dto in candidates)
        {
            dto.RequestedDate = date.Value;
            dto.RequestedSlot = normalizedSlot;
            dto.RequestedDurationMinutes = requestedDuration;
        }

        return Ok(candidates);
    }

    public sealed class CoachSummaryDto
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string? ExpertiseTags { get; init; }
        public string? Bio { get; init; }
        public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
    }

    public sealed class CoachAvailabilityDto
    {
        public int Id { get; init; }
        public string FullName { get; init; } = string.Empty;
        public string? ExpertiseTags { get; init; }
        public IReadOnlyList<string> Services { get; init; } = Array.Empty<string>();
        public DateOnly RequestedDate { get; set; }
        public string RequestedSlot { get; set; } = string.Empty;
        public int RequestedDurationMinutes { get; set; }
    }

    private static int NormalizeDuration(int? minutes)
    {
        return minutes.HasValue && minutes.Value > 0 ? minutes.Value : 60;
    }

    private static int ResolveDuration(string serviceName, IReadOnlyDictionary<string, int> lookup)
    {
        return lookup.TryGetValue(serviceName, out var duration) ? duration : 60;
    }
}
