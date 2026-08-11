using MakeUpServiceApi.DTO.BookingDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Transactions;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/bookings")]
    [Authorize]
    [Tags("Admin Bookings Management")]
    public class BookingAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly ILogger<BookingAdminController> _logger;
        private readonly ITravelFeeService _travelFeeService;
        private readonly IEmailService _emailService;
        private readonly IGoogleCalendarService _googleCalendarService;
        public BookingAdminController(AppDbContext db, ILogger<BookingAdminController> logger, ITravelFeeService travelFeeService, IEmailService emailService, IGoogleCalendarService googleCalendarService)
        {
            _db = db;
            _logger = logger;
            _travelFeeService = travelFeeService;
            _emailService = emailService;
            _googleCalendarService = googleCalendarService;
        }
        [HttpPost("search")]
        public async Task<IActionResult> SearchBooking([FromQuery] SearchBookingDto model)
        {
            var query = _db.Bookings.AsQueryable().AsNoTracking();
            if (model.BookingID.HasValue)
            {
                query = query.Where(b => b.BookingID == model.BookingID.Value);
            }
            if (model.ServiceID.HasValue)
            {
                query = query.Where(b => b.ServiceID == model.ServiceID.Value);
            }
            if (model.AreaID.HasValue)
            {
                query = query.Where(b => b.AreaID == model.AreaID.Value);
            }
            if (model.AppointmentDate.HasValue)
            {
                query = query.Where(b => b.AppointmentDate == model.AppointmentDate.Value);
            }
            if (model.Status.HasValue)
            {
                query = query.Where(b => b.Status == model.Status);
            }
            if (!string.IsNullOrEmpty(model.PhoneNumber))
            {
                query = query.Where(b => b.PhoneNumber.Contains(model.PhoneNumber));
            }
            int totalCount = await query.CountAsync();
            var bookings = await query
                .Include(b => b.Service)
                .Include(b => b.ServiceArea)
                .OrderByDescending(b => b.BookingID)
                .Skip((model.PageNumber - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(b => new
                {
                    BookingID = b.BookingID,
                    Name = b.Name,
                    PhoneNumber = b.PhoneNumber,
                    AppointmentDate = b.AppointmentDate,
                    Service = b.Service.Name,
                    Area = b.ServiceArea.Name,
                    TotalPrice = b.TotalPrice,
                    Status = b.Status.ToString()
                })
                .ToListAsync();
            return Ok(new
            {
                totalCount = totalCount,
                pageNumber = model.PageNumber,
                pageSize = model.PageSize,
                data = bookings
            });
        }
        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetBookingByID(int id)
        {
            var bookingDetails = await _db.Bookings
             .Where(b => b.BookingID == id)
             .Select(b => new
             {
                 BookingID = b.BookingID,
                 Name = b.Name,
                 Email = b.Email,
                 PhoneNumber = b.PhoneNumber,
                 AppointmentDate = b.AppointmentDate,
                 AppointmentTime = b.AppointmentTime,
                 Area = b.ServiceArea.Name,
                 Address = b.LocationAddress,
                 Service = b.Service.Name,
                 TravelFee = b.TravelFee,
                 TotalPrice = b.TotalPrice,
                 Status = b.Status.ToString()
             })
             .FirstOrDefaultAsync();

            if (bookingDetails == null)
            {
                return NotFound(new
                {
                    error = "Booking not found",
                    message = $"No booking found with BookingID {id}"
                });
            }

            return Ok(bookingDetails);
        }
        [HttpPost("toggle-status")]
        public async Task<IActionResult> ToggleBookingStatus([FromBody] ToggleStatusDto dto)
        {
            var existingBooking = await _db.Bookings
                .Include(b => b.Service)
                .FirstOrDefaultAsync(b => b.BookingID == dto.BookingID);
            if (existingBooking == null)
            {
                return NotFound(new
                {
                    error = "Booking not found",
                    message = $"No booking found with ID {dto.BookingID}"
                });
            }
            if (existingBooking.Status == dto.Status)
            {
                return BadRequest(new
                {
                    error = "Status unchanged",
                    message = $"Booking status is already {dto.Status}"
                });
            }

            // checking if the booking is being approved, we need to check for conflicts
            // 检查是否有冲突的预约
            if (existingBooking.Status == BookingStatus.Completed ||
                existingBooking.Status == BookingStatus.Rejected ||
                existingBooking.Status == BookingStatus.Canceled)
            {
                return BadRequest(new
                {
                    error = "Invalid status change",
                    message = $"Cannot change status from {existingBooking.Status} to {dto.Status}"
                });
            }

            // checking if the booking is being approved, we need to check for conflicts
            // 检查是否有冲突的预约
            var currentDateTime = DateTime.Now;
            if (dto.Status == BookingStatus.Completed && existingBooking.AppointmentDate > currentDateTime && existingBooking.AppointmentTime > currentDateTime.TimeOfDay)
            {
                return BadRequest(new
                {
                    error = "Invalid status change",
                    message = "Cannot mark a future booking as completed."
                });
            }

            if (dto.Status == BookingStatus.Rejected && existingBooking.AppointmentDate < currentDateTime.Date)
            {
                return BadRequest(new
                {
                    error = "Invalid status change",
                    message = $"Cannot reject a booking that has already passed"
                });
            }

            // Google calendar function
            string googleEventID = string.Empty;
            if (dto.Status == BookingStatus.Approved)
            {
                try
                {
                    googleEventID = await _googleCalendarService.CreateEventAsync(existingBooking, existingBooking.Service);
                    existingBooking.GoogleEventID = googleEventID;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating Google Calendar event for booking ID {BookingID}", existingBooking.BookingID);
                }
            }
            else if (dto.Status == BookingStatus.Rejected || dto.Status == BookingStatus.Canceled)
            {
                if (!string.IsNullOrEmpty(existingBooking.GoogleEventID))
                {
                    try
                    {
                        await _googleCalendarService.DeleteEventAsync(existingBooking.GoogleEventID);
                        existingBooking.GoogleEventID = null;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error deleting Google Calendar event for booking ID {BookingID}", existingBooking.BookingID);
                    }
                }
            }

            // Update the booking status
            // 更新预约状态
            existingBooking.Status = dto.Status;
            await _db.SaveChangesAsync();

            if (dto.Status == BookingStatus.Approved)
            {
                try
                {
                    string emailbody = EmailBody(name: existingBooking.Name,
                    service: existingBooking.Service.Name,
                    date: existingBooking.AppointmentDate,
                    time: existingBooking.AppointmentTime,
                    location: existingBooking.LocationAddress);
                    await _emailService.SendEmailAsync(email: existingBooking.Email, subject: "Booking Approved", body: emailbody);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error sending approval email for booking ID {BookingID}", existingBooking.BookingID);
                }

            }

            return Ok(new
            {
                message = $"Booking status updated to {dto.Status}",
                bookingID = existingBooking.BookingID,
                eventID = googleEventID,
            });
        }
        [HttpPost("manual-booking")]
        public async Task<IActionResult> ManualBooking(
            [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
            [FromBody] ManualBookingDto dto)
        {
            if (string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new
                {
                    error = "Missing Idempotency Key",
                    message = "Please provide a unique Idempotency Key in the request header.",
                    statusCode = 400
                });
            }
            var existingIdempotency = await _db.Idempotencies.FirstOrDefaultAsync(k => k.IdempotencyKey == idempotencyKey);
            if (existingIdempotency != null)
            {
                if (existingIdempotency.Status == "Completed")
                    return Content(existingIdempotency.ResponseBody, "application/json");
                if (existingIdempotency.Status == "Started")
                    return StatusCode(409, new { error = "Request in progress", message = "A request with this Idempotency Key is already being processed." });
            }

            var requestData = JsonSerializer.Serialize(dto);
            var rawHash = $"ManualBooking:{requestData}";
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
                var schedulingBlocker = await _db.ScheduleBlockers.AnyAsync(sb => sb.StartDate.Date == dto.AppointmentDate.Date);
                if (schedulingBlocker)
                {
                    return BadRequest(new
                    {
                        error = "Scheduling conflict",
                        message = "The Schedule Blocker is active for the selected date. Please choose another date."
                    });
                }
                string phonePattern = @"^01[0-9]\d{7,8}$";
                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$"; // simple email: abc@email.com
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
                        message = "Email must be in the format abc@email.com"
                    });
                }
                if (dto.AppointmentDate < DateTime.Now)
                {
                    return BadRequest(new
                    {
                        error = "Invalid appointment date",
                        message = "Appointment date cannot be in the past"
                    });
                }
                // 21:00 - 02:59 is unavailable for booking
                if (dto.AppointmentTime >= new TimeSpan(21, 0, 0) || dto.AppointmentTime < new TimeSpan(3, 0, 0))
                {
                    return BadRequest(new
                    {
                        error = "Invalid appointment time",
                        message = "Appointment time must be between 03:00 and 21:00"
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
                            error = "Service area not found",
                            message = $"No service area found with ID {dto.AreaID}"
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
                if (dto.Pax < 1)
                {
                    return BadRequest(new
                    {
                        error = "Invalid number of participants",
                        message = "Number of participants (Pax) must be at least 1"
                    });
                }
                var existingService = await _db.Services.FindAsync(dto.ServiceID);
                if (existingService == null)
                {
                    return NotFound(new
                    {
                        error = "Service not found",
                        message = $"No service found with ID {dto.ServiceID}"
                    });
                }

                var travelFee = await _travelFeeService.CalculateFeeAsync(dto.AreaID, dto.LocationAddress);
                decimal baseServicePrice = Convert.ToDecimal(existingService.Price * dto.Pax);
                decimal rawTotalPrice = baseServicePrice + travelFee.TotalFee;
                decimal totalPrice = Math.Round(rawTotalPrice, 0, MidpointRounding.AwayFromZero);

                var newBooking = new Booking
                {
                    Name = dto.Name,
                    Email = dto.Email,
                    PhoneNumber = dto.PhoneNumber,
                    AppointmentDate = dto.AppointmentDate,
                    AppointmentTime = dto.AppointmentTime,
                    ServiceID = dto.ServiceID,
                    LocationAddress = dto.LocationAddress,
                    DistanceKm = travelFee.DistanceKm,
                    TravelFee = travelFee.TotalFee,
                    TotalPrice = totalPrice,
                    AreaID = dto.AreaID,
                    CreatedAt = DateTime.Now,
                    Status = BookingStatus.Approved,
                    Pax = dto.Pax,
                    Unit = !string.IsNullOrEmpty(dto.Unit) ? dto.Unit : null,
                };



                _db.Bookings.Add(newBooking);
                await _db.SaveChangesAsync();

                var responseObj = new
                {
                    message = "Booking created successfully",
                    bookingID = newBooking.BookingID,
                };
                idempotency.ResponseBody = JsonSerializer.Serialize(responseObj);
                idempotency.Status = "Completed";
                idempotency.ResponseCode = 200;

                string googleEventID = string.Empty;
                var serviceName = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == dto.ServiceID);
                try
                {
                    googleEventID = await _googleCalendarService.CreateEventAsync(booking: newBooking, service: serviceName);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error creating Google Calendar event for booking ID {BookingID}", newBooking.BookingID);
                }
                newBooking.GoogleEventID = googleEventID;

                await _db.SaveChangesAsync();

                await transaction.CommitAsync();
                return Ok(responseObj);
            }
            catch (Exception ex)
            {
                if (transaction != null)
                {
                    await transaction.RollbackAsync();
                }

                _logger.LogError(ex, "Error processing booking: {Message}", ex.Message);

                idempotency.Status = "Failed";
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal server error",
                    message = $"An error occurred while processing the booking: {ex.Message}"
                });
                idempotency.ResponseCode = 500;

                // 保存失败状态
                await _db.SaveChangesAsync();

                return StatusCode(500, new
                {
                    error = "Internal server error",
                    message = $"An error occurred while processing the booking: {ex.Message}"
                });
            }
        }
        private string EmailBody(string name, DateTime date, TimeSpan time, string service, string location)
        {
            string emailBody = $@"
                <!DOCTYPE html>
                <html>
                <head>
                    <meta charset=""UTF-8"">
                </head>
                <body style=""margin: 0; padding: 0; background-color: #faf7f7; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
    
                    <!-- 外层背景 -->
                    <div style=""width: 100%; background-color: #faf7f7; padding: 40px 0;"">
        
                        <!-- 卡片主体 (玫瑰金顶部边框) -->
                        <div style=""max-width: 550px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 10px 25px rgba(0, 0, 0, 0.03); overflow: hidden; border-top: 6px solid #d4a373;"">
            
                            <!-- 品牌标识 -->
                            <div style=""padding: 35px 30px 10px 30px; text-align: center;"">
                                <h1 style=""margin: 0; color: #4a4a4a; font-family: 'Playfair Display', 'Georgia', serif; font-size: 26px; font-weight: normal; letter-spacing: 2px; text-transform: uppercase;"">
                                    Shirley <span style=""color: #d4a373;"">Makeup</span>
                                </h1>
                            </div>
            
                            <!-- 内容区 -->
                            <div style=""padding: 20px 35px 35px 35px; color: #4a4a4a; line-height: 1.6;"">
                
                                <!-- 审核通过的特殊标题 -->
                                <div style=""text-align: center; margin-bottom: 25px;"">
                                    <span style=""display: inline-block; background-color: #e8f5e9; color: #2e7d32; font-size: 12px; font-weight: bold; padding: 6px 16px; border-radius: 20px; text-transform: uppercase; letter-spacing: 1px; margin-bottom: 15px;"">
                                        Status: Approved ✓
                                    </span>
                                    <h2 style=""margin: 0; font-family: 'Playfair Display', 'Georgia', serif; font-size: 24px; color: #333333;"">
                                        Great News! Your Booking is Confirmed.
                                    </h2>
                                </div>
                
                                <p style=""margin: 0 0 15px 0; font-size: 15px;"">
                                    Hi <strong>{name}</strong>,
                                </p>
                                <p style=""margin: 0 0 25px 0; font-size: 15px; color: #666666;"">
                                    We are thrilled to let you know that your pending appointment has been reviewed and officially approved by our team. Your spot is now secured!
                                </p>
                
                                <!-- 预约信息详情卡片 (表格对齐) -->
                                <div style=""background-color: #fdfbfb; border: 1px solid #f3e8e0; border-radius: 8px; padding: 25px; margin: 25px 0;"">
                                    <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""font-size: 14px; color: #4a4a4a;"">
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; width: 40%;"">Name</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333;"">{name}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Date & Time</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{date.ToString("yyyy-MM-dd")} at {time}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Service</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{service}</td>
                                        </tr>
                                        <tr>
                                            <td style=""padding: 8px 0; color: #888888; border-top: 1px solid #f3e8e0;"">Location</td>
                                            <td style=""padding: 8px 0; font-weight: bold; color: #333333; border-top: 1px solid #f3e8e0;"">{location}</td>
                                        </tr>
                                    </table>
                                </div>
                
                                <p style=""margin: 0 0 25px 0; font-size: 14px; color: #777777; text-align: center;"">
                                    If you need to make any changes, please let us know at least 24 hours in advance.
                                </p>

                               
                            </div>
                        </div>
                    </div>
                </body>
                </html>";
            return emailBody;
        }
        [HttpPost("update-travel-fee")]
        public async Task<IActionResult> UpdateTravelFee([FromBody] UpdateTravelFeeDto dto)
        {
            var existingBooking = await _db.Bookings.FirstOrDefaultAsync(b => b.BookingID == dto.BookingID);
            if (existingBooking == null)
            {
                return NotFound(new { error = "Booking not found" });
            }
            if (dto.NewTravelFee < 0)
            {
                return BadRequest(new { error = "Travel fee cannot be negative" });
            }

            //重新计算总费用 
            // Re-calculate total fee based on the new travel fee
            var service = await _db.Services.FirstOrDefaultAsync(s => s.ServiceID == existingBooking.ServiceID);
            if (service == null)
            {
                return NotFound(new { error = "Service not found" });
            }

            decimal servicePrice = Convert.ToDecimal(service.Price);
            servicePrice = servicePrice * existingBooking.Pax;
            decimal BaseTotal = servicePrice + dto.NewTravelFee;

            existingBooking.TravelFee = dto.NewTravelFee;
            existingBooking.TotalPrice = BaseTotal;

            await _db.SaveChangesAsync();

            return Ok(new
            {
                message = "Travel fee updated successfully",
            });
        }
    }
}