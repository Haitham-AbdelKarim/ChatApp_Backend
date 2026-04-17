using ChatApp_Backend.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ChatApp_Backend.Context
{
    public class ChatAppDbContext : IdentityDbContext<AppUser>
    {
        public DbSet<Group> Groups { get; set; }
        public DbSet<Messages> Messages { get; set; }
        public ChatAppDbContext(DbContextOptions<ChatAppDbContext> options) : base(options)
        {
        }
    }
}
