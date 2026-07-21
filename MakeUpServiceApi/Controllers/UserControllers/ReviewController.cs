using MakeUpServiceApi.DTO.ReviewDTO;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/review")]
    [Tags("User Review")]
    public class ReviewController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly INotificationService _notificationService;
        public ReviewController(AppDbContext db, INotificationService notificationService)
        {
            _db = db;
            _notificationService = notificationService;
        }
        [HttpGet("getservicereviews/{serviceID}")]
        public async Task<IActionResult> GetReviewByServiceID(int serviceID)
        {
            var reviews = await _db.Reviews
                .Include(r => r.Booking)
                .ThenInclude(b => b.Service)  
                .OrderByDescending(r => r.CreatedAt)
                .Where(r => r.Booking.ServiceID == serviceID)
                .Select(r => new
                {
                    r.ReviewID,
                    r.Rating,
                    r.Comment,
                    r.Name
                }).Take(10)
                .ToListAsync();
            return Ok(reviews);
        }
        [HttpPost("addreview")]
        public async Task<IActionResult> CreateReview([FromBody] CreateReviewDto dto)
        {
            var existingReview = await _db.Reviews.FirstOrDefaultAsync(r => r.ReviewToken == dto.ReviewToken);
            if(existingReview == null)
            {
                return NotFound(new
                {
                    error = "Review token not found",
                    message = "Please check your review token and try again."
                });
            }
            if(existingReview.Status == "Visible")
            {
                return BadRequest(new
                {
                    error = "Review already submitted",
                    message = "You have already submitted a review for this booking."
                });
            }
            if (existingReview.TokenExpiryDate < DateTime.Now)
            {
                return BadRequest(new
                {
                    error = "Review token expired",
                    message = "The review token has expired. Please request a new review token."
                });
            }
            if(dto.Rating < 1 || dto.Rating > 5)
            {
                return BadRequest(new
                {
                    error = "Invalid rating",
                    message = "Rating must be between 1 and 5."
                });
            }
            existingReview.Rating = dto.Rating;
            existingReview.Comment = string.IsNullOrWhiteSpace(dto.Comment) ? string.Empty : dto.Comment;
            existingReview.Name = string.IsNullOrWhiteSpace(dto.Name) ? "Anonymous" : dto.Name; // 加个匿名处理
            existingReview.Status = "Visible";
            await _db.SaveChangesAsync();

            _ = Task.Run(async () =>
            {
                try
                {
                    await _notificationService.SendNotificationAsync(title: "New Review Submitted",
                   relatedID: existingReview.ReviewID,
                   message: $"A new review has been submitted for booking ID {existingReview.BookingID}.",
                   type: "Review"
                   );
                }
                catch (Exception ex)
                {
                    throw;
                }
            });

            return Ok(new
            {
                message = "Review submitted successfully",
                review = new
                {
                    existingReview.ReviewID,
                    existingReview.Rating,
                    existingReview.Comment,
                    existingReview.Name
                }
            });
        }
    }
}
