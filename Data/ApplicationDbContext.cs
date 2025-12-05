// Data/ApplicationDbContext.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore; // Bu paket gerekliydi
using GymManagement.Web.Models;
using Microsoft.AspNetCore.Identity;

namespace GymManagement.Web.Data
{
    // IdentityDbContext<ApplicationUser>'dan miras alarak Identity tablolarını dahil ediyoruz.
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Proje Varlıkları
        public DbSet<Gym> Gyms { get; set; }
        public DbSet<Trainer> Trainers { get; set; }
        public DbSet<Service> Services { get; set; }
        public DbSet<Appointment> Appointments { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            // Identity tablolarının oluşturulması için bu satır ZORUNLUDUR.
            base.OnModelCreating(builder);

            // İlişkiler: Appointment ve Member (ApplicationUser)
            builder.Entity<Appointment>()
                .HasOne(a => a.Member)
                .WithMany(u => u.Appointments)
                .HasForeignKey(a => a.MemberId)
                .IsRequired(false) // Üyelik silinse bile randevu kaydını tutabilir
                .OnDelete(DeleteBehavior.Restrict);

            // İlişkiler: Appointment ve Trainer
            builder.Entity<Appointment>()
                .HasOne(a => a.Trainer)
                .WithMany(t => t.Appointments)
                .HasForeignKey(a => a.TrainerId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}