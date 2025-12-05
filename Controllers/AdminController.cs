using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

namespace WebProje.Controllers;

public class AdminController : Controller
{
    private readonly FitnessContext _context;

    public AdminController(FitnessContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Panel()
    {
        ViewData["Title"] = "Admin Paneli";
        var model = await BuildPanelViewModel();
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateAppointmentStatus(int id, string actionType)
    {
        var appointment = await _context.AppointmentRequests.FindAsync(id);
        if (appointment is null)
        {
            return NotFound();
        }

        if (appointment.Status != AppointmentStatus.Pending)
        {
            TempData["AdminMessage"] = "Bu randevu zaten karar verilmiş.";
            return RedirectToAction(nameof(Panel));
        }

        var isApproveAction = actionType?.Equals("approve", StringComparison.OrdinalIgnoreCase) == true;

        if (isApproveAction)
        {
            var hasConflict = await _context.AppointmentRequests
                .AnyAsync(a => a.Id != appointment.Id
                    && a.Status == AppointmentStatus.Approved
                    && a.Coach == appointment.Coach
                    && a.Date == appointment.Date
                    && a.TimeSlot == appointment.TimeSlot);

            if (hasConflict)
            {
                TempData["AdminMessage"] = "Bu antrenör belirtilen tarih ve saatte zaten dolu.";
                return RedirectToAction(nameof(Panel));
            }
        }

        var status = isApproveAction ? AppointmentStatus.Approved : AppointmentStatus.Rejected;

        appointment.Status = status;
        appointment.DecisionAtUtc = DateTime.UtcNow;
        appointment.DecisionBy = "Admin";

        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = status == AppointmentStatus.Approved
            ? "Randevu onaylandı."
            : "Randevu reddedildi.";

        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCoach([Bind(Prefix = "NewCoach")] NewCoachInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPanelViewModel(input, null);
            return View("Panel", invalidModel);
        }

        var coach = new Coach
        {
            FullName = input.FullName.Trim(),
            ExpertiseTags = NormalizeList(input.ExpertiseTags),
            ServicesOffered = NormalizeList(input.ServicesOffered),
            Bio = string.IsNullOrWhiteSpace(input.Bio) ? null : input.Bio.Trim()
        };

        _context.Coaches.Add(coach);
        await _context.SaveChangesAsync();

        if (input.ServiceIds.Any())
        {
            var validServiceIds = await _context.Services
                .Where(s => input.ServiceIds.Contains(s.Id))
                .Select(s => s.Id)
                .ToListAsync();

            foreach (var serviceId in validServiceIds)
            {
                _context.CoachServices.Add(new CoachService
                {
                    CoachId = coach.Id,
                    ServiceId = serviceId
                });
            }

            if (validServiceIds.Count > 0)
            {
                await _context.SaveChangesAsync();
            }
        }

        TempData["AdminMessage"] = "Yeni antrenör kaydı eklendi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddService([Bind(Prefix = "NewService")] NewServiceInputModel input)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPanelViewModel(null, input);
            return View("Panel", invalidModel);
        }

        var service = new Service
        {
            Name = input.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim(),
            DurationMinutes = input.DurationMinutes,
            Price = input.Price
        };

        _context.Services.Add(service);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Yeni hizmet kaydedildi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> EditCoach(EditCoachInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminMessage"] = "Antrenör bilgileri doğrulanamadı.";
            return RedirectToAction(nameof(Panel));
        }

        var coach = await _context.Coaches
            .Include(c => c.CoachServices)
            .FirstOrDefaultAsync(c => c.Id == input.Id);

        if (coach is null)
        {
            return NotFound();
        }

        coach.FullName = input.FullName.Trim();
        coach.ExpertiseTags = NormalizeList(input.ExpertiseTags);
        coach.Bio = string.IsNullOrWhiteSpace(input.Bio) ? null : input.Bio.Trim();

        var requestedIds = input.ServiceIds?.Distinct().ToArray() ?? Array.Empty<int>();

        var validServiceIds = await _context.Services
            .Where(s => requestedIds.Contains(s.Id))
            .Select(s => s.Id)
            .ToListAsync();

        if (coach.CoachServices.Any())
        {
            _context.CoachServices.RemoveRange(coach.CoachServices);
        }

        foreach (var serviceId in validServiceIds)
        {
            _context.CoachServices.Add(new CoachService
            {
                CoachId = coach.Id,
                ServiceId = serviceId
            });
        }

        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Antrenör bilgileri güncellendi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteCoach(int id)
    {
        var coach = await _context.Coaches
            .Include(c => c.CoachServices)
            .FirstOrDefaultAsync(c => c.Id == id);

        if (coach is null)
        {
            TempData["AdminMessage"] = "Antrenör bulunamadı.";
            return RedirectToAction(nameof(Panel));
        }

        if (coach.CoachServices.Any())
        {
            _context.CoachServices.RemoveRange(coach.CoachServices);
        }

        _context.Coaches.Remove(coach);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Antrenör kaydı silindi.";
        return RedirectToAction(nameof(Panel));
    }

    private async Task<AdminPanelViewModel> BuildPanelViewModel(NewCoachInputModel? coachForm = null, NewServiceInputModel? serviceForm = null)
    {
        var pending = await _context.AppointmentRequests
            .Where(a => a.Status == AppointmentStatus.Pending)
            .OrderBy(a => a.Date).ThenBy(a => a.TimeSlot)
            .Take(10)
            .ToListAsync();

        var decisions = await _context.AppointmentRequests
            .Where(a => a.Status != AppointmentStatus.Pending)
            .OrderByDescending(a => a.DecisionAtUtc)
            .Take(10)
            .ToListAsync();

        var services = await _context.Services
            .OrderBy(s => s.Name)
            .ToListAsync();

        var coaches = await _context.Coaches
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(12)
            .ToListAsync();

        var coachIds = coaches.Select(c => c.Id).ToArray();

        var coachServiceMap = await _context.CoachServices
            .Where(cs => coachIds.Contains(cs.CoachId))
            .Select(cs => new
            {
                cs.CoachId,
                cs.ServiceId,
                cs.Service.Name
            })
            .ToListAsync();

        var coachItems = coaches
            .Select(c => new CoachListItem
            {
                Id = c.Id,
                FullName = c.FullName,
                Bio = c.Bio,
                ExpertiseTags = c.ExpertiseTags,
                Services = coachServiceMap
                    .Where(cs => cs.CoachId == c.Id)
                    .Select(cs => cs.Name)
                    .OrderBy(name => name)
                    .ToList(),
                ServiceIds = coachServiceMap
                    .Where(cs => cs.CoachId == c.Id)
                    .Select(cs => cs.ServiceId)
                    .Distinct()
                    .ToList()
            })
            .ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var approvedAppointments = await _context.AppointmentRequests
            .Where(a => a.Status == AppointmentStatus.Approved && a.Date >= today)
            .OrderBy(a => a.Coach)
            .ThenBy(a => a.Date)
            .ThenBy(a => a.TimeSlot)
            .Take(100)
            .ToListAsync();

        var coachNames = coachItems.Select(c => c.FullName)
            .Concat(approvedAppointments.Select(a => a.Coach))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(name => name)
            .ToList();

        var scheduleLookup = approvedAppointments
            .GroupBy(a => a.Coach, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g
                .Select(a => new CoachScheduleSlot
                {
                    Date = a.Date,
                    TimeSlot = a.TimeSlot,
                    Member = a.FullName,
                    Service = a.ServiceName
                })
                .OrderBy(slot => slot.Date)
                .ThenBy(slot => slot.TimeSlot, StringComparer.Ordinal)
                .ToList(), StringComparer.OrdinalIgnoreCase);

        var schedule = coachNames
            .Select(name => new CoachScheduleItem
            {
                Coach = name,
                Slots = scheduleLookup.TryGetValue(name, out var slots) ? slots : new List<CoachScheduleSlot>()
            })
            .ToList();

        return new AdminPanelViewModel
        {
            PendingAppointments = pending,
            RecentDecisions = decisions,
            Coaches = coachItems,
            Services = services,
            NewCoach = coachForm ?? new NewCoachInputModel(),
            NewService = serviceForm ?? new NewServiceInputModel(),
            CoachSchedules = schedule
        };
    }

    private static string? NormalizeList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var segments = value
            .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToArray();

        return segments.Length == 0 ? null : string.Join(", ", segments);
    }
}
