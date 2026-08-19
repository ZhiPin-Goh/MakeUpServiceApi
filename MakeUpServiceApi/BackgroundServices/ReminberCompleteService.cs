using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class ReminberCompleteService: BackgroundService
    {
        private readonly ILogger<ReminberCompleteService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public ReminberCompleteService(ILogger<ReminberCompleteService> logger, IServiceProvider serviceProvider)
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

                    var yesterday = DateTime.Now.AddDays(-1).Date;
                    var bookings = await db.Bookings.Where(b => b.AppointmentDate.Date == yesterday && b.Status == BookingStatus.Approved).ToListAsync(stoppingToken);
                    if(bookings.Any())
                    {
                        foreach (var booking in bookings)
                        {
                            await notificationService.SendNotificationAsync(title: "Reminder: Complete Your Booking",
                                message: @$"System automatically reminds you to complete your booking for {booking.Service.Name} on {booking.AppointmentDate:dd/MM/yyyy} at {booking.AppointmentTime}. Please provide your feedback and rating.",
                                type: "BookingCompletionReminder",
                                relatedID: booking.BookingID
                                );
                            booking.Status = BookingStatus.Completed;
                        }        
                        await db.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation("Booking completion reminders sent successfully. Count: {Count}", bookings.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while sending booking completion reminders.");
                }
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
    }
}
