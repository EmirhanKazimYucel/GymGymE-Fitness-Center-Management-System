using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

namespace WebProje.Controllers;

public class AccountController : Controller
{
<<<<<<< Updated upstream
=======
    private const string RoleAdmin = "Admin";
    private const string RoleUser = "User";
>>>>>>> Stashed changes
    private readonly FitnessContext _context;
    private readonly IPasswordHasher<AppUser> _passwordHasher;

    public AccountController(FitnessContext context, IPasswordHasher<AppUser> passwordHasher)
    {
        _context = context;
        _passwordHasher = passwordHasher;
    }

    [HttpGet]
    public IActionResult Login(string? returnUrl = null, string tab = "login")
    {
        var viewModel = new AuthPageViewModel
        {
            Login = new LoginViewModel { ReturnUrl = returnUrl }
        };
        ViewData["ActiveTab"] = tab;
        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login([Bind(Prefix = "Login")] LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BuildAuthView(model, null, "login");
        }

        var user = await _context.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Email == model.Email);

        if (user is null)
        {
            ModelState.AddModelError("Login.Email", "E-posta veya şifre hatalı.");
            return BuildAuthView(model, null, "login");
        }

        var verification = _passwordHasher.VerifyHashedPassword(user, user.PasswordHash, model.Password);

        if (verification == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError("Login.Email", "E-posta veya şifre hatalı.");
            return BuildAuthView(model, null, "login");
        }

        HttpContext.Session.SetInt32(SessionKeys.UserId, user.Id);
        HttpContext.Session.SetString(SessionKeys.UserRole, user.Role);

        var displayName = string.Join(" ", new[] { user.FirstName, user.LastName }.Where(s => !string.IsNullOrWhiteSpace(s)));
        TempData["LoginMessage"] = $"Hoş geldin {(string.IsNullOrWhiteSpace(displayName) ? user.Email : displayName)}!";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return user.Role switch
        {
<<<<<<< Updated upstream
            RoleNames.Admin => RedirectToAction("Panel", "Admin"),
            RoleNames.User => RedirectToAction("Index", "Dashboard"),
=======
            RoleAdmin => RedirectToAction("Panel", "Admin"),
            RoleUser => RedirectToAction("Index", "Dashboard"),
>>>>>>> Stashed changes
            _ => RedirectToAction("Index", "Home")
        };
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register([Bind(Prefix = "Register")] RegisterViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BuildAuthView(null, model, "register");
        }

        if (await _context.Users.AnyAsync(u => u.Email == model.Email))
        {
            ModelState.AddModelError("Register.Email", "Bu e-posta ile kayıt zaten mevcut.");
            return BuildAuthView(null, model, "register");
        }

        var user = new AppUser
        {
            FirstName = model.FirstName,
            LastName = model.LastName,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            BirthDate = model.BirthDate,
<<<<<<< Updated upstream
            Role = RoleNames.User
=======
            Role = RoleUser
>>>>>>> Stashed changes
        };

        user.PasswordHash = _passwordHasher.HashPassword(user, model.Password);

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        TempData["RegisterSuccess"] = "Kaydınız oluşturuldu. Şimdi giriş yapabilirsiniz.";
        return RedirectToAction(nameof(Login));
    }

<<<<<<< Updated upstream
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        TempData["LoginMessage"] = "Oturum başarıyla kapatıldı.";
        return RedirectToAction(nameof(Login));
    }

=======
>>>>>>> Stashed changes
    private IActionResult BuildAuthView(LoginViewModel? login, RegisterViewModel? register, string activeTab)
    {
        var viewModel = new AuthPageViewModel
        {
            Login = login ?? new LoginViewModel(),
            Register = register ?? new RegisterViewModel()
        };
        ViewData["ActiveTab"] = activeTab;
        return View("Login", viewModel);
    }

}
