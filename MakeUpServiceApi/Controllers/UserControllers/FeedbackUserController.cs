using MakeUpServiceApi.DTO.FeedbackDTO;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Mvc;
using System.Text.RegularExpressions;

namespace MakeUpServiceApi.Controllers.UserControllers
{
    [ApiController]
    [Route("api/user/feedbacks")]
    [Tags("User Feedbacks")]
    public class FeedbackUserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public FeedbackUserController(AppDbContext db)
        {
            _db = db;
        }
        [HttpPost("submit")]
        public async Task<IActionResult> SubmitFeedback([FromBody] CreateFeedbackDto dto)
        {
            string phonePattern = @"^01[0-9]\d{7,8}$";

            if (!Regex.IsMatch(dto.ContactNumber, phonePattern))
            {
                return BadRequest(new
                {
                    error = "Invalid phone number format",
                    message = "Phone number must be in the format 01XXXXXXXX or 01XXXXXXXXX"
                });
            }
            if (!string.IsNullOrEmpty(dto.Email))
            {
                string emailPattern = @"^[a-zA-Z0-9._%+-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}$";
                if (!Regex.IsMatch(dto.Email, emailPattern))
                {
                    return BadRequest(new
                    {
                        error = "Invalid email format",
                        message = "Email must be in the format"
                    });
                }
            }
            var feedback = new Feedback
            {
                Name = dto.Name,
                ContactNumber = dto.ContactNumber,
                Email = dto.Email,
                Description = dto.Description,
                IsResolved = false,
                CreatedAt = DateTime.Now,
                Title = dto.Title,
            };
            _db.Feedbacks.Add(feedback);
            await _db.SaveChangesAsync();
            return Ok(new
            {
                message = "Feedback submitted successfully",
                feedbackId = feedback.FeedbackID
            });
        }
    }
}
