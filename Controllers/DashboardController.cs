using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

namespace WebProje.Controllers;

public class DashboardController : Controller
{
    private readonly FitnessContext _context;

    public DashboardController(FitnessContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        var appointments = await _context.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.Email == user.Email)
            .OrderBy(a => a.Date)
            .ThenBy(a => a.TimeSlot)
            .ToListAsync();

        var slots = appointments.Select(a => new UserAppointmentItem
        {
            Date = a.Date,
            TimeSlot = a.TimeSlot,
            Coach = a.Coach,
            Service = a.ServiceName,
            Status = a.Status
        }).ToList();

        var today = DateOnly.FromDateTime(DateTime.Today);
        var upcoming = slots
            .Where(slot => slot.Date >= today)
            .OrderBy(slot => slot.Date)
            .ThenBy(slot => slot.TimeSlot, StringComparer.Ordinal)
            .Take(6)
            .ToList();

        var model = new DashboardViewModel
        {
            UserFullName = BuildUserDisplayName(user),
            UserEmail = user.Email,
            Appointments = slots,
            UpcomingAppointments = upcoming
        };

        ViewData["Title"] = "Kullanıcı Paneli";
        return View(model);
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (userId is null)
        {
            return null;
        }

        return await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId.Value);
    }

    private IActionResult RedirectToLogin()
    {
        var returnUrl = Url.Action(nameof(Index), "Dashboard");
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    private static string BuildUserDisplayName(AppUser user)
    {
        var nameParts = new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var joined = string.Join(" ", nameParts);
        return string.IsNullOrWhiteSpace(joined) ? user.Email : joined;
    }
}
