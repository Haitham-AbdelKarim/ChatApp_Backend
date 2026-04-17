using ChatApp_Backend.DTO.Account;
using ChatApp_Backend.Models;
using ChatApp_Backend.Repository;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ChatApp_Backend.Services
{
    public class TokenService : ITokenService
    {
        private readonly string issuer;
        private readonly string audience;
        private readonly string key;
        private readonly HttpClient httpClient;

        private readonly IUserRepository userRepository;
        private readonly IHttpClientFactory httpClientFactory;

        private int expirationInDays = 1;

        private GoogleUrlBuilderDTO googleUrlBuilder;

        public TokenService(IConfiguration configuration, UserManager<AppUser> manager, IUserRepository userRepository, IHttpClientFactory httpClientFactory)
        {
            issuer = configuration["JwtSetting:Issuer"]?? string.Empty;
            key = configuration["JwtSetting:Key"] ?? string.Empty;
            audience = configuration["JwtSetting:Audience"] ?? string.Empty;
            this.userRepository = userRepository;
            int.TryParse(configuration["JwtSetting:ExpirationInDays"]??"1",out expirationInDays);

            googleUrlBuilder = new GoogleUrlBuilderDTO
            {
                ClientID = configuration["GoogleAuth:client_id"] ?? string.Empty,
                RedirectUri = configuration["GoogleAuth:redirect_uris"] ?? string.Empty,
                ClientSecret = configuration["GoogleAuth:client_secret"] ?? string.Empty,
                AuthUri = configuration["GoogleAuth:auth_uri"] ?? string.Empty,
                TokenUri = configuration["GoogleAuth:token_uri"] ?? string.Empty
            };

            this.httpClient = httpClientFactory.CreateClient();

        }
        public async Task<string> GenerateAccessToken(AppUser user)
        {

            SigningCredentials credentials = CreateSigningCredentials();
            var token = new JwtSecurityToken(
                issuer : this.issuer,
                audience : this.audience,
                claims : await userRepository.GetUserClaims(user),
                expires : DateTime.Now.AddHours(1),
                signingCredentials : credentials
            );
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        public SigningCredentials CreateSigningCredentials()
        {
            SymmetricSecurityKey securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(this.key));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
            return credentials;
        }

        public async Task<RefreshTokenHash> GetRefreshToken()
        {
            RefreshTokenHash refreshTokenHash = new RefreshTokenHash();
            do
            {
                refreshTokenHash.RefreshToken = RefreshToken();
                refreshTokenHash.HashedRefreshToken = HashRefreshToken(refreshTokenHash.RefreshToken);
            }
            while (await userRepository.RefreshTokenIsExist(refreshTokenHash.HashedRefreshToken));
            return refreshTokenHash;
        }

        public string RefreshToken()
        {
            var randomNumber = new byte[64];
            using var randomGen = RandomNumberGenerator.Create();
            randomGen.GetBytes(randomNumber);
            string refreshToken = Convert.ToBase64String(randomNumber);
            return refreshToken;
        }

        public string HashRefreshToken(string refreshToken)
        {
            using var sha256 = SHA256.Create();
            var refreshTokenHash = sha256.ComputeHash(Encoding.UTF8.GetBytes(refreshToken));
            return Convert.ToBase64String(refreshTokenHash);
        }

        public DateTime GetRefreshTokenExp()
        {
            return DateTime.Now.AddDays(expirationInDays);
        }

        public GoogleUrlBuilderDTO BuildGoogleAuthUrl()
        {
            return googleUrlBuilder;
        }
        public async Task<GoogleTokenResponseDTO?> GetGoogleToken(string code)
        {
            GoogleUrlBuilderDTO googleUrlBuilder = BuildGoogleAuthUrl();
            var tokenRequest = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = googleUrlBuilder.ClientID,
                ["client_secret"] = googleUrlBuilder.ClientSecret,
                ["redirect_uri"] = googleUrlBuilder.RedirectUri,
                ["grant_type"] = "authorization_code"
            });
            var response = await httpClient.PostAsync(
                googleUrlBuilder.TokenUri, tokenRequest);
            var json = await response.Content.ReadAsStringAsync();
            return JsonSerializer.Deserialize<GoogleTokenResponseDTO>(json);
            
        }
    }
}
