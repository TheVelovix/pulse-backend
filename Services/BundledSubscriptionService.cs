using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Models;

namespace pulse.Services;

// This service checks for expired bundled subscriptions every 24 hours
public class BundledSubscriptionService(IServiceScopeFactory scopeFactory, ILogger<BundledSubscriptionService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<BundledSubscriptionService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                await db.Users.Where(u => u.BundledSubscription != null && u.BundledSubscription.ExpiresAt < DateTime.UtcNow)
                    .ExecuteUpdateAsync(s =>
                        s.SetProperty(u => u.BundledSubscription, (BundledSubscription?)null)
                        .SetProperty(u => u.UpdatedAt, DateTime.UtcNow),
                        stoppingToken
                    );
            }
            catch (Exception ex)
            {
#pragma warning disable CA1873
                _logger.LogInformation("Failed to remove expired bundled subscriptions: {Message}", ex.Message);
#pragma warning restore CA1873
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
