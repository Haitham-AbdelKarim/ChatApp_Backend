

using AutoMapper;
using ChatApp_Backend.Context;
using ChatApp_Backend.DTO.Message;
using ChatApp_Backend.Models;
using Microsoft.EntityFrameworkCore;

namespace ChatApp_Backend.Repository
{
    public class MessageRepository : IMessageRepository
    {
        private readonly ChatAppDbContext chatAppDbContext;
        private readonly IUserRepository userRepository;
        private readonly IMapper mapper;
        public MessageRepository(ChatAppDbContext chatAppDbContext, IUserRepository userRepository, IMapper mapper)
        {
            this.chatAppDbContext = chatAppDbContext;
            this.userRepository = userRepository;
            this.mapper = mapper;
        }

        public List<GetChatsDTO> GetUserChats(AppUser user)
        {
            string userId = user.Id;
            //List<Messages> messages = new List<Messages>();
            List <GetChatsDTO> chats = new List<GetChatsDTO>();
            var messages = chatAppDbContext.Messages.Include(m => m.Receiver).Where(m => m.SenderId == userId && m.ReceiverId != null)
                                                    .GroupBy(m => m.ReceiverId)
                                                    .Select(g => g.OrderByDescending(m => m.SendingTime).FirstOrDefault())
                                                    .ToList();
            foreach(var message in messages)
            {
                GetChatsDTO chatsDTO = new GetChatsDTO
                {
                    ChatType = "Direct",
                    ChatId = message.ReceiverId,
                    GroupId = null,
                    GroupName = null,
                    SenderName = $"{message.Receiver.FirstName} {message.Receiver.LastName}",
                    MessageType = message.MessageType,
                    LastMessage = message.Message,
                    SendingTime = message.SendingTime
                };
                chats.Add(chatsDTO);
            }

            messages = chatAppDbContext.Messages.Include(m => m.Sender).Where(m => m.SenderId != null && m.ReceiverId == userId)
                                                .GroupBy(m => m.SenderId)
                                                .Select(g => g.OrderByDescending(m => m.SendingTime).FirstOrDefault())
                                                .ToList();
            foreach (var message in messages)
            {
                GetChatsDTO chatsDTO = new GetChatsDTO
                {
                    ChatType = "Direct",
                    ChatId = message.SenderId,
                    GroupId = null,
                    GroupName = null,
                    SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}",
                    MessageType = message.MessageType,
                    LastMessage = message.Message,
                    SendingTime = message.SendingTime
                };
                chats.Add(chatsDTO);
            }

            List<Group> userGroups = user.Groups;
            foreach (var group in userGroups)
            {
                Messages? message = chatAppDbContext.Messages.Include(m => m.Sender)
                                                             .Where(m => m.GroupId == group.Id)
                                                             .OrderByDescending(group => group.SendingTime)
                                                             .FirstOrDefault();
                GetChatsDTO chatsDTO = new();
                if (message == null)
                {
                    chatsDTO.ChatType = "Group";
                    chatsDTO.GroupId = group.Id;
                    chatsDTO.GroupName = group.Name;
                }
                else
                {
                    chatsDTO = new GetChatsDTO
                    {
                        ChatType = "Group",
                        ChatId = message.SenderId,
                        GroupId = message.GroupId,
                        GroupName = group.Name,
                        SenderName = $"{message.Sender.FirstName} {message.Sender.LastName}",
                        MessageType = message.MessageType,
                        LastMessage = message.Message,
                        SendingTime = message.SendingTime
                    };
                }
                chats.Add(chatsDTO);
            }
            return chats;

        }

        public List<MessageDTO> GetDirectChatMessage(string senderId, string receiverId)
        {
            List<MessageDTO> chat = new List<MessageDTO>();
            List<Messages> messages = chatAppDbContext.Messages.Where(m => (m.SenderId == senderId && m.ReceiverId == receiverId) || (m.SenderId == receiverId && m.ReceiverId == senderId)).ToList();
            foreach(Messages message in messages)
            {
                MessageDTO messageDTO = mapper.Map<MessageDTO>(message);
                chat.Add(messageDTO);
            }
            return chat;
        }
    }
}
