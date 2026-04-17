
using System.ComponentModel.DataAnnotations;

namespace ChatApp_Backend.Models
{
    public class Group
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AppUser> Users { get; set; } = new List<AppUser>();
    }
}
