namespace MakeUpServiceApi.Interface
{
    public interface INotificationService
    {
        Task SendNotificationAsync(int? relatedID, string title, string message, string type);
    }
}
