using ChatApp_Backend.Models;
using System.ComponentModel.DataAnnotations;

namespace ChatApp_Backend.DTO.Group
{
    public class GroupResponseDTO
    {
        public int Id { get; set; }
        [Required]
        public string Name { get; set; }
        public string? Descrpition { get; set; }
        public DateTime CreatedAt { get; set; }
        public int NumOFUsers { get; set; }
    }
}
