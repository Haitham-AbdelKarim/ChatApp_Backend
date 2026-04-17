using ChatApp_Backend.Models;

namespace ChatApp_Backend.Services
{
    public interface IUserService
    {
        public Task<string> GenerateUserName(string firstname,  string lastname);
        public Task<AppUser?> GetCurrentUser(IHttpContextAccessor httpContextAccessor);
    }
}
