using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class ClearNotifyService:BackgroundService
    {
        private readonly ILogger<ClearNotifyService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public ClearNotifyService(ILogger<ClearNotifyService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var dateDelete = DateTime.Now.AddDays(-7); // Delete notifications older than 7 days

                    var notificationsToDelete =await db.Notifications
                        .Where(n => n.CreatedAt.Date < dateDelete && n.IsRead)
                        .ToListAsync(stoppingToken);

                    if (notificationsToDelete.Any())
                    {
                        foreach (var notification in notificationsToDelete)
                        {
                            db.Notifications.Remove(notification);
                        }
                        await db.SaveChangesAsync(stoppingToken);

                    }
                    _logger.LogInformation("ClearNotifyService executed at: {time}. Deleted {count} notifications.", DateTimeOffset.Now, notificationsToDelete.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while executing ClearNotifyService.");
                }

                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
