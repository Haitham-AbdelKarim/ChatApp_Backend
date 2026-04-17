using ChatApp_Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace ChatApp_Backend.Repository
{
    public class UserRepository : IUserRepository
    {
        private readonly UserManager<AppUser> userManager;

        public UserRepository(UserManager<AppUser> userManager)
        {
            this.userManager = userManager;
        }
        public async Task<AppUser?> GetUserByEmail(string email)
        {
            AppUser? user = await userManager.Users.Include(u => u.Groups).FirstOrDefaultAsync(u => u.Email == email);
            //AppUser? user = await userManager.FindByEmailAsync(email);
            return user;
        }

        public async Task<IdentityResult> AddUser(AppUser user, string passWord)
        {
            var result = await userManager.CreateAsync(user, passWord);
            return result;
        }

        public async Task<IdentityResult> AddUserNoPass(AppUser user)
        {
            var result = await userManager.CreateAsync(user);
            return result;
        }

        public async Task<List<Claim>> GetUserClaims(AppUser user)
        {
            List<Claim> claims = new List<Claim>();
            claims.Add(new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()));
            claims.Add(new Claim(ClaimTypes.Email, user.Email ?? string.Empty));
            claims.Add(new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()));
            claims.Add(new Claim("FirstName", user.FirstName));
            claims.Add(new Claim("LastName", user.LastName));
            //claims.Add(new Claim("Gender", user.Gender));
            var roles = await GetUserRoles(user);
            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            return claims;
        }

        public async Task<IList<string>> GetUserRoles(AppUser user)
        {
            return await userManager.GetRolesAsync(user);
        }

        public async Task<bool> RefreshTokenIsExist(string refreshToken)
        {
            return await userManager.Users.AnyAsync(u => u.RefreshToken == refreshToken);
        }

        public async Task<int> CountUserNameStartWith(string name)
        {
            return await userManager.Users.CountAsync(u => u.UserName != null && u.UserName.StartsWith(name));
        }

        public async Task<bool> CheckUserPassword(AppUser user, string password)
        {
            return await userManager.CheckPasswordAsync(user, password);
        }

        public async Task<IdentityResult> UpdateUser(AppUser user)
        {
            return await userManager.UpdateAsync(user);
        }

        public async Task<AppUser?> GetUserByRefreshToken(string refreshToken)
        {
            return await userManager.Users.FirstOrDefaultAsync(user => user.RefreshToken == refreshToken);
        }
    }
}
