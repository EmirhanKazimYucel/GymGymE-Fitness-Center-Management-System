using System.ComponentModel.DataAnnotations;

namespace WebProje.Models;

public class AppUser
{
    public int Id { get; set; }

    [Required, MaxLength(64)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(64)]
    public string LastName { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    public DateOnly? BirthDate { get; set; }

    [Required]
    public string PasswordHash { get; set; } = string.Empty;

    [Required, MaxLength(32)]
    public string Role { get; set; } = "User";

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
