using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp_Backend.Models
{
    public class UserConnectionIDs
    {
        [Key]
        public string ConnectionId { get; set; }
        [ForeignKey("User")]
        public string UserId { get; set; }
        public AppUser User { get; set; }
    }
}
