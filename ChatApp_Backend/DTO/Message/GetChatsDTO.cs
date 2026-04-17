using ChatApp_Backend.Models;

namespace ChatApp_Backend.DTO.Message
{
    public class GetChatsDTO
    {
        public string ChatType { get; set; }
        public string? ChatId { get; set; }
        public int? GroupId { get; set; }
        public string? GroupName { get; set; }
        public string SenderName { get; set; }
        public string LastMessage { get; set; }
        public MessageType MessageType { get; set; }
        public DateTime SendingTime { get; set; }
    }
}
