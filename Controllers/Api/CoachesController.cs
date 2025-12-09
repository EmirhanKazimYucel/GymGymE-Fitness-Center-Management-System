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
    public async Task<ActionResult<IEnumerable<CoachAvailabilityDto>>> GetAvailable([FromQuery] DateOnly? date, [FromQuery] string? timeSlot)
    {
        if (date is null || string.IsNullOrWhiteSpace(timeSlot))
        {
            return BadRequest("date ve timeSlot parametreleri zorunludur.");
        }

        var normalizedSlot = timeSlot.Trim();

        var busyCoachNames = await _context.AppointmentRequests
            .Where(a => a.Status == AppointmentStatus.Approved && a.Date == date && a.TimeSlot == normalizedSlot)
            .Select(a => a.Coach)
            .Distinct()
            .ToListAsync();

        var query = _context.Coaches
            .Include(c => c.CoachServices)
                .ThenInclude(cs => cs.Service)
            .AsNoTracking()
            .OrderBy(c => c.FullName)
            .AsQueryable();

        if (busyCoachNames.Count > 0)
        {
            query = query.Where(c => !busyCoachNames.Contains(c.FullName));
        }

        var result = await query
            .Select(c => new CoachAvailabilityDto
            {
                Id = c.Id,
                FullName = c.FullName,
                ExpertiseTags = c.ExpertiseTags,
                Services = c.CoachServices
                    .Select(cs => cs.Service.Name)
                    .OrderBy(name => name)
                    .ToList(),
                RequestedDate = date.Value,
                RequestedSlot = normalizedSlot
            })
            .ToListAsync();

        return Ok(result);
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
        public DateOnly RequestedDate { get; init; }
        public string RequestedSlot { get; init; } = string.Empty;
    }
}
