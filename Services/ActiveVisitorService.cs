using pulse.Data;
using Microsoft.Extensions.DependencyInjection;

namespace pulse.Services;

public class ActiveVisitorService(IServiceScopeFactory scopeFactory)
{
    private readonly Dictionary<Guid, Dictionary<string, DateTime>> _activeVisitors = new();
    private readonly TimeSpan _timeout = TimeSpan.FromMinutes(1);
    private readonly Lock _lock = new();

    public void RecordHeartBeat(Guid projectId, string visitorId)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MyDbContext>();
        var exists = db.Projects.Any(p => p.Id == projectId);
        if (!exists) return;
        lock (_lock)
        {
            if (!_activeVisitors.ContainsKey(projectId))
            {
                _activeVisitors[projectId] = new Dictionary<string, DateTime>();
            }
            _activeVisitors[projectId][visitorId] = DateTime.UtcNow;
        }
    }

    public int GetActiveVisitors(Guid projectId)
    {
        lock (_lock)
        {
            if (!_activeVisitors.TryGetValue(projectId, out var visitors))
            {
                return 0;
            }
            var cutoff = DateTime.UtcNow - _timeout;
            return visitors.Values.Count(t => t >= cutoff);
        }
    }

    public void Cleanup()
    {
        lock (_lock)
        {
            var cutoff = DateTime.UtcNow - _timeout;
            foreach (var projectId in _activeVisitors.Keys)
            {
                var stale = _activeVisitors[projectId]
                    .Where(kv => kv.Value < cutoff)
                    .Select(kv => kv.Key)
                    .ToList();
                foreach (var key in stale)
                    _activeVisitors[projectId].Remove(key);
            }
        }
    }
}
