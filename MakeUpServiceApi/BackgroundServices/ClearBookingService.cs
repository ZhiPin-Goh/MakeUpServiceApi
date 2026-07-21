using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class ClearBookingService:BackgroundService
    {
        private readonly ILogger<ClearBookingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public ClearBookingService(ILogger<ClearBookingService> logger, IServiceProvider serviceProvider)
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
                    var expiredBookings = await db.Bookings
                        .Where(b => b.AppointmentDate < DateTime.Now && b.Status == BookingStatus.Pending)
                        .ToListAsync(stoppingToken);

                    if (expiredBookings.Any())
                    {
                        foreach(var booking in expiredBookings)
                        {
                            booking.Status = BookingStatus.Canceled;
                        }
                        await db.SaveChangesAsync(stoppingToken);
                        _logger.LogInformation("Cleared {count} expired bookings at: {time}", expiredBookings.Count, DateTimeOffset.Now);
                    }
                    _logger.LogInformation("ClearBookingService executed at: {time}", DateTimeOffset.Now);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while clearing expired bookings at: {time}", DateTimeOffset.Now);
                }
                await Task.Delay(TimeSpan.FromHours(2), stoppingToken);
            }
        }
    }
}
