using ChatApp_Backend.DTO.Account;
using ChatApp_Backend.Models;

namespace ChatApp_Backend.Services
{
    public interface ITokenService
    {
        public Task<string> GenerateAccessToken(AppUser user);
        public DateTime GetRefreshTokenExp();
        public Task<RefreshTokenHash> GetRefreshToken();
        public string HashRefreshToken(string refreshToken);
        public GoogleUrlBuilderDTO BuildGoogleAuthUrl();
        public Task<GoogleTokenResponseDTO?> GetGoogleToken(string code);
    }
}
