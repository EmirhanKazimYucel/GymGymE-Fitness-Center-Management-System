using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;
using WebProje.Models;

namespace WebProje.Controllers;

public class AppointmentController : Controller
{
    [HttpGet]
    public IActionResult Book()
    {
        var model = new AppointmentViewModel
        {
            AvailableCoaches = ["Mert Kaya", "Selin Ar", "Can Yıldız"],
            TimeSlots = ["09:00", "11:00", "14:30", "16:00", "18:00"],
            SelectedDate = DateOnly.FromDateTime(DateTime.Today)
        };
        ViewData["Title"] = "Randevu Oluştur";
        return View(model);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Book(AppointmentViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        TempData["AppointmentMessage"] = $"{model.SelectedDate:dd MMMM} tarihinde {model.SelectedCoach} ile {model.SelectedTime} randevunuz oluşturuldu.";
        return RedirectToAction("Index", "Dashboard");
    }
}
