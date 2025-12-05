using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

namespace WebProje.Controllers;

public class AppointmentController : Controller
{
    private readonly FitnessContext _context;

    private static readonly IReadOnlyList<string> TimeSlots = new[] { "09:00", "11:00", "14:30", "16:00", "18:00" };

    public AppointmentController(FitnessContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Book()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        var model = await BuildViewModelAsync(user);
        ViewData["Title"] = "Randevu Oluştur";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Book(AppointmentViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        var coachNames = await GetCoachNamesAsync();
        model.AvailableCoaches = coachNames;
        model.TimeSlots = TimeSlots;
        model.UserFullName = BuildUserDisplayName(user);
        model.UserEmail = user.Email;
        model.UserPhone = user.PhoneNumber;

        if (!coachNames.Any())
        {
            ModelState.AddModelError(string.Empty, "Şu anda sistemde aktif antrenör bulunmuyor.");
        }
        else if (!coachNames.Contains(model.SelectedCoach))
        {
            ModelState.AddModelError(nameof(model.SelectedCoach), "Geçerli bir antrenör seçiniz.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var request = new AppointmentRequest
        {
            FullName = BuildUserDisplayName(user),
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Date = model.SelectedDate,
            TimeSlot = model.SelectedTime,
            Coach = model.SelectedCoach,
            Notes = model.Notes,
            Status = AppointmentStatus.Pending
        };

        _context.AppointmentRequests.Add(request);
        await _context.SaveChangesAsync();

        TempData["AppointmentMessage"] = $"{model.SelectedDate:dd MMMM} {model.SelectedTime} slotu için talebiniz alındı. Admin onayı sonrası kesinleşecek.";
        return RedirectToAction("Index", "Dashboard");
    }

    private async Task<AppointmentViewModel> BuildViewModelAsync(AppUser user)
    {
        var coachNames = await GetCoachNamesAsync();
        var selectedCoach = coachNames.FirstOrDefault() ?? string.Empty;
        return new AppointmentViewModel
        {
            AvailableCoaches = coachNames,
            TimeSlots = TimeSlots,
            SelectedDate = DateOnly.FromDateTime(DateTime.Today),
            SelectedCoach = selectedCoach,
            UserFullName = BuildUserDisplayName(user),
            UserEmail = user.Email,
            UserPhone = user.PhoneNumber
        };
    }

    private async Task<AppUser?> GetCurrentUserAsync()
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (userId is null)
        {
            return null;
        }

        return await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
    }

    private IActionResult RedirectToLogin()
    {
        var returnUrl = Url.Action(nameof(Book), "Appointment");
        return RedirectToAction("Login", "Account", new { returnUrl });
    }

    private async Task<List<string>> GetCoachNamesAsync()
    {
        return await _context.Coaches
            .OrderBy(c => c.FullName)
            .Select(c => c.FullName)
            .ToListAsync();
    }

    private static string BuildUserDisplayName(AppUser user)
    {
        var nameParts = new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var joined = string.Join(" ", nameParts);
        return string.IsNullOrWhiteSpace(joined) ? user.Email : joined;
    }
}
