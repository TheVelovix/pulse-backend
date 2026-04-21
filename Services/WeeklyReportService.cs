using pulse.Constants;
using pulse.Data;
using Microsoft.EntityFrameworkCore;
using pulse.Models;

namespace pulse.Services;

public class WeeklyReportService(IServiceScopeFactory scopeFactory, ILogger<WeeklyReportService> logger) : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<WeeklyReportService> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GenerateReportAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in WeeklyReportService");
            }
            var now = DateTime.UtcNow;
            var nextMonday = now.AddDays((7 - (int)now.DayOfWeek + 1) % 7 == 0 ? 7 : (7 - (int)now.DayOfWeek + 1) % 7);
            var nextRun = new DateTime(nextMonday.Year, nextMonday.Month, nextMonday.Day, 8, 0, 0, DateTimeKind.Utc);
            var delay = nextRun - now;
            await Task.Delay(delay, stoppingToken);
        }
    }

    private async Task GenerateReportAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        var emailService = scope.ServiceProvider.GetRequiredService<EmailService>();

        var now = DateTime.UtcNow;
        var thisWeekStart = now.AddDays(-7);
        var lastWeekStart = now.AddDays(-14);

        var proUsers = await db.Users
            .Include(u => u.Projects)
            .Where(u => u.SubscriptionPlan == Plans.Pro)
            .ToListAsync();

        foreach (var user in proUsers)
        {
            if (user.Projects.Count == 0) continue;

            var reports = new List<ProjectWeeklyReport>();

            foreach (var project in user.Projects)
            {
                var thisWeekViews = await db.PageViews
                    .Where(pv => pv.ProjectId == project.Id && pv.CreatedAt >= thisWeekStart)
                    .ToListAsync();

                var lastWeekViews = await db.PageViews
                    .Where(pv => pv.ProjectId == project.Id && pv.CreatedAt >= lastWeekStart && pv.CreatedAt < thisWeekStart)
                    .CountAsync();

                var topPages = thisWeekViews
                    .GroupBy(pv => pv.Url)
                    .Select(g => new PageStat { Url = g.Key, Count = g.Count() })
                    .OrderByDescending(p => p.Count)
                    .Take(3)
                    .ToList();

                var topReferrers = thisWeekViews
                    .Where(pv => pv.Referrer != null)
                    .GroupBy(pv => pv.Referrer)
                    .Select(g => new ReferrerStat { Referrer = g.Key, Count = g.Count() })
                    .OrderByDescending(r => r.Count)
                    .Take(3)
                    .ToList();

                reports.Add(new ProjectWeeklyReport
                {
                    ProjectName = project.Name,
                    TotalViewsThisWeek = thisWeekViews.Count,
                    TotalViewsLastWeek = lastWeekViews,
                    TopPages = topPages,
                    TopReferrers = topReferrers,
                });
            }

            try
            {
                await emailService.SendAsync(
                    user.Email,
                    $"Your Weekly Pulse Report — {now.ToString("MMM d, yyyy")}",
                    EmailTemplates.WeeklyReportEmail(user.Email, reports)
                );
                _logger.LogInformation("Weekly report sent to {Email}", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send weekly report to {Email}", user.Email);
            }
        }
    }
}
