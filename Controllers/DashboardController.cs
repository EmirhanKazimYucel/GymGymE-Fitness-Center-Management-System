using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;
using WebProje.Services;

namespace WebProje.Controllers;

public class DashboardController : Controller
{
    private const string AvatarFolder = "uploads/avatars";
    private const long MaxAvatarBytes = 2 * 1024 * 1024; // 2 MB limit keeps uploads lightweight

    private readonly FitnessContext _context;
    private readonly IWebHostEnvironment _environment;
    private readonly IOpenAiDietService _openAiDietService;

    public DashboardController(FitnessContext context, IWebHostEnvironment environment, IOpenAiDietService openAiDietService)
    {
        _context = context;
        _environment = environment;
        _openAiDietService = openAiDietService;
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

    [HttpGet]
    public async Task<IActionResult> Profile()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        var model = BuildProfileViewModel(user);
        ViewData["Title"] = "Profilim";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(UserProfileViewModel model)
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        ViewData["Title"] = "Profilim";

        if (!ModelState.IsValid)
        {
            return View(RefreshProfileModelForDisplay(model, user));
        }

        var avatarValidationError = await TryProcessAvatarUploadAsync(model, user);
        if (avatarValidationError is not null)
        {
            ModelState.AddModelError(nameof(UserProfileViewModel.AvatarUpload), avatarValidationError);
            return View(RefreshProfileModelForDisplay(model, user));
        }

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.PhoneNumber = model.PhoneNumber;
        user.BirthDate = model.BirthDate;
        user.HeightCm = model.HeightCm;
        user.WeightKg = model.WeightKg;
        user.TargetWeightKg = model.TargetWeightKg;
        user.DietGoal = model.DietGoal;
        user.ActivityLevel = model.ActivityLevel;
        user.HealthConditions = model.HealthConditions;
        user.Allergies = model.Allergies;
        user.SpecialNotes = model.SpecialNotes;

        _context.Users.Update(user);
        await _context.SaveChangesAsync();

        TempData["ProfileMessage"] = "Profiliniz güncellendi.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet]
    public async Task<IActionResult> DietPlan()
    {
        var user = await GetCurrentUserAsync();
        if (user is null)
        {
            return RedirectToLogin();
        }

        var model = BuildBaselineDietPlan(user);

        try
        {
            var requestContext = DietPlanRequestContext.FromUser(user, model.BodyMassIndex, model.BmiCategory);
            var aiResult = await _openAiDietService.GeneratePlanAsync(requestContext, HttpContext.RequestAborted);
            if (aiResult is not null)
            {
                ApplyAiSuggestion(model, aiResult.Suggestion, aiResult.Model, aiResult.Provider);
            }
            else
            {
                model.Ai.ErrorMessage = "Yapay zeka yanıt veremediği için standart öneriler gösteriliyor.";
            }
        }
        catch (OperationCanceledException)
        {
            model.Ai.ErrorMessage = "Yapay zeka isteği iptal edildi, standart plan gösteriliyor.";
        }
        catch (Exception)
        {
            model.Ai.ErrorMessage = "Yapay zeka önerisi alınamadı, standart plan gösteriliyor.";
        }

        ViewData["Title"] = "Diyet Planım";
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

    private static UserProfileViewModel BuildProfileViewModel(AppUser user)
    {
        var bmi = CalculateBmi(user.HeightCm, user.WeightKg);
        return new UserProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email,
            PhoneNumber = user.PhoneNumber,
            BirthDate = user.BirthDate,
            HeightCm = user.HeightCm,
            WeightKg = user.WeightKg,
            TargetWeightKg = user.TargetWeightKg,
            DietGoal = user.DietGoal,
            ActivityLevel = user.ActivityLevel,
            HealthConditions = user.HealthConditions,
            Allergies = user.Allergies,
            SpecialNotes = user.SpecialNotes,
            AvatarUrl = BuildAvatarUrl(user.AvatarPath),
            BodyMassIndex = bmi,
            BmiCategory = DescribeBmi(bmi)
        };
    }

    private DietPlanViewModel BuildBaselineDietPlan(AppUser user)
    {
        var bmi = CalculateBmi(user.HeightCm, user.WeightKg);
        var category = DescribeBmi(bmi);
        return new DietPlanViewModel
        {
            UserFullName = BuildUserDisplayName(user),
            BodyMassIndex = bmi,
            BmiCategory = category,
            SuggestedCalories = SuggestCalories(category),
            MacroSplit = BuildMacroSplit(category),
            HydrationLiters = SuggestHydrationLiters(user.WeightKg),
            FocusTips = BuildFocusTips(category),
            MealIdeas = BuildMealIdeas(category),
            Ai = new DietPlanAiMetadata
            {
                GeneratedByAi = false,
                Source = "BarbieFit Standart"
            }
        };
    }

    private static void ApplyAiSuggestion(DietPlanViewModel model, AiDietPlanSuggestion suggestion, string? modelName, string? providerName)
    {
        if (suggestion.SuggestedCalories is int calories && calories > 0)
        {
            model.SuggestedCalories = calories;
        }

        if (suggestion.HydrationLiters is double liters && liters > 0)
        {
            model.HydrationLiters = Math.Round(liters, 1, MidpointRounding.AwayFromZero);
        }

        if (suggestion.MacroSplit is MacroDistribution macros)
        {
            model.MacroSplit = macros;
        }

        if (suggestion.FocusTips?.Count > 0)
        {
            model.FocusTips = suggestion.FocusTips;
        }

        if (suggestion.Meals?.Count > 0)
        {
            model.MealIdeas = suggestion.Meals;
        }

        model.Ai = new DietPlanAiMetadata
        {
            GeneratedByAi = true,
            Source = string.IsNullOrWhiteSpace(providerName) ? "OpenAI" : providerName,
            Model = modelName,
            MotivationMessage = suggestion.MotivationMessage,
            Cautions = suggestion.Cautions ?? Array.Empty<string>(),
            PlanTitle = suggestion.PlanTitle,
            ErrorMessage = null
        };
    }

    private static UserProfileViewModel RefreshProfileModelForDisplay(UserProfileViewModel model, AppUser user)
    {
        var bmi = CalculateBmi(model.HeightCm, model.WeightKg);
        model.Email = user.Email;
        model.AvatarUrl = BuildAvatarUrl(user.AvatarPath);
        model.BodyMassIndex = bmi;
        model.BmiCategory = DescribeBmi(bmi);
        return model;
    }

    private static int SuggestCalories(string bmiCategory)
    {
        return bmiCategory switch
        {
            "Zayıf" => 2300,
            "Normal" => 2000,
            "Fazla kilolu" => 1800,
            "Obez" => 1650,
            _ => 1950
        };
    }

    private static MacroDistribution BuildMacroSplit(string bmiCategory)
    {
        return bmiCategory switch
        {
            "Zayıf" => new MacroDistribution { CarbsPercent = 50, ProteinPercent = 25, FatPercent = 25 },
            "Normal" => new MacroDistribution { CarbsPercent = 45, ProteinPercent = 30, FatPercent = 25 },
            "Fazla kilolu" => new MacroDistribution { CarbsPercent = 35, ProteinPercent = 35, FatPercent = 30 },
            "Obez" => new MacroDistribution { CarbsPercent = 30, ProteinPercent = 40, FatPercent = 30 },
            _ => new MacroDistribution { CarbsPercent = 45, ProteinPercent = 30, FatPercent = 25 }
        };
    }

    private static double SuggestHydrationLiters(double? weightKg)
    {
        if (weightKg is null or <= 0)
        {
            return 2.5;
        }

        var liters = weightKg.Value * 0.035;
        return Math.Round(Math.Clamp(liters, 2.0, 4.0), 1, MidpointRounding.AwayFromZero);
    }

    private static IReadOnlyList<string> BuildFocusTips(string bmiCategory)
    {
        return bmiCategory switch
        {
            "Zayıf" => new[]
            {
                "Daha sık enerji yoğun ara öğünler planlayın",
                "Her öğüne ekstra kaliteli yağ ve protein ekleyin",
                "Her gün en az 7 saat uykuya odaklanın"
            },
            "Normal" => new[]
            {
                "Renkli tabak hedefi: her öğünde 3 farklı renk",
                "Haftada 3 gün kuvvet antrenmanı sonrası protein",
                "Gün içinde 10.000 adım hedefi"
            },
            "Fazla kilolu" => new[]
            {
                "Günlük şekerli içecekleri limonlu suyla değiştirin",
                "Tabaklarınızın yarısını sebze ile doldurun",
                "Akşam 20.00 sonrası atıştırmalara mola verin"
            },
            "Obez" => new[]
            {
                "Her öğünden önce 2 bardak su için",
                "Nişastalı karbonhidratları haftada 3 öğünle sınırlandırın",
                "Yavaş yemek için 20 dakika kuralını uygulayın"
            },
            _ => new[]
            {
                "Haftalık alışveriş listesi hazırlayın",
                "Protein/karbonhidrat/yağ dengesini takip edin",
                "Su şişenizi asla boş bırakmayın"
            }
        };
    }

    private static IReadOnlyList<DietMealIdea> BuildMealIdeas(string bmiCategory)
    {
        var palette = new Dictionary<string, string>
        {
            ["Zayıf"] = "#80deea",
            ["Normal"] = "#a5d6a7",
            ["Fazla kilolu"] = "#ffcc80",
            ["Obez"] = "#ef9a9a"
        };

        var accent = palette.TryGetValue(bmiCategory, out var color) ? color : "#ff80ab";

        return new[]
        {
            new DietMealIdea
            {
                Meal = "Sabah",
                Description = "Proteinli smoothie + yulaf + chia",
                Accent = accent
            },
            new DietMealIdea
            {
                Meal = "Öğle",
                Description = "Izgara protein, bol yeşillik, tahıllı taban",
                Accent = "#ffd54f"
            },
            new DietMealIdea
            {
                Meal = "Akşam",
                Description = "Fırın sebze, bakliyat ve probiyotik tabak",
                Accent = "#ce93d8"
            },
            new DietMealIdea
            {
                Meal = "Ara",
                Description = "Kuruyemiş + yoğurt + taze meyve",
                Accent = "#b39ddb"
            }
        };
    }

    private static double? CalculateBmi(double? heightCm, double? weightKg)
    {
        if (heightCm is null or <= 0 || weightKg is null or <= 0)
        {
            return null;
        }

        var heightM = heightCm.Value / 100d;
        if (heightM <= 0)
        {
            return null;
        }

        var bmi = weightKg.Value / (heightM * heightM);
        return Math.Round(bmi, 1, MidpointRounding.AwayFromZero);
    }

    private static string DescribeBmi(double? bmi)
    {
        if (bmi is null)
        {
            return "—";
        }

        return bmi switch
        {
            < 18.5 => "Zayıf",
            < 25 => "Normal",
            < 30 => "Fazla kilolu",
            _ => "Obez"
        };
    }

    private static string? BuildAvatarUrl(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath))
        {
            return null;
        }

        return avatarPath.StartsWith('/') ? avatarPath : $"/{avatarPath}";
    }

    private async Task<string?> TryProcessAvatarUploadAsync(UserProfileViewModel model, AppUser user)
    {
        if (model.AvatarUpload is null || model.AvatarUpload.Length == 0)
        {
            return null;
        }

        if (!model.AvatarUpload.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
        {
            return "Lütfen yalnızca görsel dosyaları yükleyin.";
        }

        if (model.AvatarUpload.Length > MaxAvatarBytes)
        {
            return "Dosya boyutu 2 MB'ı geçmemelidir.";
        }

        if (string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return "Sunucu yükleme dizini bulunamadı.";
        }

        var uploadsRoot = Path.Combine(_environment.WebRootPath, AvatarFolder.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(uploadsRoot);

        var extension = Path.GetExtension(model.AvatarUpload.FileName);
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsRoot, fileName);

        await using (var stream = System.IO.File.Create(filePath))
        {
            await model.AvatarUpload.CopyToAsync(stream);
        }

        RemoveExistingAvatar(user.AvatarPath);
        user.AvatarPath = $"/{AvatarFolder}/{fileName}";
        return null;
    }

    private void RemoveExistingAvatar(string? avatarPath)
    {
        if (string.IsNullOrWhiteSpace(avatarPath) || string.IsNullOrWhiteSpace(_environment.WebRootPath))
        {
            return;
        }

        var relativePath = avatarPath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
        var fullPath = Path.Combine(_environment.WebRootPath, relativePath);
        if (System.IO.File.Exists(fullPath))
        {
            System.IO.File.Delete(fullPath);
        }
    }
}
