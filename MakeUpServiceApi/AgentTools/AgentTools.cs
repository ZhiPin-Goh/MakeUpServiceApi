using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

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
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<AgentTools> _logger;
        public AgentTools(AppDbContext db, ITravelFeeService travelFeeService, IServiceScopeFactory serviceScope, ILogger<AgentTools> logger)
        {
            _db = db;
            _travelFeeService = travelFeeService;
            _scopeFactory = serviceScope;
            _logger = logger;
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
                            .Select(x => new { x.StartDate, x.Reason })
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
                            .Include(x => x.Service)
                            .Where(x => x.AppointmentDate.Date == checkDate.Date)
                            .Where(x => x.Status == BookingStatus.Pending || x.Status == BookingStatus.Approved)
                            .Select(x => new { 
                                x.BookingID, 
                                x.AppointmentDate, 
                                x.AppointmentTime, 
                                EstimatedDurationMinutes = x.Service != null ? x.Service.EstimatedDurationMinutes : 60,
                                x.Status 
                            })
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

                        if (parsedAreaID.HasValue)
                        {
                            var existingAreaCalc = await _db.ServiceAreas.FirstOrDefaultAsync(a => a.AreaID == parsedAreaID.Value && a.IsActive);
                            if (existingAreaCalc != null && !address.Contains(existingAreaCalc.Name, StringComparison.OrdinalIgnoreCase))
                            {
                                return JsonSerializer.Serialize(new { tool = "CalculatePriceAndTravelFee", error = $"Location Mismatch: You selected area '{existingAreaCalc.Name}', but the address does not match this area. Please ask the user for the correct service area or address." });
                            }
                        }

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

                        _logger.LogInformation("Calculated total price: {TotalPrice} (Base: {BasePrice}, Travel Fee: {TravelFee})", totalPrice, basePrice, travelFee.TotalFee);

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
                        if (bPax < 1) return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Number of passengers (pax) must be at least 1." });

                        string emailStr = emailObj.ToString();
                        string phoneStr = call.Args.GetValueOrDefault("phoneNumber")?.ToString() ?? "";
                        string phonePattern = @"^01[0-9]\d{7,8}$";
                        string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";

                        if (!Regex.IsMatch(phoneStr, phonePattern))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid phone number format. Tell the user it must be a valid Malaysian number (e.g. 01X-XXXXXXX)." });
                        }
                        if (!Regex.IsMatch(emailStr, emailPattern))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid email format. Tell the user to provide a valid email (e.g. example@domain.com)." });
                        }
                        if (appDate < DateTime.Now)
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Appointment date must be in the future." });
                        }
                        
                        var minimumBookingDate = _db.SystemSettings.Find("MinimumBookingDate");
                        var selectedBookingDate = minimumBookingDate != null ? DateTime.Now.AddDays(int.Parse(minimumBookingDate.Value)) : DateTime.Now.AddDays(2);
                        if (appDate < selectedBookingDate)
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = $"Appointment date must be at least {selectedBookingDate.ToString("yyyy-MM-dd")} or later." });
                        }
                        
                        var maximumBookingDate = _db.SystemSettings.Find("MaximumBookingDate");
                        var selectedMaximumBookingDate = maximumBookingDate != null ? DateTime.Now.AddDays(int.Parse(maximumBookingDate.Value)) : DateTime.Now.AddMonths(3);
                        if (appDate > selectedMaximumBookingDate)
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = $"Appointment date must be on or before {selectedMaximumBookingDate.ToString("yyyy-MM-dd")}." });
                        }
                        
                        if(appDate.TimeOfDay >= new TimeSpan(21, 0, 0) || appDate.TimeOfDay < new TimeSpan(3, 0, 0))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Appointment time must be between 02:59 and 21:00." });
                        }
                        
                        int? bookAreaID = null;
                        if (call.Args.TryGetValue("areaID", out var bAreaIdObj) && int.TryParse(bAreaIdObj.ToString(), out int bAId))
                        {
                            bookAreaID = bAId;
                        }
                        var existingArea = await _db.ServiceAreas.FirstOrDefaultAsync(a => a.AreaID == bookAreaID && a.IsActive == true);
                        if (bookAreaID.HasValue && existingArea == null)
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Invalid 'areaID'. The specified area does not exist or is inactive." });
                        }
                        
                        string locAddress = call.Args.GetValueOrDefault("locationAddress")?.ToString() ?? "";
                        if (existingArea != null && !locAddress.Contains(existingArea.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            return JsonSerializer.Serialize(new { tool = "CreateBooking", error = $"Location Mismatch: You selected area '{existingArea.Name}', but the address does not match this area. Please ask the user for the correct service area or address." });
                        }

                        var serciveToBook = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == bookServiceID && s.Status == "Active");
                        if (serciveToBook == null) return JsonSerializer.Serialize(new { tool = "CreateBooking", error = "Service not found or inactive." });

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
                        _ = Task.Run(async () =>
                        {
                            using var scope = _scopeFactory.CreateScope();
                            var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                            var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                            try
                            {
                                string emailSubject = "Booking Confirmation";
                                string body = EmailBody(name: newBooking.Name,
                                    phoneNumber: newBooking.PhoneNumber,
                                    appointmentDate: newBooking.AppointmentDate,
                                    appointmentTime: newBooking.AppointmentTime,
                                    service: serciveToBook.Name,
                                    location: newBooking.LocationAddress,
                                    totalPrice: Convert.ToDecimal(newBooking.TotalPrice),
                                    pax: newBooking.Pax);
                                await emailService.SendEmailAsync(email: newBooking.Email, subject: emailSubject, body: body);

                                await notificationService.SendNotificationAsync(relatedID: newBooking.BookingID,
                                    title: "New Booking Received",
                                    message: $"You have a new booking from {newBooking.Name} on {newBooking.AppointmentDate.ToString("dd MMM yyyy")} at {newBooking.AppointmentTime.ToString(@"hh\:mm")}.",
                                    type: "Booking");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Error occurred while sending email or notification.");
                            }
                        });
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
        private string EmailBody(string name, string phoneNumber, string location, DateTime appointmentDate, TimeSpan appointmentTime, decimal totalPrice, string service, int pax)
        {
            string emailBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""UTF-8"">
                </head>
                <body style=""margin: 0; padding: 0; background-color: #faf7f7; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
    
                    <!-- 外层背景 (浅裸色) -->
                    <div style=""width: 100%; background-color: #faf7f7; padding: 40px 0;"">
        
                        <!-- 卡片主体 (顶部玫瑰金品牌色) -->
                        <div style=""max-width: 550px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.03); overflow: hidden; border-top: 6px solid #d4a373;"">
            
                            <!-- 品牌标识 (优雅的衬线字体) -->
                            <div style=""padding: 35px 30px 10px 30px; text-align: center;"">
                                <h1 style=""margin: 0; color: #4a4a4a; font-family: 'Playfair Display', 'Georgia', serif; font-size: 26px; font-weight: normal; letter-spacing: 2px; text-transform: uppercase;"">
                                    Shirley <span style=""color: #d4a373;"">Makeup</span>
                                </h1>
                            </div>
            
                            <!-- 内容区 -->
                            <div style=""padding: 20px 35px 35px 35px; color: #4a4a4a; line-height: 1.6;"">
                
                                <!-- 确认标题 -->
                                <h2 style=""margin: 0 0 20px 0; font-family: 'Playfair Display', 'Georgia', serif; font-size: 22px; color: #333333; text-align: center;"">
                                    Booking Confirmed ✨
                                </h2>
                
                                <p style=""margin: 0 0 20px 0; font-size: 15px;"">
                                    Hi <strong>{name}</strong>,
                                </p>
                                <p style=""margin: 0 0 25px 0; font-size: 15px; color: #666666;"">
                                    Your appointment has been successfully booked. Here are the details of your upcoming session:
                                </p>
                
                                <!-- 预约信息详情卡片 (玫瑰金极细边框，浅粉底色) -->
                                <div style=""background-color: #fdfbfb; border: 1px solid #f3e8e0; border-radius: 8px; padding: 25px; margin: 25px 0;"">
                    
                                    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-size: 14px; color: #4a4a4a;"">
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; width: 40%;"">Name</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333;"">{name}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; width: 40%;"">Service</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333;"">{service}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Date</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{appointmentDate.ToString("yyyy-MM-dd")}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Time</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{appointmentTime}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Total Amount</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #d4a373; border-top: 1px solid #f3e8e0;"">RM {totalPrice}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Pax</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{pax}</td>
                                        </tr>
                                    </table>
                                </div>               
            
                        </div>
                    </div>
                </body>
                </html>";
            return emailBody;
        }
    }
}
