using ChatApp_Backend.DTO;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using System.Security.Claims;

namespace ChatApp_Backend.Services
{
    public class UserService : IUserService
    {
        private readonly IUserRepository userRepository;

        public UserService(IUserRepository userRepository)
        {
            this.userRepository = userRepository;
        }
        public async Task<string> GenerateUserName(string firstname, string lastname)
        {
            string userName = $"{firstname.Replace(" ", "")}{lastname.Replace(" ", "")}".ToLower();
            int count = await userRepository.CountUserNameStartWith(userName);
            return $"{userName}{count + 1}".ToLower();
        }

        public async Task<AppUser?> GetCurrentUser(IHttpContextAccessor httpContextAccessor)
        {
            Claim? claim = httpContextAccessor.HttpContext?.User.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Email);
            if (claim == null)
            {
                return null;
            }
            AppUser? appuser = await userRepository.GetUserByEmail(claim.Value);
            if (appuser == null)
            {
                return null;
            }
            return appuser;
        }
    }
}
