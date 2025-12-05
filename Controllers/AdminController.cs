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

        var status = actionType?.Equals("approve", StringComparison.OrdinalIgnoreCase) == true
            ? AppointmentStatus.Approved
            : AppointmentStatus.Rejected;

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
            var invalidModel = await BuildPanelViewModel(input);
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

        TempData["AdminMessage"] = "Yeni antrenör kaydı eklendi.";
        return RedirectToAction(nameof(Panel));
    }

    private async Task<AdminPanelViewModel> BuildPanelViewModel(NewCoachInputModel? formModel = null)
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

        var coaches = await _context.Coaches
            .OrderByDescending(c => c.CreatedAtUtc)
            .Take(12)
            .ToListAsync();

        return new AdminPanelViewModel
        {
            PendingAppointments = pending,
            RecentDecisions = decisions,
            Coaches = coaches,
            NewCoach = formModel ?? new NewCoachInputModel()
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
