using MakeUpServiceApi.DTO.FeedbackDTO;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/feedback")]
    [Tags("Admin Feedback Management")]
    [Authorize] 
    public class FeedbackAdminController : ControllerBase
    {
        private readonly AppDbContext _db;
        public FeedbackAdminController(AppDbContext db)
        {
            _db = db;
        }
        [HttpPost("search")]
        public async Task<IActionResult> SearchFeedback([FromQuery] SearchFeedbackDto model)
        {   
            var query = _db.Feedbacks.AsQueryable().AsNoTracking();

            if (model.FeedbackID.HasValue)
            {
                query = query.Where(f => f.FeedbackID == model.FeedbackID.Value);
            }
            if (!string.IsNullOrEmpty(model.Name))
            {
                query = query.Where(f => f.Name.Contains(model.Name));
            }
            if (model.CreatedAt.HasValue)
            {
                query = query.Where(f => f.CreatedAt.Date == model.CreatedAt.Value.Date);
            }
            if (model.IsResolved.HasValue)
            {
                query = query.Where(f => f.IsResolved == model.IsResolved.Value);
            }
            int totalCount = await query.CountAsync();
            var feedback = await query
                .OrderByDescending(f => f.FeedbackID)
                .Skip((model.PageNumber - 1) * model.PageSize)
                .Take(model.PageSize)
                .Select(x => new
                {
                    FeedbackID = x.FeedbackID,
                    Name = x.Name,
                    ContactNumber = x.ContactNumber,
                    Title = x.Title,
                    IsResolved = x.IsResolved ? "Yes" : "No",
                    CreatedAt = x.CreatedAt,
                })
                .ToListAsync();
            return Ok(new
            {
                totalCount = totalCount,
                pageNumber = model.PageNumber,
                pageSize = model.PageSize,
                data = feedback
            });
        }
        [HttpGet("details/{id}")]
        public async Task<IActionResult> GetFeedbackByID(int id)
        {
            var feedback = await _db.Feedbacks
                .Where(f => f.FeedbackID == id)
                .Select(x => new
                {
                    FeedbackID = x.FeedbackID,
                    Name = x.Name,
                    Email = x.Email,
                    ContactNumber = x.ContactNumber,
                    Title = x.Title,
                    Description = x.Description,
                    IsResolved = x.IsResolved ? "Yes" : "No",
                    CreatedAt = x.CreatedAt,
                })
                .FirstOrDefaultAsync();
            if (feedback == null)
            {
                return NotFound(new
                {
                    error = "Feedback not found",
                    message = $"No feedback found with ID {id}"
                });
            }
            return Ok(feedback);
        }
        [HttpPost("resolve/{id}")]
        public async Task<IActionResult> FeedbackResolve(int id)
        {
            var feedback = await _db.Feedbacks.FindAsync(id);
            if (feedback == null)
            {
                return NotFound(new
                {
                    error = "Feedback not found",
                    message = $"No feedback found with ID {id}"
                });
            }
            if(feedback.IsResolved)
            {
                return BadRequest(new
                {
                    error = "Feedback already resolved",
                    message = $"Feedback with ID {id} is already resolved"
                });
            }
            feedback.IsResolved = true;
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = $"Feedback with ID {id} has been marked as resolved",
                feedback = feedback
            });
        }
    }
} 
