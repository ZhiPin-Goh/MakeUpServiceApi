using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.InterfaceServices
{
    public class NotificationService : INotificationService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<NotificationService> _logger;
        public NotificationService(AppDbContext db, ILogger<NotificationService> logger)
        {
            _db = db;
            _logger = logger;
        }
        public async Task SendNotificationAsync(int? relatedID, string title, string message, string type)
        {
            try
            {
                var notification = new Notification
                {
                    Title = title,
                    Message = message,
                    Type = type,
                    RelatedID = relatedID,
                    IsRead = false,
                    CreatedAt = DateTime.Now,
                };
                await _db.Notifications.AddAsync(notification);
                await _db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error sending notification");
                throw; // Optionally rethrow or handle the exception as needed
            }
        }
    }
}
