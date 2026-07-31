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

                    case "getarea":
                        var areas = await _db.ServiceAreas.AsNoTracking()
                            .Where(x => x.IsActive)
                            .Select(s => new { s.AreaID, s.Name, s.BasePrice })
                            .ToListAsync();
                        return JsonSerializer.Serialize(new
                        {
                            tool = "GetArea",
                            result = areas
                        });

                    case "checkscheduleblocker":
                        if (!call.Args.TryGetValue("targetMonth", out var targetMonthObj) || !DateTime.TryParse(targetMonthObj.ToString(), out DateTime targetMonth))
                        {
                            return JsonSerializer.Serialize(new
                            {
                                tool = "CheckScheduleBlocker",
                                error = "Invalid or missing 'targetMonth' parameter. Please use a valid date format like YYYY-MM-DD."
                            });
                        }
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
                        if (!call.Args.TryGetValue("date", out var dateObj) || !DateTime.TryParse(dateObj.ToString(), out DateTime checkDate))
                        {
                            return JsonSerializer.Serialize(new
                            {
                                tool = "CheckBookingSchedule",
                                error = "Invalid or missing 'date' parameter. Please use a valid date format like YYYY-MM-DD."
                            });
                        }
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
                        if (!call.Args.TryGetValue("serviceID", out var sIdObj) || !int.TryParse(sIdObj.ToString(), out int reqServiceID))
                        {
                            return JsonSerializer.Serialize(new { tool = "CalculatePriceAndTravelFee", error = "Invalid or missing 'serviceID'." });
                        }
                        if (!call.Args.TryGetValue("address", out var addrObj) || string.IsNullOrWhiteSpace(addrObj.ToString()))
                        {
                            return JsonSerializer.Serialize(new { tool = "CalculatePriceAndTravelFee", error = "Invalid or missing 'address'. Please ask the user for a full valid address." });
                        }
                        if (!call.Args.TryGetValue("pax", out var cPaxObj) || !int.TryParse(cPaxObj.ToString(), out int cPax)) cPax = 1;
                        
                        int? parsedAreaID = null;
                        if (call.Args.TryGetValue("areaID", out var aIdObj) && int.TryParse(aIdObj.ToString(), out int aId))
                        {
                            parsedAreaID = aId;
                        }

                        string address = addrObj.ToString();

                        var sercive = await _db.Services
                            .FirstOrDefaultAsync(s => s.ServiceID == reqServiceID && s.Status == "Active");
                        if (sercive == null)
                            return JsonSerializer.Serialize(new
                            {
                                tool = "CalculatePriceAndTravelFee",
                                error = "Service not found or inactive."
                            });

                        var travelFee = await _travelFeeService.CalculateFeeAsync(parsedAreaID, address);
                        decimal basePrice = Convert.ToDecimal(sercive.Price * cPax);
                        decimal rawTotalPrice = basePrice + travelFee.TotalFee;
                        decimal totalPrice = Math.Round(rawTotalPrice, 0, MidpointRounding.AwayFromZero);

                        return JsonSerializer.Serialize(new
                        {
                            tool = "CalculatePriceAndTravelFee",
                            result = new
                            {
                                ServiceID = sercive.ServiceID,
                                ServiceName = sercive.Name,
                                ServicePrice = sercive.Price,
                                Pax = cPax,
                                TravelFee = travelFee.TotalFee,
                                TotalPrice = totalPrice
                            }
                        });

                    // Method post
                    case "createbooking":
                        if (!call.Args.TryGetValue("appointmentDate", out var appDateObj) || !DateTime.TryParse(appDateObj.ToString(), out DateTime appDate))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid or missing 'appointmentDate'. Please use format YYYY-MM-DD HH:mm:ss." });
                        }
                        if (!call.Args.TryGetValue("serviceID", out var bookSIdObj) || !int.TryParse(bookSIdObj.ToString(), out int bookServiceID))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid or missing 'serviceID'." });
                        }
                        if (!call.Args.TryGetValue("email", out var emailObj) || string.IsNullOrWhiteSpace(emailObj.ToString()))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid or missing 'email'. Please ask the user for their email address." });
                        }
                        if (!call.Args.TryGetValue("pax", out var bPaxObj) || !int.TryParse(bPaxObj.ToString(), out int bPax)) bPax = 1;
                        
                        int? bookAreaID = null;
                        if (call.Args.TryGetValue("areaID", out var bAreaIdObj) && int.TryParse(bAreaIdObj.ToString(), out int bAId))
                        {
                            bookAreaID = bAId;
                        }

                        var serciveToBook = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == bookServiceID && s.Status == "Active");
                        if (serciveToBook == null) return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Service not found or inactive." });

                        string locAddress = call.Args.GetValueOrDefault("locationAddress")?.ToString() ?? "";
                        var travelFeeToBook = await _travelFeeService.CalculateFeeAsync(bookAreaID, locAddress);
                        decimal basePriceToBook = Convert.ToDecimal(serciveToBook.Price * bPax);
                        decimal totalPriceToBook = Math.Round(basePriceToBook + travelFeeToBook.TotalFee, 0, MidpointRounding.AwayFromZero);

                        var newBooking = new Booking
                        {
                            Name = call.Args.GetValueOrDefault("name")?.ToString() ?? "",
                            Email = emailObj.ToString(),
                            PhoneNumber = call.Args.GetValueOrDefault("phoneNumber")?.ToString() ?? "",
                            AppointmentDate = appDate.Date,
                            AppointmentTime = appDate.TimeOfDay,
                            LocationAddress = locAddress,
                            ServiceID = bookServiceID,
                            AreaID = bookAreaID,
                            Pax = bPax,
                            DistanceKm = travelFeeToBook.DistanceKm,
                            TravelFee = travelFeeToBook.TotalFee,
                            TotalPrice = totalPriceToBook,
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
