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
}
