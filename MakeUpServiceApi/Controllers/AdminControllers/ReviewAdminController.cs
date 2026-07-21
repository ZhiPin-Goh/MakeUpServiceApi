using MakeUpServiceApi.DTO.ReviewDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/reviews")]
    [Tags("Admin Reviews Management")]
    [Authorize]
    public class ReviewAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly IReviewService _reviewService;
        private readonly ILogger<ReviewAdminController> _logger;
        public ReviewAdminController(AppDbContext db, IReviewService reviewService, ILogger<ReviewAdminController> logger)
        {
            _db = db;
            _reviewService = reviewService;
            _logger = logger;
        }
        [HttpPost("search")]
        public async Task<IActionResult> SearchReview([FromQuery] SearchReviewDto model)
        {
            var query = _db.Reviews.AsQueryable().AsNoTracking();
            if (!string.IsNullOrEmpty(model.Status))
            {
                query = query.Where(r => r.Status == model.Status);
            }
            if (model.ReviewID.HasValue)
            {
                query = query.Where(r => r.ReviewID == model.ReviewID.Value);
            }
            if (model.Rating.HasValue)
            {
                query = query.Where(r => r.Rating == model.Rating.Value);
            }
            if (model.CreatedAt.HasValue)
            {
                query = query.Where(r => r.CreatedAt.Date == model.CreatedAt.Value.Date);
            }
            int totalCount = await query.CountAsync();
            var reviews = await query
                .Include(r => r.Booking)
                .ThenInclude(r => r.Service)
                .OrderByDescending(r => r.CreatedAt)
                .Skip((model.PageNumber - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Name,
                    r.CreatedAt,
                    Service = r.Booking != null ? r.Booking.Service.Name : null,
                })
                .ToListAsync();
            return Ok(new
            {
                totalCount = totalCount,
                pageNumber = model.PageNumber,
                pageSize = model.PageSize,
                data = reviews
            });
        }
        [HttpGet("getreview/{reviewID}")]
        public async Task<IActionResult> GetReviewByID(int reviewID)
        {
            var review = await _db.Reviews.AsNoTracking()
                .Include(r => r.Booking)
                .ThenInclude(b => b.Service)
                .Where(r => r.ReviewID == reviewID)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Name,
                    r.Comment,
                    r.Status,
                    r.CreatedAt,
                    Service = r.Booking != null ? r.Booking.Service.Name : null,
                }).FirstOrDefaultAsync();
            if (review == null)
            {
                return NotFound(new
                {
                    error = "Review not found",
                    message = "The review with the specified ID does not exist."
                });
            }
            return Ok(review);

        }
        [HttpPost("generate-manual-link")]
        public async Task<IActionResult> GenerateManualLink(
            [FromHeader(Name = "X-Idempotency-Key")] string idempotencyKey,
            [FromBody] ManualReviewLinkDto dto)
        {
            if(string.IsNullOrEmpty(idempotencyKey))
            {
                return BadRequest(new
                {
                    error = "Idempotency Key Required",
                    message = "Please provide an Idempotency Key in the X-Idempotency-Key header.",
                    statusCode = 400
                });
            }   
            var existingIdempotency = await _db.Idempotencies.FirstOrDefaultAsync(k => k.IdempotencyKey == idempotencyKey);
            if(existingIdempotency != null)
            {
                if(existingIdempotency.Status == "Completed")
                {
                    return Content(existingIdempotency.ResponseBody, "application/json");
                }
                if(existingIdempotency.Status == "Started")
                {
                    return Conflict(new
                    {
                        error = "Request Already In Progress",
                        message = "A request with this Idempotency Key is already being processed. Please wait for it to complete.",
                        statusCode = 409
                    });
                }
              
            }
            var idempotency = new Idempotency
            {
                IdempotencyKey = idempotencyKey,
                RequestHash = "Manual Link Created",
                Status = "Started",
                CreatedAt = DateTime.Now,
                ResponseBody = ""
            };
            _db.Idempotencies.Add(idempotency);
            await _db.SaveChangesAsync();

            try
            {
                string phonePattern = @"^01[0-9]\d{7,8}$"; // 01X-XXXXXXX or 01X-XXXXXXXX
                var existingService = await _db.Services.AnyAsync(s => s.ServiceID == dto.ServiceID);
                if (!existingService)
                {
                    return NotFound(new
                    {
                        error = "Service Not Found",
                        message = $"No service found with ID {dto.ServiceID}."
                    });
                }

                if (!Regex.IsMatch(dto.PhoneNumber, phonePattern))
                {
                    return BadRequest(new
                    {
                        error = "Invalid Phone Number",
                        message = "Please enter a valid phone number in the format 01X-XXXXXXX or 01X-XXXXXXXX."
                    });
                }
                var link = await _reviewService.GenerateLinkForManualReviewAsync(customerName: dto.Name, serviceID: dto.ServiceID, phoneNumber: dto.PhoneNumber);
                if (link == null)
                {
                    return StatusCode(500, new
                    {
                        error = "Link Generation Failed",
                        message = "An error occurred while generating the manual review link. Please try again later."
                    });
                }
                var responseObj = new
                {
                    reviewLink = link,
                    message = "Manual review link generated successfully."
                };
                idempotency.Status = "Completed";
                idempotency.ResponseBody = JsonSerializer.Serialize(responseObj);
                idempotency.ResponseCode = 200;
                return Ok(responseObj);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating manual review link.");
                idempotency.Status = "Failed";
                idempotency.ResponseBody = JsonSerializer.Serialize(new
                {
                    error = "Internal Server Error",
                    message = $"An error occurred while processing your request: {ex.Message}"
                });
                idempotency.ResponseCode = 500;
                await _db.SaveChangesAsync();
                return StatusCode(500, new
                {
                    error = "Internal Server Error",
                    message = $"An error occurred while processing your request: {ex.Message}"
                });
            }
           
        }
        [HttpPost("generate-link/{id}")]
        public async Task<IActionResult> GenerateLink(int id)
        {
            var existingBooking = await _db.Bookings.AnyAsync(b => b.BookingID == id);
            if (!existingBooking)
            {
                return NotFound(new
                {
                    error = "Booking Not Found",
                    message = $"No booking found with ID {id}."
                });
            }
            var link = await _reviewService.GenerateLinkForExistingReviewAsync(id);
            if(link == null)
            {
                return StatusCode(500, new
                {
                    error = "Link Generation Failed",
                    message = "An error occurred while generating the review link. Please try again later."
                });
            }
            return Ok(new
            {
                message = "Review link generated successfully.",
                reviewLink = link
            });
        }
        [HttpPost("toggle-status/{id}")]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var review = await _db.Reviews.FirstOrDefaultAsync(x => x.ReviewID == id);
            if (review == null)
            {
                return NotFound(new
                {
                    error = "Review Not Found",
                    message = $"No review found with ID {id}."
                });
            }
            string status = review.Status == "Visible" ? "Hidden" : "Visible";
            review.Status = status;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = $"Review status updated to {status}.",
                reviewID = review.ReviewID,
                newStatus = review.Status
            });
        }
    }
}
