using ChatApp_Backend.Context;
using ChatApp_Backend.Models;

namespace ChatApp_Backend.Repository
{
    public interface IUserConnectionIDsRepository
    {
        public List<string> GetUserConnectionID(string userId);
        public string? GetConnectionIDForUser(string connectionId);
        public void SetUserConnectionId(string userId, string connectionId);
        public void RemoveConnectionId(string connectionId);
    }

}
