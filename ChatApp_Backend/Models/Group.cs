
namespace ChatApp_Backend.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Descrpition { get; set; }
        public DateTime CreatedAt { get; set; }
        public List<AppUser> Users { get; set; } = new List<AppUser>();
    }
}
