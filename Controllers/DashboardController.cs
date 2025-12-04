using Microsoft.AspNetCore.Mvc;

namespace WebProje.Controllers;

public class DashboardController : Controller
{
    [HttpGet]
    public IActionResult Index()
    {
        ViewData["Title"] = "Kullanıcı Paneli";
        return View();
    }
}
