using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace MakeUpServiceApi.Interface_Services
{
    public class TokenService : ITokenService
    {
        private readonly IConfiguration _config;
        private readonly ILogger<TokenService> _logger;
        private readonly AppDbContext _db;

        public TokenService(IConfiguration config, ILogger<TokenService> logger, AppDbContext db)
        {
            _config = config;
            _logger = logger;
            _db = db;
        }
        public async Task<(string AccessToken, string RefreshToken)> GenerateTokensAsync(int userID)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, userID.ToString())

                }),
                Expires = DateTime.Now.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"]
            };

            var rawToken = tokenHandler.CreateToken(tokenDescriptor);
            var tokenString = tokenHandler.WriteToken(rawToken);
            var refreshToken = Guid.NewGuid().ToString();

            var token = new TokenActivity
            {
                CreatedAt = DateTime.Now,
                AccessToken = tokenString,
                RefreshToken = refreshToken,
                IsRevoked = false,
                ExpiryDate = DateTime.Now.AddDays(7),
                AdminID = userID
            };
            _db.TokenActivities.Add(token);
            await _db.SaveChangesAsync();
            _logger.LogInformation($"Token generated for user {userID} at {DateTime.Now}");
            return (tokenString, refreshToken);
        }
        public async Task<(string AccessToken, string RefreshToken)> RefreshTokenGenerateAsync(string accessToken, string refreshToken)
        {
            var activity = await _db.TokenActivities.FirstOrDefaultAsync(t => t.AccessToken == accessToken && t.RefreshToken == refreshToken);
            if (activity == null)
            {
                throw new Exception("Invalid token pair.");
            }
            if (activity.CreatedAt.AddDays(7) < DateTime.Now || activity.IsRevoked)
            {
                activity.IsRevoked = true;
                throw new Exception("Token expired or revoked.");
            }

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["Jwt:Key"]);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                    new Claim(ClaimTypes.NameIdentifier, activity.AdminID.ToString()),
                }),
                Expires = DateTime.Now.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),
                Issuer = _config["Jwt:Issuer"],
                Audience = _config["Jwt:Audience"]
            };
            var newAccessToken = tokenHandler.WriteToken(tokenHandler.CreateToken(tokenDescriptor));
            var newRefreshToken = Guid.NewGuid().ToString();

            activity.AccessToken = newAccessToken;
            activity.RefreshToken = newRefreshToken;
            
            await _db.SaveChangesAsync();
            _logger.LogInformation($"Token refreshed for user {activity.AdminID} at {DateTime.Now}");
            return (newAccessToken, newRefreshToken);
        }
        public async Task<bool> RevokeTokenAsync(string accesstoken, string refreshToken)
        {
            var activity = await _db.TokenActivities.FirstOrDefaultAsync(x => x.AccessToken == accesstoken && x.RefreshToken == refreshToken);
            if(activity != null)
            {
                string lastChars = accesstoken.Length > 10 ? accesstoken.Substring(accesstoken.Length - 10) : "***";

                activity.AccessToken = $"REVOKED_AT_{DateTime.Now:yyyyMMddHHmmss}_{lastChars}";
                activity.RefreshToken = $"REVOKED_AT_{DateTime.Now:yyyyMMddHHmmss}_{lastChars}";
                activity.IsRevoked = true;

                await _db.SaveChangesAsync();
                return true;
            }
            return false;
        }

    }
}
