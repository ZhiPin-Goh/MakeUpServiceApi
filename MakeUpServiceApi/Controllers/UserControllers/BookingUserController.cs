using MakeUpServiceApi.DTO.BookingDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/booking")]
    [Tags("User Booking")]
    public class BookingUserController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ITravelFeeService _travelFeeService;
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<BookingUserController> _logger;
        public BookingUserController(AppDbContext db, ITravelFeeService travelFeeService, IConfiguration config, HttpClient httpClient, IServiceScopeFactory serviceScopeFactory, ILogger<BookingUserController> logger) 
        {
            _db = db;
            _travelFeeService = travelFeeService;
            _config = config;
            _httpClient = httpClient;
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }
        [HttpGet("completelocation")]
        public async Task<IActionResult> GetCompleteLocation([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 3)
            {
                return Ok(new List<string>());
            }
            try
            {
                string apiKey = _config["MapSettings:ApiKey"];
                string requestUrl = $"https://api.locationiq.com/v1/autocomplete.php?key={apiKey}&q={Uri.EscapeDataString(query)}&countrycodes=my&limit=5&format=json";
                var response = await _httpClient.GetAsync(requestUrl);

                if (!response.IsSuccessStatusCode)
                {
                    return StatusCode(500, new
                    {
                        error = "Failed to fetch location data from the API"
                    });
                }

                var jsonResult = await response.Content.ReadAsStringAsync();
                return Content(jsonResult, "application/json");
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while processing your request",
                    message = ex.Message
                });
            }
        }
        [HttpGet("bookingprice")]
        public async Task<IActionResult> GetBookingPrice([FromBody] BookingPriceDto dto)
        {
            try
            {
                var service = await _db.Services.AsNoTracking().FirstOrDefaultAsync(s => s.ServiceID == dto.ServiceID);
                if (service == null)
                {
                    return NotFound(new
                    {
                        error = "Service not found",
                        message = $"No service found with ServiceID: {dto.ServiceID}"
                    });
                }
                var travelFee = await _travelFeeService.CalculateFeeAsync(dto.AreaID, dto.LocationAddress);
                decimal rawTotalPrice = Convert.ToDecimal(service.Price) + travelFee.TotalFee;
                decimal totalPrice = Math.Round(rawTotalPrice, 0, MidpointRounding.AwayFromZero);

                return Ok(new
                {
                    serviceName = service.Name,
                    basePrice = service.Price,
                    distanceKm = travelFee.DistanceKm,
                    travelFee = travelFee.TotalFee,
                    totalPrice = totalPrice,
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new
                {
                    error = "An error occurred while calculating the booking price",
                    message = ex.Message
                });
            }
        }
        [HttpPost("create")]
        public async Task<IActionResult> CreateBooking(
            [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
            [FromBody] UserBookingDto dto)
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
            using var transaction = await _db.Database.BeginTransactionAsync();
            try
            {
                string phonePattern = @"^01[0-9]\d{7,8}$"; // Malaysian phone number format: 01X-XXXXXXX or 01X-XXXXXXXX
                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"; // Basic email format validation

                if (!Regex.IsMatch(dto.PhoneNumber, phonePattern))
                {
                    return BadRequest(new
                    {
                        error = "Invalid phone number format",
                        message = "Phone number must be in the format 01X-XXXXXXX or 01X-XXXXXXXX"
                    });
                }
                if (!Regex.IsMatch(dto.Email, emailPattern))
                {
                    return BadRequest(new
                    {
                        error = "Invalid email format",
                        message = "Email must be in the format example@domain.com"
                    });
                }
                if(dto.Pax < 1)
                {
                    return BadRequest(new
                    {
                        error = "Invalid number of passengers",
                        message = "Number of passengers (Pax) must be at least 1"
                    });
                }
                if (dto.AppointmentDate < DateTime.Now)
                {
                    return BadRequest(new
                    {
                        error = "Invalid appointment date",
                        message = "Appointment date must be in the future"
                    });
                }
                var minimumBookingDate = _db.SystemSettings.Find("MinimumBookingDate");
                var selectedBookingDate = minimumBookingDate != null ? DateTime.Parse(minimumBookingDate.Value) : DateTime.Now.AddDays(2);
                if (dto.AppointmentDate < selectedBookingDate)
                {
                    return BadRequest(new
                    {
                        error = "Appointment date is too soon",
                        message = $"Appointment date must be at least {selectedBookingDate.ToString("yyyy-MM-dd")} or later"
                    });
                }

                var maximumBookingDate = _db.SystemSettings.Find("MaximumBookingDate");
                var selectedMaximumBookingDate = maximumBookingDate != null ? DateTime.Parse(maximumBookingDate.Value) : DateTime.Now.AddMonths(3);
                if (dto.AppointmentDate > selectedMaximumBookingDate)
                {
                    return BadRequest(new
                    {
                        error = "Appointment date is too far in the future",
                        message = $"Appointment date must be on or before {selectedMaximumBookingDate.ToString("yyyy-MM-dd")}"
                    });
                }
                // 2100 - 0259 is unavailable for booking
                if(dto.AppointmentTime >= new TimeSpan(21, 0, 0) || dto.AppointmentTime < new TimeSpan(2, 59, 0))
                {
                    return BadRequest(new
                    {
                        error = "Invalid appointment time",
                        message = "Appointment time must be between 02:59 and 21:00"
                    });
                }
                var existingService = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == dto.ServiceID && s.Status == "Active");
                if (existingService == null)
                {
                    return NotFound(new
                    {
                        error = "Service not found",
                        message = $"No service found with ServiceID {dto.ServiceID}"
                    });
                }
                decimal areaPrice = 0;
                if (dto.AreaID.HasValue)
                {
                    var existingArea = await _db.ServiceAreas.FirstOrDefaultAsync(a => a.AreaID == dto.AreaID.Value && a.IsActive == true);
                    if (existingArea == null)
                    {
                        return NotFound(new
                        {
                            error = "Area not found",
                            message = $"No area found with AreaID {dto.AreaID.Value}"
                        });
                    }
                    if (!dto.LocationAddress.Contains(existingArea.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        return BadRequest(new
                        {
                            error = "Location Mismatch",
                            message = $"You selected '{existingArea.Name}', but your address does not match this area. Please select the correct service area."
                        });
                    }
                    areaPrice = existingArea.BasePrice;
                }

                var travelFee = await _travelFeeService.CalculateFeeAsync(clientAddress: dto.LocationAddress, areaID: dto.AreaID);

                decimal baseServicePrice = Convert.ToDecimal(existingService.Price * dto.Pax);
                decimal rawTotalPrice = baseServicePrice + travelFee.TotalFee + areaPrice;
                decimal totalPrice = Math.Round(rawTotalPrice, 0, MidpointRounding.AwayFromZero);

                var booking = new Booking
                {
                    Name = dto.Name,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    AppointmentDate = dto.AppointmentDate,
                    AppointmentTime = dto.AppointmentTime,
                    ServiceID = dto.ServiceID,
                    Status = BookingStatus.Pending,
                    LocationAddress = dto.LocationAddress,
                    Unit = dto.Unit,
                    DistanceKm = travelFee.DistanceKm,
                    TravelFee = travelFee.TotalFee,
                    TotalPrice = totalPrice,
                    CreatedAt = DateTime.Now,
                    AreaID = dto.AreaID,
                    Pax = dto.Pax,
                };
                var responObj = new
                {
                    message = "Booking created successfully",
                    text = "Please check your email for booking confirmation",
                };
                idempotency.ResponseBody = JsonSerializer.Serialize(responObj);
                idempotency.Status = "Completed";
                idempotency.ResponseCode = 200;
                _db.Bookings.Add(booking); 
                await _db.SaveChangesAsync();

                _ = Task.Run(async() =>
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();

                    try
                    {
                        string emailSubject = "Booking Confirmation";
                        string body = EmailBody(name: dto.Name,
                            phoneNumber: dto.PhoneNumber,
                            appointmentDate: dto.AppointmentDate,
                            appointmentTime: dto.AppointmentTime,
                            service: existingService.Name,
                            location: dto.LocationAddress,
                            totalPrice: totalPrice,
                            pax: dto.Pax);
                        await emailService.SendEmailAsync(email: dto.Email, subject: emailSubject, body: body);

                        await notificationService.SendNotificationAsync(relatedID: booking.BookingID,
                            title: "New Booking Received",
                            message: $"You have a new booking from {dto.Name} on {dto.AppointmentDate.ToString("dd MMM yyyy")} at {dto.AppointmentTime.ToString(@"hh\:mm")}.",
                            type: "Booking");
                    }
                    catch (Exception ex)
                    {
                        // Log the exception
                        _logger.LogError(ex, "Error occurred while sending email or notification.");
                    }
                });

                await transaction.CommitAsync();
                return Ok(responObj);

            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    transaction.Rollback();
                }
                idempotency.Status = "Failed";
                idempotency.ResponseCode = 500;
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = "An error occurred while creating the booking"
                });
                await _db.SaveChangesAsync();
                throw ex;
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
