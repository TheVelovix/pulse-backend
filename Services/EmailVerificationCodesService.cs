using Microsoft.EntityFrameworkCore;
using pulse.Data;
using pulse.Models;

namespace pulse.Services;

// This service checks for expired email verification codes and deletes them every 24 hours

public class EmailVerificationCodesService(IServiceScopeFactory scopeFactory, ILogger<EmailVerificationCodesService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<EmailVerificationCodesService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
                await db.EmailVerificationCodes
                    .Where(c => c.ExpiresAt < DateTime.UtcNow)
                    .ExecuteDeleteAsync(stoppingToken);
            }
            catch (Exception ex)
            {
#pragma warning disable CA1873
                _logger.LogInformation("Failed to remove expired email verification codes: {Message}", ex.Message);
#pragma warning restore CA1873
            }
            await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
        }
    }
}
