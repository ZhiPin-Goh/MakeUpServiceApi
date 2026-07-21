using MakeUpServiceApi.DTO.AdminDTO;
using MakeUpServiceApi.DTO.AuthDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scrypt;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/auth")]
    [Tags("Admin Authentication")]
    public class AuthAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITokenService _tokenService;
        private static ScryptEncoder _encoder = new ScryptEncoder();
        public AuthAdminController(AppDbContext db, ITokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }
        [EnableRateLimiting("StrictPolicy")]
        [HttpPost("login")]
        public async Task<IActionResult> AuthLogin([FromBody] LoginDto dto)
        {
            try
            {
                var admin = await _db.Admins.FirstOrDefaultAsync(a => a.UserName == dto.UserName);
                if (admin == null || !_encoder.Compare(dto.Password, admin.PasswordHash))
                {
                    return Unauthorized(new
                    {
                        success = false,
                        error = "Invalid username or password",
                        message = $"Login failed for username: {dto.UserName}"
                    });
                }
                var tokens = await _tokenService.GenerateTokensAsync(adminID: admin.AdminID);
                return Ok(new
                {
                    success = true,
                    message = "Login successful",
                    accessToken = tokens.AccessToken,
                    refreshToken = tokens.RefreshToken
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    success = false,
                    message = "An error occurred during authentication",
                    error = ex.Message
                });
            }
        }
        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenDto dto)
        {
            try
            {
                var tokens = await _tokenService.RefreshTokenGenerateAsync(accessToken: dto.AccessToken, refreshToken: dto.RefreshToken);
                return Ok(new
                {
                    message = "Token refreshed successfully",
                    accessToken = tokens.AccessToken,
                    refreshToken = tokens.RefreshToken
                });
            }
            catch (UnauthorizedAccessException ex)
            {
                return Unauthorized(new
                {
                    message = "Token refresh failed",
                    error = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    message = "An error occurred during token refresh",
                    error = ex.Message
                });
            }
        }
        [Authorize]
        [HttpPost("revoke-token")]
        public async Task<IActionResult> Logout([FromBody] RefreshTokenDto dto)
        {
            var tokens = await _tokenService.RevokeTokenAsync(accessToken: dto.AccessToken, refreshToken: dto.RefreshToken);
            if (tokens)
            {
                return Ok(new
                {
                    message = "Logout successful"
                });
            }
            else
            {
                return BadRequest(new
                {
                    error = "Invalid token or logout failed",
                    message = "Please ensure the provided tokens are correct and try again."
                });
            }
        }
    }
}
