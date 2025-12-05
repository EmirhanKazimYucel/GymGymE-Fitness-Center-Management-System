using System;
using System.ComponentModel.DataAnnotations;

namespace GymManagement.Web.Models
{
    public class Appointment
    {
        public int Id { get; set; }

        [Required]
        public DateTime AppointmentTime { get; set; }

        public int ServiceId { get; set; }
        public virtual Service Service { get; set; }

        public int TrainerId { get; set; }
        public virtual Trainer Trainer { get; set; }

        // Member ID ve Navigation Property (ApplicationUser ile ilişki)
        public string MemberId { get; set; }
        public virtual ApplicationUser Member { get; set; }

        public bool IsConfirmed { get; set; } = false;
    }
}