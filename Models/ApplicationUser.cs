// Models/ApplicationUser.cs
using Microsoft.AspNetCore.Identity;
using System.Collections.Generic;

namespace GymManagement.Web.Models
{
    // IdentityUser'dan miras alır
    public class ApplicationUser : IdentityUser
    {
        public string FullName { get; set; }

        // Yapay zeka entegrasyonu için kullanılabilecek alanlar
        public double? Height { get; set; } // Boy (cm)
        public double? Weight { get; set; } // Kilo (kg)

        // Navigation Property: Üyenin randevuları
        public virtual ICollection<Appointment> Appointments { get; set; }
    }
}