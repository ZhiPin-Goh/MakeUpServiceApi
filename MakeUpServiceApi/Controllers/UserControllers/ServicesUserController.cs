using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/services")]
    [Tags("User Services")]
    public class ServicesUserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ServicesUserController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("services")]
        public async Task<IActionResult> GetServices()
        {
            var services = await _db.Services.AsNoTracking()
                .Where(x => x.Status == "Active")
                .Select(x => new
                {
                    x.Name,
                    x.ImageUrl,
                    x.Price,
                })
                .ToListAsync();
            return Ok(services);
        }
        [HttpGet("randomservices")]
        public async Task<IActionResult> GetRandomServices()
        {
            var services = await _db.Services.AsNoTracking()
                .Where(x => x.Status == "Active")
                .OrderBy(x => Guid.NewGuid())
                .Select(x => new
                {
                    x.Name,
                    x.ImageUrl,
                    x.Price,
                })
                .Take(5)
                .ToListAsync();
            return Ok(services);
        }
        [HttpGet("service/{id}")]
        public async Task<IActionResult> GetServicesByID(int id)
        {
            var services = await _db.Services.AsNoTracking()
                .Where(x => x.Status == "Active" && x.ServiceID == id)
                .Select(x => new
                {
                    x.Name,
                    x.Description,
                    x.Price,
                    x.EstimatedDurationMinutes,
                    x.ImageUrl
                }).FirstOrDefaultAsync();
            if(services == null)
            {
                return NotFound(new
                {
                    error = "Service Not Found",
                    message = $"ServicesID {id} not found"
                });
            }
            return Ok(services);
        }
    }
}
