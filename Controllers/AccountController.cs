using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using WebProje.Models;

namespace WebProje.Controllers;

public class AccountController : Controller
{
    private const string RoleAdmin = "Admin";
    private const string RoleUser = "User";

    private static readonly List<UserCredential> Users =
    [
        new("demo", "demo123", "Demo Kullanıcısı", RoleUser),
        new("admin", "admin123", "Yönetici", RoleAdmin)
    ];

    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        var model = new LoginViewModel { ReturnUrl = returnUrl };
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var user = Users.FirstOrDefault(u =>
            string.Equals(u.Username, model.Username, StringComparison.OrdinalIgnoreCase) &&
            u.Password == model.Password);

        if (user is null)
        {
            ModelState.AddModelError(string.Empty, "Kullanıcı adı veya şifre hatalı.");
            return View(model);
        }

        TempData["LoginMessage"] = $"Hoş geldin {user.DisplayName}!";

        if (!string.IsNullOrWhiteSpace(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
        {
            return Redirect(model.ReturnUrl);
        }

        return user.Role switch
        {
            RoleAdmin => RedirectToAction("Panel", "Admin"),
            RoleUser => RedirectToAction("Index", "Dashboard"),
            _ => RedirectToAction("Index", "Home")
        };
    }

    private sealed record UserCredential(string Username, string Password, string DisplayName, string Role);
}
