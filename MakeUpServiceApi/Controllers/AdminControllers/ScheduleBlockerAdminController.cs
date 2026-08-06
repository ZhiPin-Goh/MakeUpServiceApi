using MakeUpServiceApi.DTO.ScheduleBlockerDTO;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [Authorize]
    [ApiController]
    [Route("api/admin/schedule-blocker")]
    [Tags("Admin Schedule Blocker Management")]
    public class ScheduleBlockerAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public ScheduleBlockerAdminController(AppDbContext db)
        {
            _db = db;
        }
        [HttpGet("schedule-blockers")]
        public async Task<IActionResult> GetScheduleBlockers()
        {
            var scheduleBlockers = await _db.ScheduleBlockers.AsNoTracking()
                .OrderByDescending(sb => sb.StartDate)
                .ToListAsync();
            return Ok(scheduleBlockers);
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateSB([FromBody] CreateSBDto dto)
        {
           var exisitingBlocker = await _db.ScheduleBlockers.FirstOrDefaultAsync(x => x.StartDate.ToString() == dto.StartDate.ToString());
            if (exisitingBlocker != null)
            {
                return BadRequest(new
                {
                    error = "Invalid Request",
                    message = "Schedule Blocker already exists for the given start date."
                });
            }

            var existingBooking = await _db.Bookings
                  .Where(x => x.AppointmentDate.Date == dto.StartDate.Date)
                  .Where(x => x.Status == BookingStatus.Pending || x.Status == BookingStatus.Approved)
                  .ToListAsync();
            if (existingBooking.Any())
            {
                return BadRequest(new
                {
                    error = "Schedule Conflict",
                    message = $"Cannot create schedule blocker. There are existing bookingIDs {string.Join(", ", existingBooking.Select(b => b.BookingID))} on {dto.StartDate.Date}."
                });
            }

            string reason = string.IsNullOrEmpty(dto.Reason) ? "No message provided" : dto.Reason;
            var scheduleBlocker = new ScheduleBlocker
            {
                StartDate = dto.StartDate,
                Reason = reason,
            };
            _db.ScheduleBlockers.Add(scheduleBlocker);  
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Schedule Blocker created successfully.",
                scheduleBlocker = scheduleBlocker
            });
        }
        [HttpDelete("delete/{id}")]
        public async Task<IActionResult> DeleteSB(int id)
        {
            var sb = await _db.ScheduleBlockers.FindAsync(id);
            if (sb == null)
            {
                return NotFound(new
                {
                    error = "Schedule Blocker Not Found",
                    message = $"No schedule blocker found with ID {id}."
                });
            }
            else
            {
                // Pass days check
                var todayDays = DateTime.Now;
                if (sb.StartDate < todayDays)
                {
                    return BadRequest(new
                    {
                        error = "Invalid Request",
                        message = "Cannot delete a schedule blocker that has already started."
                    });
                }
                _db.ScheduleBlockers.Remove(sb);
                await _db.SaveChangesAsync();
                return Ok(new
                {
                    message = $"Schedule blocker with ID {id} has been deleted."
                });
            }
        }
    }
}
