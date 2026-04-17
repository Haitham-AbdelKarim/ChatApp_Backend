using System.ComponentModel.DataAnnotations;

namespace ChatApp_Backend.DTO.Group
{
    public class CreateGroupRequestDTO
    {
        [Required]
        public string Name { get; set; }
        public string? Description { get; set; }
    }
}
