using MakeUpServiceApi.Models;
using Microsoft.EntityFrameworkCore;

namespace MakeUpServiceApi.BackgroundServices
{
    public class TokenClearService:BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<TokenClearService> _logger;
        public TokenClearService(IServiceProvider serviceProvider, ILogger<TokenClearService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }
        protected override async Task ExecuteAsync(CancellationToken _stoppingToken)
        {
            while (!_stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                    var cutoffDays = DateTime.Now.AddDays(-7);
                    var expiredTokens = await dbContext.TokenActivities.Where(rt => rt.CreatedAt < cutoffDays && rt.IsRevoked == true)
                        .ToListAsync(_stoppingToken);
                    if (expiredTokens.Any())
                    {
                        dbContext.TokenActivities.RemoveRange(expiredTokens);
                        await dbContext.SaveChangesAsync(_stoppingToken);
                        _logger.LogInformation("Count of expired tokens cleaned up: {Count}", expiredTokens.Count);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "An error occurred while cleaning up expired tokens.");
                }
                await Task.Delay(TimeSpan.FromDays(1), _stoppingToken);
            }
        }

    }
}
