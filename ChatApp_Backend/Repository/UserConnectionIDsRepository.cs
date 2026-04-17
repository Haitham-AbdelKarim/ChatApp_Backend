using ChatApp_Backend.Context;
using ChatApp_Backend.Models;

namespace ChatApp_Backend.Repository
{
    public class UserConnectionIDsRepository : IUserConnectionIDsRepository
    {
        private readonly ChatAppDbContext chatAppDbContext;

        public UserConnectionIDsRepository(ChatAppDbContext chatAppDbContext)
        {
            this.chatAppDbContext = chatAppDbContext;
        }
        public List<string> GetUserConnectionID(string userId)
        {
            return chatAppDbContext.UserConnectionIDs.Where(u => u.UserId == userId).Select(u => u.ConnectionId).ToList();
        }

        public string? GetConnectionIDForUser(string connectionId)
        {
            return chatAppDbContext.UserConnectionIDs.Where(u => u.ConnectionId == connectionId).Select(u => u.ConnectionId).FirstOrDefault();
        }

        public void SetUserConnectionId(string userId, string connectionId)
        {
            var uCId = new UserConnectionIDs { UserId = userId, ConnectionId = connectionId };
            chatAppDbContext.UserConnectionIDs.Add(uCId);
            chatAppDbContext.SaveChanges();
        }

        public void RemoveConnectionId(string connectionId)
        {
            var uCId = chatAppDbContext.UserConnectionIDs.FirstOrDefault(u => u.ConnectionId == connectionId);
            if(uCId != null)
            {
                chatAppDbContext.UserConnectionIDs.Remove(uCId);
                chatAppDbContext.SaveChanges();
            }
        }
    }
}
