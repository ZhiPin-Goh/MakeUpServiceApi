using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/calendar")]
    [Tags("Admin Calendar Management")]
    [Authorize]
    public class CalendarAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public CalendarAdminController(AppDbContext db)
        {
            this._db = db;
        }
        public class CalendarEventDto
        {
            public string ID { get; set; }
            public string Title { get; set; }
            public string Start { get; set; }
            public string End { get; set; }
            public string Color { get; set; }
            public bool AllDay { get; set; }
            public string EventType { get; set; }
        }

        public class SelectDateDto
        {
            public DateTime Start { get; set; }
            public DateTime End { get; set; }
        }

        [HttpGet("get-calendar")]
        public async Task<IActionResult> GetCalendarEvent([FromQuery] SelectDateDto request)
        {
            var eventList = new List<CalendarEventDto>();

            var bookings = await _db.Bookings
             .Include(b => b.Service)
             .Where(b => b.AppointmentDate >= request.Start && b.AppointmentDate <= request.End &&
                         (b.Status == BookingStatus.Approved || b.Status == BookingStatus.Completed))
             .ToListAsync();

            foreach (var b in bookings)
            {
                DateTime startDateTime = b.AppointmentDate.Date.Add(b.AppointmentTime);
                int durationMinutes = (b.Service.EstimatedDurationMinutes ?? 60) * b.Pax;
                DateTime endDateTime = startDateTime.AddMinutes(durationMinutes);

                eventList.Add(new CalendarEventDto
                {
                    ID = $"booking_{b.BookingID}",
                    Title = $"{startDateTime:HH:mm} {b.Name} - {b.Service.Name}",
                    Start = startDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    End = endDateTime.ToString("yyyy-MM-ddTHH:mm:ss"),
                    Color = "#d4a373",
                    AllDay = false,
                    EventType = "booking"
                });
            }

            var scheduleBlockers = await _db.ScheduleBlockers
                .Where(sb => sb.StartDate <= request.End && sb.StartDate >= request.Start)
                .ToListAsync();

            foreach (var sb in scheduleBlockers)
            {
                eventList.Add(new CalendarEventDto
                {
                    ID = $"blocker_{sb.ScheduleBlockerID}",
                    Title = $"🚫 {sb.Reason}",
                    Start = sb.StartDate.ToString("yyyy-MM-dd"),
                    Color = "#ff4d4d",
                    AllDay = true,
                    EventType = "blocker"
                });
            }
            return Ok(eventList);
        }
    }
}
