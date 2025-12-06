namespace WebProje.Models;

public class CoachService
{
    public int CoachId { get; set; }
    public Coach Coach { get; set; } = null!;
    public int ServiceId { get; set; }
    public Service Service { get; set; } = null!;
    public DateTime LinkedAtUtc { get; set; } = DateTime.UtcNow;
}
