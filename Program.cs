using System;
using System.Linq;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using WebProje.Data;
using WebProje.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<FitnessContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(8);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<FitnessContext>();
    context.Database.Migrate();

    var hasher = services.GetRequiredService<IPasswordHasher<AppUser>>();

    if (!context.Users.Any(u => u.Email == "admin@fitapp.com"))
    {
        var admin = new AppUser
        {
            FirstName = "Admin",
            LastName = "Kullanicisi",
            Email = "admin@fitapp.com",
            Role = "Admin"
        };
        admin.PasswordHash = hasher.HashPassword(admin, "admin123");
        context.Users.Add(admin);
    }

    if (!context.Users.Any(u => u.Email == "demo@fitapp.com"))
    {
        var demo = new AppUser
        {
            FirstName = "Demo",
            LastName = "uye",
            Email = "demo@fitapp.com",
            Role = "User"
        };
        demo.PasswordHash = hasher.HashPassword(demo, "demo123");
        context.Users.Add(demo);
    }

    context.SaveChanges();
    }

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

app.UseSession();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Account}/{action=Login}/{id?}")
    .WithStaticAssets();


app.Run();
