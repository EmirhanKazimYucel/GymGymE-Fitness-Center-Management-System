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

        await PopulateBookingModelAsync(model, user);

        if (!model.Services.Any())
        {
            ModelState.AddModelError(string.Empty, "Şu anda randevu için tanımlı hizmet bulunmuyor.");
        }
        else if (model.Services.All(s => s.Id != model.SelectedServiceId))
        {
            ModelState.AddModelError(nameof(model.SelectedServiceId), "Geçerli bir hizmet seçiniz.");
        }

        if (model.Services.Any())
        {
            var allowedCoaches = model.ServiceCoachMap.TryGetValue(model.SelectedServiceId, out var list)
                ? list
                : Array.Empty<string>();

            model.AvailableCoaches = allowedCoaches;

            if (!allowedCoaches.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedCoach), "Bu hizmet için uygun koç bulunmuyor.");
            }
            else if (!allowedCoaches.Contains(model.SelectedCoach))
            {
                ModelState.AddModelError(nameof(model.SelectedCoach), "Koç seçimi hizmet ile uyumlu değil.");
            }
        }

        var today = DateOnly.FromDateTime(DateTime.Today);
        if (model.SelectedDate < today)
        {
            ModelState.AddModelError(nameof(model.SelectedDate), "Geçmiş tarihler için randevu alınamaz.");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var selectedService = model.Services.FirstOrDefault(s => s.Id == model.SelectedServiceId);
        if (selectedService is null)
        {
            ModelState.AddModelError(nameof(model.SelectedServiceId), "Seçilen hizmet bulunamadı.");
            return View(model);
        }

        var userSlotConflict = await _context.AppointmentRequests
            .AsNoTracking()
            .AnyAsync(a => a.Email == user.Email
                && a.Date == model.SelectedDate
                && a.TimeSlot == model.SelectedTime
                && a.Status != AppointmentStatus.Rejected);

        if (userSlotConflict)
        {
            ModelState.AddModelError(string.Empty, $"{model.SelectedDate:dd MMMM} {model.SelectedTime} dilimi için zaten bir talebiniz bulunuyor. Lütfen farklı bir saat seçin.");
            return View(model);
        }

        var slotConflictExists = await _context.AppointmentRequests
            .AsNoTracking()
            .AnyAsync(a => a.Coach == model.SelectedCoach
                && a.Date == model.SelectedDate
                && a.TimeSlot == model.SelectedTime
                && a.Status != AppointmentStatus.Rejected);

        if (slotConflictExists)
        {
            ModelState.AddModelError(string.Empty, $"{model.SelectedCoach} koçu {model.SelectedDate:dd MMMM} {model.SelectedTime} slotunda dolu. Lütfen farklı bir zaman seçin.");
            return View(model);
        }

        var serviceName = selectedService.Name;

        var request = new AppointmentRequest
        {
            FullName = BuildUserDisplayName(user),
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            Date = model.SelectedDate,
            TimeSlot = model.SelectedTime,
            Coach = model.SelectedCoach,
            ServiceName = serviceName,
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
        var model = new AppointmentViewModel
        {
            SelectedDate = DateOnly.FromDateTime(DateTime.Today)
        };

        await PopulateBookingModelAsync(model, user);
        return model;
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

    private async Task PopulateBookingModelAsync(AppointmentViewModel model, AppUser user)
    {
        var serviceData = await LoadServiceDataAsync();

        model.Services = serviceData.Services;
        model.ServiceCoachMap = serviceData.CoachMap.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value);

        if (model.SelectedServiceId == 0 && model.Services.Any())
        {
            model.SelectedServiceId = model.Services.First().Id;
        }

        var coaches = model.ServiceCoachMap.TryGetValue(model.SelectedServiceId, out var list)
            ? list
            : Array.Empty<string>();

        model.AvailableCoaches = coaches;

        if (string.IsNullOrWhiteSpace(model.SelectedCoach) && coaches.Any())
        {
            model.SelectedCoach = coaches.First();
        }

        model.TimeSlots = TimeSlots;
        model.UserFullName = BuildUserDisplayName(user);
        model.UserEmail = user.Email;
        model.UserPhone = user.PhoneNumber;
    }

    private async Task<ServiceData> LoadServiceDataAsync()
    {
        var services = await _context.Services
            .OrderBy(s => s.Name)
            .Select(s => new ServiceOption
            {
                Id = s.Id,
                Name = s.Name,
                Description = s.Description
            })
            .ToListAsync();

        var map = new Dictionary<int, List<string>>();

        if (services.Count > 0)
        {
            var serviceIds = services.Select(s => s.Id).ToArray();

            var pairs = await _context.CoachServices
                .Where(cs => serviceIds.Contains(cs.ServiceId))
                .Select(cs => new
                {
                    cs.ServiceId,
                    cs.Coach.FullName
                })
                .ToListAsync();

            map = pairs
                .GroupBy(p => p.ServiceId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(x => x.FullName).OrderBy(name => name).ToList());
        }

        return new ServiceData(services, map);
    }

    private static string BuildUserDisplayName(AppUser user)
    {
        var nameParts = new[] { user.FirstName, user.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part));
        var joined = string.Join(" ", nameParts);
        return string.IsNullOrWhiteSpace(joined) ? user.Email : joined;
    }

    private sealed record ServiceData(List<ServiceOption> Services, Dictionary<int, List<string>> CoachMap);
}
