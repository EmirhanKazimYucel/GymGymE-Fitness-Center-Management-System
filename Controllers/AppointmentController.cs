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

    public AppointmentController(FitnessContext context)
    {
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> Book(DateOnly? date)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ApplySidebarContext(user);
        var model = await BuildViewModelAsync(user, date);
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

        ApplySidebarContext(user);
        await PopulateBookingModelAsync(model, user);

        if (!model.Services.Any())
        {
            ModelState.AddModelError(string.Empty, "Şu anda randevu için tanımlı hizmet bulunmuyor.");
        }
        else if (!model.SelectedServiceId.HasValue || model.Services.All(s => s.Id != model.SelectedServiceId.Value))
        {
            ModelState.AddModelError(nameof(model.SelectedServiceId), "Geçerli bir hizmet seçiniz.");
        }

        if (model.Services.Any() && model.SelectedServiceId.HasValue)
        {
            var allowedCoaches = model.ServiceCoachMap.TryGetValue(model.SelectedServiceId.Value, out var list)
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
        else
        {
            model.AvailableCoaches = Array.Empty<string>();
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

        var selectedService = model.SelectedServiceId.HasValue
            ? model.Services.FirstOrDefault(s => s.Id == model.SelectedServiceId.Value)
            : null;
        if (selectedService is null)
        {
            ModelState.AddModelError(nameof(model.SelectedServiceId), "Seçilen hizmet bulunamadı.");
            return View(model);
        }

        var requestedDuration = NormalizeDuration(selectedService.DurationMinutes);
        if (!BookingTimeSlots.TryBuildInterval(model.SelectedTime, requestedDuration, out var requestedStart, out var requestedEnd))
        {
            ModelState.AddModelError(nameof(model.SelectedTime), "Seçilen saat aralığı desteklenmiyor.");
            return View(model);
        }

        var durationLookup = await BuildServiceDurationLookupAsync();

        var userSlotConflict = await _context.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.Email == user.Email
                && a.Date == model.SelectedDate
                && a.Status != AppointmentStatus.Rejected)
            .Select(a => new { a.TimeSlot, a.ServiceName })
            .ToListAsync();

        if (userSlotConflict.Any(entry =>
                BookingTimeSlots.TryBuildInterval(entry.TimeSlot, ResolveDuration(entry.ServiceName, durationLookup), out var start, out var end)
                && BookingTimeSlots.Overlaps(start, end, requestedStart, requestedEnd)))
        {
            ModelState.AddModelError(string.Empty, $"{model.SelectedDate:dd MMMM} tarihindeki mevcut randevunuz seçilen saat aralığıyla çakışıyor.");
            return View(model);
        }

        var coachConflicts = await _context.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.Coach == model.SelectedCoach
                && a.Date == model.SelectedDate
                && a.Status != AppointmentStatus.Rejected)
            .Select(a => new { a.TimeSlot, a.ServiceName })
            .ToListAsync();

        if (coachConflicts.Any(entry =>
                BookingTimeSlots.TryBuildInterval(entry.TimeSlot, ResolveDuration(entry.ServiceName, durationLookup), out var start, out var end)
                && BookingTimeSlots.Overlaps(start, end, requestedStart, requestedEnd)))
        {
            ModelState.AddModelError(string.Empty, $"{model.SelectedCoach} koçu {model.SelectedDate:dd MMMM} için seçilen aralıkta dolu.");
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

    private async Task<AppointmentViewModel> BuildViewModelAsync(AppUser user, DateOnly? preferredDate = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var initialDate = preferredDate.HasValue && preferredDate.Value >= today
            ? preferredDate.Value
            : today;

        var model = new AppointmentViewModel
        {
            SelectedDate = initialDate
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

    private void ApplySidebarContext(AppUser user)
    {
        var avatarUrl = BuildAvatarUrl(user.AvatarPath) ?? Url.Content("~/images/barbie.png");
        ViewData["SidebarAvatarUrl"] = avatarUrl;
        ViewData["SidebarUserName"] = BuildUserDisplayName(user);
    }

    private async Task PopulateBookingModelAsync(AppointmentViewModel model, AppUser user)
    {
        var serviceData = await LoadServiceDataAsync();

        model.Services = serviceData.Services;
        model.ServiceCoachMap = serviceData.CoachMap.ToDictionary(
            kvp => kvp.Key,
            kvp => (IReadOnlyList<string>)kvp.Value);

        var coaches = model.SelectedServiceId.HasValue
            && model.ServiceCoachMap.TryGetValue(model.SelectedServiceId.Value, out var list)
            ? list
            : Array.Empty<string>();

        model.AvailableCoaches = coaches;

        if (!string.IsNullOrWhiteSpace(model.SelectedCoach) && !coaches.Contains(model.SelectedCoach))
        {
            model.SelectedCoach = string.Empty;
        }

        model.TimeSlots = BookingTimeSlots.All;
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
                Description = s.Description,
                DurationMinutes = NormalizeDuration(s.DurationMinutes)
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

    private static string? BuildAvatarUrl(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return null;
        }

        return avatarPath.StartsWith('/') ? avatarPath : $"/{avatarPath}";
    }

    private static int NormalizeDuration(int? minutes)
    {
        return minutes.HasValue && minutes.Value > 0 ? minutes.Value : 60;
    }

    private static int ResolveDuration(string serviceName, IReadOnlyDictionary<string, int> durationLookup)
    {
        if (durationLookup.TryGetValue(serviceName, out var duration))
        {
            return duration;
        }

        return 60;
    }

    private async Task<Dictionary<string, int>> BuildServiceDurationLookupAsync()
    {
        return await _context.Services
            .Select(s => new { s.Name, Duration = NormalizeDuration(s.DurationMinutes) })
            .ToDictionaryAsync(x => x.Name, x => x.Duration, StringComparer.OrdinalIgnoreCase);
    }

    private sealed record ServiceData(List<ServiceOption> Services, Dictionary<int, List<string>> CoachMap);
}
