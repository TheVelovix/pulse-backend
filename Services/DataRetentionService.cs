using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Constants;

namespace pulse.Services;

public class DataRetentionService(IServiceScopeFactory scopeFactory, ILogger<DataRetentionService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<DataRetentionService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await CleanUpOldData();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Data Retention Cleanup Failed");
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }

    private async Task CleanUpOldData()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();

        var freeRetention = DateTime.UtcNow.AddDays(-Plans.RetentionDays[Plans.Free]);
        var proRetention = DateTime.UtcNow.AddDays(-Plans.RetentionDays[Plans.Pro]);

        var freeDeleted = await db.PageViews
            .Where(p => p.Project.User.SubscriptionPlan == Plans.Free && p.CreatedAt < freeRetention)
            .ExecuteDeleteAsync();
        var proDeleted = await db.PageViews
            .Where(p =>
                (p.Project.User.SubscriptionPlan == Plans.Pro ||
                    (p.Project.User.BundledSubscription != null && p.Project.User.BundledSubscription.Plan == Plans.Pro && p.Project.User.BundledSubscription.ExpiresAt > DateTime.UtcNow)
                )
                && p.CreatedAt < proRetention)
            .ExecuteDeleteAsync();
#pragma warning disable CA1873
        _logger.LogInformation("Data retention cleanup: {FreeDeleted} free records, {ProDeleted} pro records", freeDeleted, proDeleted);
#pragma warning restore CA1873
    }
}
