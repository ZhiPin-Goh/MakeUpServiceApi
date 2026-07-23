using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;

namespace MakeUpServiceApi.AgentTools
{
    public class ToolCall
    {
        public string Tool { get; set; } = string.Empty;
        public Dictionary<string, object> Args { get; set; } = new();
    }
    public class AgentTools
    {
        private readonly AppDbContext _db;
        private readonly ITravelFeeService _travelFeeService;
        public AgentTools(AppDbContext db, ITravelFeeService travelFeeService)
        {
            _db = db;
            _travelFeeService = travelFeeService;
        }
        public async Task<string> ExecuteToolAsync(ToolCall call)
        {
            try
            {
                switch (call.Tool.ToLower())
                {
                    // Method get
                    case "getservice":
                        var service = await _db.Services.AsNoTracking()
                            .Where(x => x.Status == "Active")
                            .Select(s => new { s.ServiceID, s.Name, s.Price, s.Description })
                            .ToListAsync();
                        return JsonSerializer.Serialize(new
                        {
                            tool = "GetService",
                            result = service
                        });

                   
                    case "checkscheduleblocker":
                        var targetMonth = Convert.ToDateTime(call.Args["targetMonth"].ToString());
                        var blockers = await _db.ScheduleBlockers.AsNoTracking()
                            .Where(x => x.StartDate.Month == targetMonth.Month && x.StartDate.Year == targetMonth.Year)
                            .Select(x => new { x.StartDate, x.EndDate, x.Reason })
                            .ToListAsync();
                        return JsonSerializer.Serialize(new
                        {
                            tool = "CheckScheduleBlocker",
                            result = blockers
                        });

                    case "checkbookingschedule":
                        var checkDate = Convert.ToDateTime(call.Args["date"].ToString());
                        var existingBooking = await _db.Bookings.AsNoTracking()
                            .Where(x => x.AppointmentDate.Date == checkDate.Date)
                            .Where(x => x.Status == BookingStatus.Pending || x.Status == BookingStatus.Approved)
                            .Select(x => new { x.BookingID, x.AppointmentDate, x.Status })
                            .ToListAsync();
                        return JsonSerializer.Serialize(new
                        {
                            tool = "CheckBookingSchedule",
                            result = existingBooking
                        });

                    case "calculatepriceandtravelfee":
                        int reqServiceID = int.Parse(call.Args["serviceID"].ToString());
                        string address = call.Args["address"].ToString();

                        var sercive = await _db.Services
                            .FirstOrDefaultAsync(s => s.ServiceID == reqServiceID && s.Status == "Active");
                        if (sercive == null)
                            return JsonSerializer.Serialize(new
                            {
                                tool = "CalculatePriceAndTravelFee",
                                error = "Service not found or inactive."
                            });

                        var travelFee = await _travelFeeService.CalculateFeeAsync(null, address);
                        decimal totalPrice = Convert.ToDecimal(sercive.Price) + travelFee.TotalFee;

                        return JsonSerializer.Serialize(new
                        {
                            tool = "CalculatePriceAndTravelFee",
                            result = new
                            {
                                ServiceID = sercive.ServiceID,
                                ServiceName = sercive.Name,
                                ServicePrice = sercive.Price,
                                TravelFee = travelFee.TotalFee,
                                TotalPrice = totalPrice
                            }
                        });

                    // Method post
                    case "createbooking":
                        var newBooking = new Booking
                        {
                            Name = call.Args["name"].ToString(),
                            PhoneNumber = call.Args["phoneNumber"].ToString(),
                            AppointmentDate = Convert.ToDateTime(call.Args["appointmentDate"].ToString()),
                            LocationAddress = call.Args["locationAddress"].ToString(),
                            ServiceID = Convert.ToInt32(call.Args["serviceID"].ToString()),
                            Status = BookingStatus.Pending,
                            CreatedAt = DateTime.Now
                        };
                        _db.Bookings.Add(newBooking);
                        await _db.SaveChangesAsync();
                        return JsonSerializer.Serialize(new
                        {
                            tool = "CreateBooking",
                            status = "success",
                            bookingID = newBooking.BookingID
                        });

                    default:
                        return JsonSerializer.Serialize(new
                        {
                            tool = call.Tool,
                            error = "Tool not found."
                        });
                }
            }
            catch (Exception ex)
            {
                return JsonSerializer.Serialize(new
                {
                    tool = call.Tool,
                    error = "An error occurred while executing the tool",
                    message = ex.Message
                });
            }
        }
    }
}
