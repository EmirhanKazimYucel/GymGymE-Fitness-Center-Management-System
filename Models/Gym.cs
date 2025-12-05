using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace GymManagement.Web.Models
{
    public class Gym
    {
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public string Address { get; set; }
        public string WorkingHours { get; set; }

        public virtual ICollection<Service> Services { get; set; }
    }
}