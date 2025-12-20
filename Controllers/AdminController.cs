using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Filters;
using WebProje.Models;

namespace WebProje.Controllers;

[RoleAuthorize(RoleNames.Admin)]
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
        appointment.DecisionBy = await ResolveDecisionOwnerAsync();

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
    public async Task<IActionResult> EditService(EditServiceInputModel input)
    {
        if (!ModelState.IsValid)
        {
            TempData["AdminMessage"] = "Hizmet bilgilerini doğrulayın.";
            return RedirectToAction(nameof(Panel));
        }

        var service = await _context.Services.FirstOrDefaultAsync(s => s.Id == input.Id);
        if (service is null)
        {
            TempData["AdminMessage"] = "Hizmet bulunamadı.";
            return RedirectToAction(nameof(Panel));
        }

        service.Name = input.Name.Trim();
        service.Description = string.IsNullOrWhiteSpace(input.Description) ? null : input.Description.Trim();
        service.DurationMinutes = input.DurationMinutes;
        service.Price = input.Price;

        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Hizmet güncellendi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteService(int id)
    {
        var service = await _context.Services
            .Include(s => s.CoachServices)
            .FirstOrDefaultAsync(s => s.Id == id);

        if (service is null)
        {
            TempData["AdminMessage"] = "Silinecek hizmet bulunamadı.";
            return RedirectToAction(nameof(Panel));
        }

        if (service.CoachServices.Any())
        {
            _context.CoachServices.RemoveRange(service.CoachServices);
        }

        _context.Services.Remove(service);
        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Hizmet silindi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGymInfo([Bind(Prefix = "GymInfo")] GymInfoViewModel input)
    {
        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPanelViewModel(null, null, input);
            return View("Panel", invalidModel);
        }

        var entity = await _context.GymInfos.FirstOrDefaultAsync();
        if (entity is null)
        {
            entity = new GymInfo();
            _context.GymInfos.Add(entity);
        }

        var trimmedName = input.Name?.Trim();
        entity.Name = string.IsNullOrWhiteSpace(trimmedName) ? entity.Name : trimmedName;
        entity.Address = NormalizeText(input.Address);
        entity.Phone = NormalizeText(input.Phone);
        entity.Email = NormalizeText(input.Email);
        entity.Website = NormalizeText(input.Website);
        entity.WeekdayHours = NormalizeText(input.WeekdayHours);
        entity.WeekendHours = NormalizeText(input.WeekendHours);
        entity.About = NormalizeText(input.About);
        entity.Facilities = NormalizeText(input.Facilities);
        entity.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Spor salonu bilgileri güncellendi.";
        return RedirectToAction(nameof(Panel));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateGymHours([FromForm] List<GymOpeningHourInputModel> schedule)
    {
        schedule ??= new List<GymOpeningHourInputModel>();
        var normalized = schedule
            .GroupBy(item => item.DayOfWeek)
            .Select(group => group.First())
            .ToList();

        var orderedSchedule = OrderSchedule(normalized);

        foreach (var entry in orderedSchedule)
        {
            if (entry.IsClosed)
            {
                continue;
            }

            if (!TryParseTime(entry.OpenTime, out var open) || !TryParseTime(entry.CloseTime, out var close))
            {
                ModelState.AddModelError(string.Empty, $"{entry.DayOfWeek} günü için geçerli açılış/kapanış saati giriniz.");
                continue;
            }

            if (close <= open)
            {
                ModelState.AddModelError(string.Empty, $"{entry.DayOfWeek} günü için kapanış saati açılıştan sonra olmalıdır.");
            }
        }

        if (!ModelState.IsValid)
        {
            var invalidModel = await BuildPanelViewModel(null, null, null, orderedSchedule);
            return View("Panel", invalidModel);
        }

        var existing = await _context.GymOpeningHours.ToDictionaryAsync(h => h.DayOfWeek);
        foreach (var item in orderedSchedule)
        {
            if (!existing.TryGetValue(item.DayOfWeek, out var entity))
            {
                entity = new GymOpeningHour { DayOfWeek = item.DayOfWeek };
                _context.GymOpeningHours.Add(entity);
            }

            if (item.IsClosed)
            {
                entity.IsClosed = true;
                entity.OpenTime = null;
                entity.CloseTime = null;
            }
            else
            {
                entity.IsClosed = false;
                entity.OpenTime = TryParseTime(item.OpenTime, out var open) ? open : null;
                entity.CloseTime = TryParseTime(item.CloseTime, out var close) ? close : null;
            }
            entity.UpdatedAtUtc = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        TempData["AdminMessage"] = "Çalışma saatleri güncellendi.";
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

    private async Task<AdminPanelViewModel> BuildPanelViewModel(
        NewCoachInputModel? coachForm = null,
        NewServiceInputModel? serviceForm = null,
        GymInfoViewModel? gymInfoForm = null,
        IReadOnlyList<GymOpeningHourInputModel>? gymHoursForm = null)
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

        var approvedAppointments = await _context.AppointmentRequests
            .Where(a => a.Status == AppointmentStatus.Approved)
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

        var gymInfoEntity = await _context.GymInfos.AsNoTracking().FirstOrDefaultAsync();
        var gymHoursEntities = await _context.GymOpeningHours.AsNoTracking().ToListAsync();
        var usageMetrics = await BuildBookingUsageMetricsAsync();

        return new AdminPanelViewModel
        {
            PendingAppointments = pending,
            RecentDecisions = decisions,
            Coaches = coachItems,
            Services = services,
            NewCoach = coachForm ?? new NewCoachInputModel(),
            NewService = serviceForm ?? new NewServiceInputModel(),
            CoachSchedules = schedule,
            GymInfo = gymInfoForm ?? BuildGymInfoViewModel(gymInfoEntity),
            GymHours = gymHoursForm ?? BuildGymHoursViewModel(gymHoursEntities),
            BookingUsageMetrics = usageMetrics
        };
    }

    private async Task<string> ResolveDecisionOwnerAsync()
    {
        var userId = HttpContext.Session.GetInt32(SessionKeys.UserId);
        if (userId is null)
        {
            return RoleNames.Admin;
        }

        var admin = await _context.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId.Value);
        if (admin is null)
        {
            return RoleNames.Admin;
        }

        var displayName = string.Join(" ", new[] { admin.FirstName, admin.LastName }
            .Where(part => !string.IsNullOrWhiteSpace(part)));
        return string.IsNullOrWhiteSpace(displayName) ? admin.Email : displayName;
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

    private static GymInfoViewModel BuildGymInfoViewModel(GymInfo? entity)
    {
        if (entity is null)
        {
            return new GymInfoViewModel
            {
                Name = "GymGyme Studio",
                WeekdayHours = "Hafta içi 08:00 - 22:00",
                WeekendHours = "Hafta sonu 09:00 - 20:00"
            };
        }

        return new GymInfoViewModel
        {
            Name = entity.Name,
            Address = entity.Address,
            Phone = entity.Phone,
            Email = entity.Email,
            Website = entity.Website,
            WeekdayHours = entity.WeekdayHours,
            WeekendHours = entity.WeekendHours,
            About = entity.About,
            Facilities = entity.Facilities,
            UpdatedAtUtc = entity.UpdatedAtUtc
        };
    }

    private static string? NormalizeText(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }

    private static TimeSpan? ParseTimeSpan(string? value)
    {
        return TryParseTime(value, out var result) ? result : null;
    }

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        return TimeSpan.TryParse(value, out time);
    }

    private static IReadOnlyList<GymOpeningHourInputModel> BuildGymHoursViewModel(IEnumerable<GymOpeningHour>? entities)
    {
        var map = entities?.ToDictionary(e => e.DayOfWeek) ?? new Dictionary<DayOfWeek, GymOpeningHour>();
        var order = new[]
        {
            DayOfWeek.Monday,
            DayOfWeek.Tuesday,
            DayOfWeek.Wednesday,
            DayOfWeek.Thursday,
            DayOfWeek.Friday,
            DayOfWeek.Saturday,
            DayOfWeek.Sunday
        };

        return order
            .Select(day => map.TryGetValue(day, out var hour)
                ? new GymOpeningHourInputModel
                {
                    DayOfWeek = day,
                    IsClosed = hour.IsClosed,
                    OpenTime = hour.OpenTime?.ToString(@"hh\:mm"),
                    CloseTime = hour.CloseTime?.ToString(@"hh\:mm")
                }
                : new GymOpeningHourInputModel
                {
                    DayOfWeek = day,
                    IsClosed = false,
                    OpenTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? "09:00" : "08:00",
                    CloseTime = day is DayOfWeek.Saturday or DayOfWeek.Sunday ? "20:00" : "22:00"
                })
            .ToList();
    }

    private static IReadOnlyList<GymOpeningHourInputModel> OrderSchedule(IEnumerable<GymOpeningHourInputModel> inputs)
    {
        var source = (inputs ?? Enumerable.Empty<GymOpeningHourInputModel>())
            .GroupBy(item => item.DayOfWeek)
            .Select(group => group.First())
            .ToDictionary(item => item.DayOfWeek);

        return Enum.GetValues<DayOfWeek>()
            .OrderBy(day => ((int)day + 6) % 7)
            .Select(day => source.TryGetValue(day, out var existing)
                ? new GymOpeningHourInputModel
                {
                    DayOfWeek = existing.DayOfWeek,
                    IsClosed = existing.IsClosed,
                    OpenTime = existing.OpenTime,
                    CloseTime = existing.CloseTime
                }
                : new GymOpeningHourInputModel { DayOfWeek = day, IsClosed = true })
            .ToList();
    }

    private async Task<IReadOnlyList<BookingUsageMetric>> BuildBookingUsageMetricsAsync()
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var earliest = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);

        var rows = await _context.AppointmentRequests
            .AsNoTracking()
            .Where(a => a.Status == AppointmentStatus.Approved && a.Date >= earliest)
            .Select(a => new AppointmentUsageRow
            {
                Date = a.Date,
                ServiceName = a.ServiceName,
                Coach = a.Coach
            })
            .ToListAsync();

        return BuildUsageSeries(rows, today);
    }

    private static IReadOnlyList<BookingUsageMetric> BuildUsageSeries(IEnumerable<AppointmentUsageRow> rows, DateOnly today)
    {
        var history = rows?.ToList() ?? new List<AppointmentUsageRow>();
        var configs = BuildUsagePeriodConfigs(today);
        var metrics = new List<BookingUsageMetric>(configs.Count * 2);

        foreach (var config in configs)
        {
            var filtered = history.Where(r => r.Date >= config.FilterStart).ToList();
            metrics.Add(BuildUsageMetric(filtered, config, "service", r => r.ServiceName));
            metrics.Add(BuildUsageMetric(filtered, config, "coach", r => r.Coach));
        }

        return metrics;
    }

    private static IReadOnlyList<UsagePeriodConfig> BuildUsagePeriodConfigs(DateOnly today)
    {
        var culture = CultureInfo.GetCultureInfo("tr-TR");
        var configs = new List<UsagePeriodConfig>(3);

        var weeklyStart = today.AddDays(-6);
        var weeklyLabels = Enumerable.Range(0, 7)
            .Select(offset => weeklyStart.AddDays(offset).ToString("ddd dd", culture))
            .ToList()
            .AsReadOnly();
        configs.Add(new UsagePeriodConfig
        {
            Key = "weekly",
            FilterStart = weeklyStart,
            BucketCount = weeklyLabels.Count,
            Labels = weeklyLabels,
            BucketSelector = row =>
            {
                var diff = row.Date.DayNumber - weeklyStart.DayNumber;
                return diff is >= 0 and < 7 ? diff : null;
            }
        });

        var monthlyStart = today.AddDays(-29);
        var monthlyLabels = Enumerable.Range(0, 30)
            .Select(offset => monthlyStart.AddDays(offset).ToString("dd MMM", culture))
            .ToList()
            .AsReadOnly();
        configs.Add(new UsagePeriodConfig
        {
            Key = "monthly",
            FilterStart = monthlyStart,
            BucketCount = monthlyLabels.Count,
            Labels = monthlyLabels,
            BucketSelector = row =>
            {
                var diff = row.Date.DayNumber - monthlyStart.DayNumber;
                return diff is >= 0 and < 30 ? diff : null;
            }
        });

        var monthAnchor = new DateOnly(today.Year, today.Month, 1).AddMonths(-11);
        var yearlyMonths = Enumerable.Range(0, 12)
            .Select(offset => monthAnchor.AddMonths(offset))
            .ToList();
        var yearlyLabels = yearlyMonths
            .Select(date => date.ToString("MMM yy", culture))
            .ToList()
            .AsReadOnly();
        configs.Add(new UsagePeriodConfig
        {
            Key = "yearly",
            FilterStart = monthAnchor,
            BucketCount = yearlyLabels.Count,
            Labels = yearlyLabels,
            BucketSelector = row =>
            {
                if (row.Date < monthAnchor)
                {
                    return null;
                }

                var diff = (row.Date.Year - monthAnchor.Year) * 12 + row.Date.Month - monthAnchor.Month;
                if (diff < 0 || diff >= yearlyLabels.Count)
                {
                    return null;
                }

                return diff;
            }
        });

        return configs;
    }

    private static BookingUsageMetric BuildUsageMetric(
        IEnumerable<AppointmentUsageRow> rows,
        UsagePeriodConfig config,
        string categoryType,
        Func<AppointmentUsageRow, string?> labelSelector)
    {
        var seriesMap = new Dictionary<string, int[]>(StringComparer.OrdinalIgnoreCase);

        foreach (var row in rows)
        {
            var bucketIndex = config.BucketSelector(row);
            if (bucketIndex is null)
            {
                continue;
            }

            var label = NormalizeUsageLabel(labelSelector(row));
            if (!seriesMap.TryGetValue(label, out var points))
            {
                points = new int[config.BucketCount];
                seriesMap[label] = points;
            }

            points[bucketIndex.Value] += 1;
        }

        var series = seriesMap
            .Select(entry => new BookingUsageMetricSeries
            {
                Label = entry.Key,
                Points = Array.AsReadOnly(entry.Value),
                Total = entry.Value.Sum()
            })
            .OrderByDescending(item => item.Total)
            .ThenBy(item => item.Label, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new BookingUsageMetric
        {
            Period = config.Key,
            CategoryType = categoryType,
            Labels = config.Labels,
            Series = series
        };
    }

    private static string NormalizeUsageLabel(string? label)
    {
        return string.IsNullOrWhiteSpace(label)
            ? "Tanımsız"
            : label.Trim();
    }

    private sealed record AppointmentUsageRow
    {
        public DateOnly Date { get; init; }
        public string? ServiceName { get; init; }
        public string? Coach { get; init; }
    }

    private sealed class UsagePeriodConfig
    {
        public string Key { get; init; } = string.Empty;
        public DateOnly FilterStart { get; init; }
        public int BucketCount { get; init; }
        public IReadOnlyList<string> Labels { get; init; } = Array.Empty<string>();
        public Func<AppointmentUsageRow, int?> BucketSelector { get; init; } = default!;
    }
}
