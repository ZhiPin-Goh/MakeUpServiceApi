using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class ReminbersBookingService : BackgroundService
    {
        private readonly ILogger<ReminbersBookingService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public ReminbersBookingService(ILogger<ReminbersBookingService> logger, IServiceProvider serviceProvider)
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
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var tomorrowDays = DateTime.Now.AddDays(1).Date;

                    var bookings = await db.Bookings.Where(b => b.AppointmentDate.Date == tomorrowDays).ToListAsync(stoppingToken);
                    if (bookings.Any())
                    {
                        foreach (var booking in bookings)
                        {
                            await notificationService.SendNotificationAsync(title: "Booking Reminder",
                                message: @$"Reminder: You have an appointment scheduled for {booking.AppointmentDate.ToString("dd/MM/yyyy")}
                                        Time: {booking.AppointmentDate.ToString("HH:mm")}. Please make sure to be on time.",
                                type: "BookingReminder",
                                relatedID: booking.BookingID
                                );
                        }
                        _logger.LogInformation("Booking reminders sent successfully. Count: {Count}", bookings.Count);
                    }

                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while sending booking reminders.");
                }
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
