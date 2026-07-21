using MakeUpServiceApi.DTO.AreaDTO;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [Authorize]
    [Route("api/admin/areas")]
    [ApiController]
    [Tags("Admin Areas Management")]
    public class AreaAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AreaAdminController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("areas")]
        public async Task<IActionResult> GetAreas()
        {
            var areas = await _db.ServiceAreas.Select(x => new
            {
                x.AreaID,
                x.Name,
                x.BasePrice,
                Status = x.IsActive ? "Active" : "Inactive"
            }).ToListAsync();
            return Ok(areas);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateArea(
            [FromHeader(Name ="X-Idempotency-Key")] string idempotencyKey,
            [FromBody] CreateAreaDto dto)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new
                {
                    error = "Idempotency key is required",
                    message = "Please provide a unique idempotency key in the request header.",
                    statusCode = 400
                });
            }
            var existingIdempotency = await _db.Idempotencies.FirstOrDefaultAsync(x => x.IdempotencyKey == idempotencyKey);
            if (existingIdempotency != null)
            {
                if (existingIdempotency.Status == "Completed")
                {
                    return Content(existingIdempotency.ResponseBody, "application/json");
                }
                if (existingIdempotency.Status == "Started")
                {
                    return Conflict(new
                    {
                        error = "Request is already being processed",
                        message = "Please wait for the previous request to complete.",
                        statusCode = 409
                    });
                }
            }
            var rawHash = "AreaCreate:" + JsonSerializer.Serialize(dto);
            var idempotency = new Idempotency
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = rawHash.Length > 256 ? rawHash.Substring(0, 256) : rawHash,
                Status = "Started",
                CreatedAt = DateTime.Now,
                ResponseBody = ""
            };
            _db.Idempotencies.Add(idempotency);
            await _db.SaveChangesAsync();

            try
            {

                var existingArea = await _db.ServiceAreas.AnyAsync(x => x.Name.ToLower() == dto.Name.ToLower());
                if (existingArea)
                {
                    return BadRequest(new
                    {
                        error = "An area with the same name already exists",
                        message = "Please choose a different name for the area."
                    });
                }
                if (dto.Price < 0)
                {
                    return BadRequest(new
                    {
                        error = "Invalid price",
                        message = "Price must be a non-negative value."
                    });
                }
                var newArea = new ServiceArea
                {
                    Name = dto.Name,
                    BasePrice = dto.Price,
                    IsActive = true
                };
                _db.ServiceAreas.Add(newArea);

                var responseObj = new
                {
                    message = "Area created successfully",
                    areaID = newArea.AreaID,
                    text = $"Area '{newArea.Name}' created successfully with ID {newArea.AreaID}."
                };
                idempotency.Status = "Completed";
                idempotency.ResponseBody = JsonSerializer.Serialize(responseObj);
                idempotency.ResponseCode = 200;
                await _db.SaveChangesAsync();
                return Ok(responseObj);
            }
            catch (Exception ex)
            {
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = $"An error occurred while processing your request: {ex.Message}"
                });
                idempotency.ResponseCode = 500;
                await _db.SaveChangesAsync();
                throw;
            }
        }
        [HttpPost("update")]
        public async Task<IActionResult> UpdateArea([FromBody] UpdateAreaDto dto)
        {
            var area = await _db.ServiceAreas.FindAsync(dto.AreaID);
            if (area == null)
            {
                return NotFound(new
                {
                    error = "Area not found",
                    message = $"No area found with ID {dto.AreaID}."
                });
            }
            if(!string.IsNullOrEmpty(dto.Name))
            {
                var existingArea = await _db.ServiceAreas.AnyAsync(x => x.Name.ToLower() == dto.Name.ToLower() && x.AreaID != dto.AreaID);
                if (existingArea)
                {
                    return BadRequest(new
                    {
                        error = "An area with the same name already exists",
                        message = "Please choose a different name for the area."
                    });
                }
               area.Name = dto.Name;
            }
            if(dto.BaseFee.HasValue && dto.BaseFee.Value < 0)
            {
                return BadRequest(new
                {
                    error = "Invalid price",
                    message = "Price must be a non-negative value."
                });
            }
            else if (dto.BaseFee.HasValue)
            {
                area.BasePrice = dto.BaseFee.Value;
            }
            _db.Entry(area).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Area updated successfully",
                area = new
                {
                    area.AreaID,
                    area.Name,
                    area.BasePrice,
                    Status = area.IsActive ? "Active" : "Inactive"
                }
            });
        }
        [HttpPost("toggle-status/{areaID}")]
        public async Task<IActionResult> ToggleAreaStatus(int areaID)
        {
            var area = await _db.ServiceAreas.FindAsync(areaID);
            if (area == null)
            {
                return NotFound(new
                {
                    error = "Area not found",
                    message = $"No area found with ID {areaID}."
                });
            }
            area.IsActive = !area.IsActive;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Area status updated successfully",
                status = area.IsActive ? "Active" : "Inactive"
            });
        }
    }
}
