using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.Controllers.AdminControllers
{
    [ApiController]
    [Route("api/admin/notifications")]
    [Authorize]
    [Tags("Admin Notifications Management")]
    public class NotificationAdminController : ControllerBase
    {
        private readonly INotificationService _notificationService;
        private readonly AppDbContext _db;
        public NotificationAdminController(INotificationService notificationService, AppDbContext db)
        {
            _notificationService = notificationService;
            _db = db;
        }
        [HttpGet("unreadcount")]
        public async Task<IActionResult> GetUnreadCount()
        {
            var count = await _notificationService.GetUnreadCountAsync();
            return Ok(count);
        }
        [HttpGet("unread")]
        public async Task<IActionResult> GetUnreadNotifications()
        {
            var unreadNotifications = await _db.Notifications.AsNoTracking()
                .Where(n => !n.IsRead)
                .Take(50)
                .OrderByDescending(n => n.CreatedAt)
                 .Select(n => new
                 {
                     n.NotificationID,
                     n.Title,
                     // message show 50 length
                     Message = n.Message.Length > 50 ? n.Message.Substring(0, 50) + "..." : n.Message,
                     n.Type,
                     n.CreatedAt,
                     n.RelatedID
                 }).ToListAsync();
            return Ok(unreadNotifications);
        }
        // Endpoint to mark a notification as read
        // And notification details will be returned in the response
        public class MarkAsReadRequest
        {
            public int NotificationID { get; set; }
        }
        [HttpPost("markasread")]
        public async Task<IActionResult> MarkAsRead([FromBody] MarkAsReadRequest request)
        {
            var notification = await _db.Notifications.FindAsync(request.NotificationID);
            if (notification == null)
            {
                return NotFound(new
                {
                    error = "Notification not found",
                    message = "The notification with the specified ID does not exist."
                });
            }
            if (!notification.IsRead)
            {
                notification.IsRead = true;
                await _db.SaveChangesAsync();
            }
            var response = new
            {
                message = "Notification marked as read successfully",
                notification = new
                {
                    notification.NotificationID,
                    notification.Title,
                    notification.Message,
                    notification.Type,
                    notification.CreatedAt,
                    notification.RelatedID
                }
            };
            return Ok(response);
        }
    }
}
