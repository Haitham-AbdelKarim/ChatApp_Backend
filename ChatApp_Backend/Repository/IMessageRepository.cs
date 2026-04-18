using ChatApp_Backend.DTO.Message;
using ChatApp_Backend.Models;

namespace ChatApp_Backend.Repository
{
    public interface IMessageRepository
    {
        public List<GetChatsDTO> GetUserChats(AppUser user);
        public List<MessageDTO> GetDirectChatMessage(string senderId, string receiverId);
        public List<MessageDTO> GetGroupChatMessage(int groupId);
        public void CreateMessage(Messages messages);
    }
}
