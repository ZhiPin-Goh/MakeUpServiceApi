using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class BookingReminderService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<BookingReminderService> _logger;
        public BookingReminderService(IServiceProvider serviceProvider, ILogger<BookingReminderService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var notificationService = scope.ServiceProvider.GetRequiredService<INotificationService>();
                    var emailService = scope.ServiceProvider.GetRequiredService<IEmailService>();

                    var tomorrow = DateTime.Now.AddDays(1).Date;

                    var upComingBookings = await db.Bookings
                        .Where(b => b.AppointmentDate.Date == tomorrow && b.Status == BookingStatus.Pending)
                        .ToListAsync(stoppingToken);

                    if(upComingBookings.Any())
                    {
                        foreach (var booking in upComingBookings)
                        {
                            await notificationService.SendNotificationAsync(title: "Booking Reminder",
                                message: @$"Reminder: You have an appointment scheduled for {booking.AppointmentDate.ToString("dd/MM/yyyy")}
                                        Time: {booking.AppointmentTime}. Please make sure to be on time.",
                                type: "BookingReminder",
                                relatedID: booking.BookingID
                                );
                            string emailBody = EmailBody(booking.Name, booking.Service.Name, booking.AppointmentDate, booking.AppointmentTime);

                            await emailService.SendEmailAsync(booking.Email, "Trip Reminder", emailBody);
                        }
                        _logger.LogInformation("Sent reminders for {Count} upcoming bookings.", upComingBookings.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while sending booking reminders.");
                }
                await Task.Delay(TimeSpan.FromDays(1), stoppingToken);
            }
        }
        private string EmailBody(string name, string service, DateTime date, TimeSpan timeSpan)
        {
            string emailBody = $@"
                                <!DOCTYPE html>
                                <html>
                                <head>
                                    <meta charset=""UTF-8"">
                                </head>
                                <body style=""margin: 0; padding: 0; background-color: #faf7f7; font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; -webkit-font-smoothing: antialiased;"">
    
                                    <!-- 外层背景 -->
                                    <div style=""width: 100%; background-color: #faf7f7; padding: 40px 0;"">
        
                                        <!-- 卡片主体 (稍微变窄，更显精致) -->
                                        <div style=""max-width: 480px; margin: 0 auto; background-color: #ffffff; border-radius: 12px; box-shadow: 0 8px 20px rgba(0, 0, 0, 0.03); overflow: hidden; border-top: 5px solid #d4a373;"">
            
                                            <!-- 品牌标识 -->
                                            <div style=""padding: 30px 30px 10px 30px; text-align: center;"">
                                                <h1 style=""margin: 0; color: #4a4a4a; font-family: 'Playfair Display', 'Georgia', serif; font-size: 22px; font-weight: normal; letter-spacing: 2px; text-transform: uppercase;"">
                                                    Shirley <span style=""color: #d4a373;"">Makeup</span>
                                                </h1>
                                            </div>
            
                                            <!-- 内容区 -->
                                            <div style=""padding: 15px 35px 35px 35px; color: #4a4a4a; line-height: 1.6;"">
                
                                                <!-- 亲切的提醒标题 -->
                                                <h2 style=""margin: 0 0 20px 0; font-family: 'Playfair Display', 'Georgia', serif; font-size: 22px; color: #333333; text-align: center;"">
                                                    See You Soon! 🤍
                                                </h2>
                
                                                <p style=""margin: 0 0 15px 0; font-size: 15px;"">
                                                    Hi <strong>{name}</strong>,
                                                </p>
                                                <p style=""margin: 0 0 25px 0; font-size: 15px; color: #666666;"">
                                                    This is just a friendly reminder about your upcoming appointment. We are so excited to see you and get you ready for your day!
                                                </p>
                
                                                <!-- 极简信息框 (无 Table，纯文本居中排版) -->
                                                <div style=""background-color: #fffaf7; border-radius: 8px; padding: 25px 20px; margin: 25px 0; text-align: center;"">
                                                    <p style=""margin: 0 0 8px 0; font-size: 16px; color: #333333;"">
                                                        <strong>{service}</strong>
                                                    </p>
                                                    <p style=""margin: 0; font-size: 16px; color: #d4a373; font-weight: bold;"">
                                                        {date.ToString("MM/dd/yyyy")} • {timeSpan}
                                                    </p>
                                                </div>
                                            </div>
                           
                                        </div>
                                    </div>
                                </body>
                            </html>";
            return emailBody;
        }
    }
}
