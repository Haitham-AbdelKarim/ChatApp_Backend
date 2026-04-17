using ChatApp_Backend.Models;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace ChatApp_Backend.Repository
{
    public interface IUserRepository
    {
        public Task<AppUser?> GetUserByEmail(string email);
        public Task<AppUser?> GetUserById(string id);
        public Task<AppUser?> GetUserByRefreshToken(string refreshToken);
        public Task<IdentityResult> AddUser(AppUser user, string passWord);
        public Task<IdentityResult> AddUserNoPass(AppUser user);
        public Task<List<Claim>> GetUserClaims(AppUser user);
        public Task<IList<string>> GetUserRoles(AppUser user);
        public Task<bool> RefreshTokenIsExist(string refreshToken);
        public Task<int> CountUserNameStartWith(string name);
        public Task<bool> CheckUserPassword(AppUser user, string  password);
        public Task<IdentityResult> UpdateUser(AppUser user);
    }
}
