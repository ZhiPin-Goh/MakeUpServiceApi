using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class AuditLogClearService: BackgroundService
    {
        private readonly ILogger<AuditLogClearService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public AuditLogClearService(ILogger<AuditLogClearService> logger, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }
        protected override async Task ExecuteAsync(CancellationToken _stoppingToken)
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cutoffDate = DateTime.UtcNow.AddDays(-3);
                    var oldLogs = await db.AuditLogs.Where(log => log.Timestamp < cutoffDate).ToListAsync(_stoppingToken);
                    if (oldLogs.Any())
                    {
                        db.AuditLogs.RemoveRange(oldLogs);
                        await db.SaveChangesAsync(_stoppingToken);
                        _logger.LogInformation("Count of {Count} old audit logs cleared from the database.", oldLogs.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while clearing old audit logs.");
                }
                await Task.Delay(TimeSpan.FromDays(1), _stoppingToken);
            }
            
        }
    }
}
