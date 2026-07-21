using MakeUpServiceApi.Models;

namespace MakeUpServiceApi.Interface
{
    public interface IGoogleCalendarService
    {
        Task<string> CreateEventAsync(Booking booking, Service service);
        Task DeleteEventAsync(string eventID);
    }
}
