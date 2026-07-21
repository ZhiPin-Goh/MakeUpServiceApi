using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class IdempotencyClearService:BackgroundService
    {
        private readonly ILogger<IdempotencyClearService> _logger;
        private readonly IServiceProvider _serviceProvider;
        public IdempotencyClearService(ILogger<IdempotencyClearService> logger, IServiceProvider serviceProvider)
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
                    var cutoffDate = DateTime.UtcNow.AddDays(-1);
                    var idempotencyClean = await db.Idempotencies.Where(k => k.CreatedAt < cutoffDate).ToListAsync(_stoppingToken);
                    if (idempotencyClean.Any())
                    {
                        db.Idempotencies.RemoveRange(idempotencyClean);
                        await db.SaveChangesAsync();
                        _logger.LogInformation("Count of idempotency records cleaned up: {Count}", idempotencyClean.Count());
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while cleaning up idempotency records.");
                }
                await Task.Delay(TimeSpan.FromDays(1), _stoppingToken);
            }

        }
    }
}
