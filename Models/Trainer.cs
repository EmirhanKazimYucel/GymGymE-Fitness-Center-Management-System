using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GymManagement.Web.Models
{
    public class Trainer
    {
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string FirstName { get; set; }

        [Required]
        [StringLength(50)]
        public string LastName { get; set; }

        public string Expertise { get; set; }

        public virtual ICollection<Appointment> Appointments { get; set; }
    }
}