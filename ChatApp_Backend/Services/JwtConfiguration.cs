using Microsoft.IdentityModel.Tokens;
using System.Text;

namespace ChatApp_Backend.Services
{
    public static class JwtConfiguration
    {
        public static TokenValidationParameters Create(IConfiguration configuration)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = configuration["JwtSetting:Issuer"],
                ValidAudience = configuration["JwtSetting:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["JwtSetting:Key"] ?? string.Empty))
            };
        }
    }
}
