using Microsoft.EntityFrameworkCore;
using WebProje.Models;

namespace WebProje.Data;

public class FitnessContext : DbContext
{
    public FitnessContext(DbContextOptions<FitnessContext> options) : base(options)
    {
    }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<AppointmentRequest> AppointmentRequests => Set<AppointmentRequest>();
    public DbSet<Coach> Coaches => Set<Coach>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<CoachService> CoachServices => Set<CoachService>();
<<<<<<< Updated upstream
    public DbSet<GymInfo> GymInfos => Set<GymInfo>();
    public DbSet<GymOpeningHour> GymOpeningHours => Set<GymOpeningHour>();
=======
>>>>>>> Stashed changes

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<CoachService>().HasKey(cs => new { cs.CoachId, cs.ServiceId });
        modelBuilder.Entity<CoachService>()
            .HasOne(cs => cs.Coach)
            .WithMany(c => c.CoachServices)
            .HasForeignKey(cs => cs.CoachId);
        modelBuilder.Entity<CoachService>()
            .HasOne(cs => cs.Service)
            .WithMany(s => s.CoachServices)
            .HasForeignKey(cs => cs.ServiceId);
    }
}
