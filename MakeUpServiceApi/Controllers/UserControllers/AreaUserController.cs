using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/area")]
    [Tags("User Areas")]
    public class AreaUserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public AreaUserController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("areas")]
        public async Task<IActionResult> GetAreas()
        {
            var areas = await _db.ServiceAreas.AsNoTracking()
                .Where(x => x.IsActive)
                .Select(x => new
                {
                    x.Name,
                    x.AreaID,
                    x.BasePrice
                })
                .ToListAsync();
            return Ok(areas);
        }
        [HttpGet("areas/{id}")]
        public async Task<IActionResult> GetAreaByID(int id)
        {
            var area = await _db.ServiceAreas.AsNoTracking().FirstOrDefaultAsync(a => a.AreaID == id && a.IsActive == true);
            if (area == null)
            {
                return NotFound(new
                {
                    error = "Area not found",
                    message = $"No area found with ID {id}"
                });
            }
            return Ok(area);
        }
    }
}
