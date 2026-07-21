using System.ComponentModel.DataAnnotations.Schema;

namespace ChatApp_Backend.Models
{
    public enum MessageType
    {
        Text,
        Image,
        File
    }
    public class Messages
    {
        public int Id { get; set; }
        [ForeignKey("Sender")]
        public string SenderId { get; set; }
        [ForeignKey("Receiver")]
        public string? ReceiverId { get; set; }
        [ForeignKey("Group")]
        public int? GroupId {  get; set; }
        public MessageType MessageType { get; set; }
        public string Message { get; set; }
        public DateTime SendingTime { get; set; }
        public Group Group { get; set; }
        public AppUser Sender { get; set; }
        public AppUser Receiver { get; set; }
    }
}
