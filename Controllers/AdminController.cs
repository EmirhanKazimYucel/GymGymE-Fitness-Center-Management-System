using Microsoft.AspNetCore.Mvc;

namespace WebProje.Controllers;

public class AdminController : Controller
{
    [HttpGet]
    public IActionResult Panel()
    {
        ViewData["Title"] = "Admin Paneli";
        return View();
    }
}
