using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;

namespace ChatApp_Backend.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly IUserConnectionIDsRepository userConnectionIDsRepository;
        private readonly IUserRepository userRepository;
        private readonly IMessageRepository messageRepository;

        public ChatHub(IUserConnectionIDsRepository userConnectionIDsRepository, IUserRepository userRepository, IMessageRepository messageRepository)
        {
            this.userConnectionIDsRepository = userConnectionIDsRepository;
            this.userRepository = userRepository;
            this.messageRepository = messageRepository;
        }
        //[Authorize]
        public override async Task OnConnectedAsync()
        {
            userConnectionIDsRepository.SetUserConnectionId(Context.UserIdentifier??string.Empty, Context.ConnectionId);
            AppUser? appUser = await userRepository.GetUserById(Context.UserIdentifier ?? string.Empty);
            if(appUser != null)
            {
                foreach(var group in appUser.Groups)
                {
                    await Groups.AddToGroupAsync(Context.ConnectionId, group.Id.ToString());
                }
            }
            return;
        }

        public async Task SendDirectMessage(string receiverId, string message, MessageType type)
        {
            var senderId = Context.UserIdentifier;

            var msg = new Messages
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Message = message, // text OR file path
                MessageType = type,
                SendingTime = DateTime.Now
            };
            messageRepository.CreateMessage(msg);
            // send to receiver
            await Clients.User(receiverId).SendAsync("ReceiveMessage", new
            {
                msg.Id,
                msg.Message,
                msg.MessageType,
                msg.SenderId,
                msg.SendingTime
            });
            // send back to sender (for UI sync)
            await Clients.Caller.SendAsync("ReceiveMessage", msg);
        }

        public async Task SendGroupMessage(int groupId, string message, MessageType type)
        {
            var senderId = Context.UserIdentifier;

            var msg = new Messages
            {
                SenderId = senderId,
                GroupId = groupId,
                Message = message, // text OR file path
                MessageType = type,
                SendingTime = DateTime.Now
            };
            messageRepository.CreateMessage(msg);
            // send to receiver
            await Clients.Group(groupId.ToString()).SendAsync("ReceiveMessage", new
            {
                msg.Id,
                msg.Message,
                msg.MessageType,
                msg.SenderId,
                msg.SendingTime
            });
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            userConnectionIDsRepository.RemoveConnectionId(Context.ConnectionId);
            AppUser? appUser = await userRepository.GetUserById(Context.UserIdentifier ?? string.Empty);
            if (appUser != null)
            {
                foreach (var group in appUser.Groups)
                {
                    await Groups.RemoveFromGroupAsync(Context.ConnectionId, group.Id.ToString());
                }
            }
            return;
        }
    }
}
