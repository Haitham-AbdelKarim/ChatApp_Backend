using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;

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

    }
}
