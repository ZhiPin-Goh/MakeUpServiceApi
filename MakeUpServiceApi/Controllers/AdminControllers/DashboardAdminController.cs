using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/dashboard")]
    [Tags("Admin Dashboard Management")]
    [Authorize]
    public class DashboardAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public DashboardAdminController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("stats")]
        public async Task<IActionResult> GetDashboardStats()
        {
            var today = DateTime.Today;
            var firstDayOfMonth = new DateTime(today.Year, today.Month, 1);

            var pendingCount = await _db.Bookings.AsNoTracking()
                .CountAsync(b => b.Status == BookingStatus.Pending);

            var todayCount = await _db.Bookings.AsNoTracking()
                .CountAsync(b => b.AppointmentDate.Date == today && b.Status == BookingStatus.Approved);

            var monthRevenus = await _db.Bookings.AsNoTracking()
                .Where(b => b.AppointmentDate >= firstDayOfMonth && b.Status == BookingStatus.Completed)
                .SumAsync(b => (decimal?)b.TotalPrice) ?? 0;

            var unreadFeedbackCount = await _db.Feedbacks.AsNoTracking()
                .CountAsync(f => !f.IsResolved);

            var upComing = await _db.Bookings.AsNoTracking()
                .Include(b => b.Service)
                .Where(b => b.AppointmentDate >= today && b.Status == BookingStatus.Approved)      
                .OrderBy(b => b.AppointmentDate)
                .Take(10)
                .Select(b => new
                {
                    b.BookingID,
                    b.AppointmentDate,
                    b.LocationAddress,
                    ServiceName = b.Service.Name,
                    b.Status
                }).ToListAsync();

            var topServicesType = await _db.Bookings.AsNoTracking()
                .Include(b => b.Service)
                .GroupBy(b => b.Service.Name)
                .Select(g => new
                {
                    Type = g.Key,
                    Count = g.Count()
                })
                .OrderByDescending(g => g.Count)
                .Take(5)
                .ToListAsync();

            var reponseObj = new
            {
                pendingCount = pendingCount,
                todayCount = todayCount,                
                unreadFeedbackCount = unreadFeedbackCount,
                monthRevenus = monthRevenus,
                upComing = upComing,
                topServicesType = topServicesType,
            };
            return Ok(reponseObj);
        }
    }
}
