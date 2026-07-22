using MakeUpServiceApi.DTO.SettingDTO;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/[controller]")]
    [Authorize]
    [Tags("Admin Settings Management")]
    public class SettingsAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IMemoryCache _cache;
        public SettingsAdminController(AppDbContext db, IMemoryCache cache)
        {
            _db = db;
            _cache = cache;
        }
        [HttpGet("get-settings")]
        public async Task<IActionResult> GetSettings()
        {
            var settings = await _db.SystemSettings.AsNoTracking().ToListAsync();
            return Ok(settings);
        }
        [HttpGet("get-setting/{key}")]
        public async Task<IActionResult> GetSettingByKey(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return BadRequest(new
                {
                    error = "Invalid Key",
                    message = "The key parameter cannot be null or empty."
                });
            }
            var setting = await _db.SystemSettings.AsNoTracking().FirstOrDefaultAsync(s => s.Key.ToLower() == key.ToLower());
            if (setting == null)
            {
                return NotFound(new
                {
                    error = "Setting Not Found",
                    message = $"No setting found with the key '{key}'."
                });
            }
            return Ok(setting);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateSetting([FromBody] SettingDto dto)
        {
            var existingSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == dto.Key);
            if (existingSetting != null)
            {
                return BadRequest(new
                {
                    error = "Setting Already Exists",
                    message = $"A setting with the key '{dto.Key}' already exists."
                });
            }
            var newSetting = new SystemSettings
            {
                Key = dto.Key,
                Value = dto.Value,
                Description = dto.Description ?? "No description"
            };

            _db.SystemSettings.Add(newSetting);
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Setting Created Successfully",
                setting = newSetting
            });
        }
        [HttpPost("update")]
        public async Task<IActionResult> UpdateSetting([FromBody] SettingDto dto)
        {
            var existingSetting = await _db.SystemSettings.FirstOrDefaultAsync(s => s.Key == dto.Key);
            if (existingSetting == null)
            {
                return NotFound(new
                {
                    error = "Setting Not Found",
                    message = $"No setting found with the key '{dto.Key}'."
                });
            }
            if (!string.IsNullOrEmpty(dto.Value))
            {
                existingSetting.Value = dto.Value;
            }
            if (!string.IsNullOrEmpty(dto.Description))
            {
                existingSetting.Description = dto.Description;
            }
            await _db.SaveChangesAsync();
            _cache.Remove(dto.Key);
            return Ok(new
            {
                message = "Setting Updated Successfully",
                setting = existingSetting
            });
        }
    }
}
