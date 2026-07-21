using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/banners")]
    [Tags("User Banner")]
    public class BannerUserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public BannerUserController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("banners")]   
        public async Task<IActionResult> GetAllBanners()
        {
            var banners = await _db.Banners
                .Where(b => b.IsActive)
                .OrderBy(b => b.SortOrder)
                .Select(b => new
                {
                    b.Title,
                    b.ImageUrl,
                    b.TargetUrl,
                    b.SortOrder
                })
                .ToListAsync();
            return Ok(banners);
        }
    }
}
