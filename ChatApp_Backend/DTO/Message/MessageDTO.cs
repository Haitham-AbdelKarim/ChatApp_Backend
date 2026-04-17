using ChatApp_Backend.Models;
using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp_Backend.DTO.Message
{
    public class MessageDTO
    {
        public int Id { get; set; }
        [ForeignKey("Sender")]
        public string SenderId { get; set; }
        [ForeignKey("Group")]
        public int? GroupId { get; set; }
        public MessageType MessageType { get; set; }
        public string Message { get; set; }
        public DateTime SendingTime { get; set; }
    }
}
