using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using MakeUpServiceApi.Interface;
using MakeUpServiceApi.Models;

namespace MakeUpServiceApi.InterfaceServices
{
    public class GoogleCalendarService : IGoogleCalendarService
    {
        private readonly IConfiguration _config;
        private readonly string _calendarId = "gohxt710@gmail.com"; // Changed to primary to avoid 404 error if user calendar isn't shared
        public GoogleCalendarService(IConfiguration config)
        {
            _config = config;
        }
        public async Task<string> CreateEventAsync(Booking booking, Service service)
        {
            string relativePath = _config["GoogleSettings:ServiceAccountKeyPath"];
            string absolutePath = Path.Combine(AppContext.BaseDirectory, relativePath);
            GoogleCredential credential;
            using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
            credential = GoogleCredential.FromStream(stream)
                .CreateScoped(CalendarService.Scope.Calendar);

            var calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "TestingCalendarEvent"
            });

            // Combine Date and Time
            DateTime startDateTime = booking.AppointmentDate.Date.Add(booking.AppointmentTime);
            int durationMinutes = (service.EstimatedDurationMinutes ?? 60) * booking.Pax;
            DateTime endDateTime = startDateTime.AddMinutes(durationMinutes);

            var newEvent = new Event
            {
                Summary = $"Appointment: {service.Name} - {booking.Name}",
                Location = booking.LocationAddress,
                Description = $"Booking ID: {booking.BookingID}\nService: {service.Name}\nCustomer: {booking.Name}\nPhone: {booking.PhoneNumber}",
                Start = new EventDateTime { DateTimeDateTimeOffset = startDateTime, TimeZone = "Asia/Kuala_Lumpur" },
                End = new EventDateTime { DateTimeDateTimeOffset = endDateTime, TimeZone = "Asia/Kuala_Lumpur" }
            };
            var request = calendarService.Events.Insert(newEvent, _calendarId);
            var createdEvent = await request.ExecuteAsync();

            return createdEvent.Id;
        }
        public async Task DeleteEventAsync(string eventID)
        {
            if (string.IsNullOrWhiteSpace(eventID)) 
                throw new ArgumentException("Event ID cannot be null or empty", nameof(eventID));

            try
            {
                string relativePath = _config["GoogleSettings:ServiceAccountKeyPath"];
                string absolutePath = Path.Combine(AppContext.BaseDirectory, relativePath)  ;
                GoogleCredential credential;
                using var stream = new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
                credential = GoogleCredential.FromStream(stream)
                    .CreateScoped(CalendarService.Scope.Calendar);

                var calendarService = new CalendarService(new BaseClientService.Initializer()
                {
                    HttpClientInitializer = credential,
                    ApplicationName = "TestingCalendarEvent"
                });

                var request = calendarService.Events.Delete(_calendarId, eventID);
                await request.ExecuteAsync();
            }
            catch (Exception ex)
            {
                // Log the exception or handle it as needed
                throw new ApplicationException($"An error occurred while deleting the event with ID {eventID}: {ex.Message}", ex);
            }
        }
    }
}

