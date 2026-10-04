using System.Text.RegularExpressions;
using Microsoft.Extensions.Caching.Memory;

namespace pulse.Services;

public record FaviconEntry(byte[] Data, string ContentType);

public partial class FaviconService(IHttpClientFactory factory, IMemoryCache cache)
{
    [GeneratedRegex(@"^(?=.{4,253}$)([a-z0-9-]{1,63}\.)+[a-z]{2,63}$", RegexOptions.IgnoreCase)]
    private static partial Regex DomainRe();

    private const int MaxBytes = 100 * 1024;
    private static readonly string[] AllowedTypes =
        ["image/png", "image/x-icon", "image/vnd.microsoft.icon", "image/jpeg", "image/gif", "image/webp"];

    // Returns null for invalid input, true/false for hit or known miss via the out entry
    public static bool IsValidDomain(string domain) => DomainRe().IsMatch(domain);

    public async Task<FaviconEntry?> GetAsync(string domain, CancellationToken ct)
    {
        domain = domain.ToLowerInvariant();

        if (cache.TryGetValue(domain, out FaviconEntry? cached))
            return cached; // may be null, which means a cached miss

        var entry = await FetchAsync(domain, ct);
        cache.Set(
            domain,
            entry,
            new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = entry is null
                    ? TimeSpan.FromHours(6)
                    : TimeSpan.FromDays(7)
            });
        return entry;
    }

    private async Task<FaviconEntry?> FetchAsync(string domain, CancellationToken ct)
    {
        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(4));

            var client = factory.CreateClient("favicon");
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://{domain}/favicon.ico");
            req.Headers.UserAgent.ParseAdd("PulseFaviconFetcher/1.0");

            using var res = await client.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            if (!res.IsSuccessStatusCode) return null;

            var type = res.Content.Headers.ContentType?.MediaType?.ToLowerInvariant();
            if (type is null || !AllowedTypes.Contains(type)) return null;
            if (res.Content.Headers.ContentLength > MaxBytes) return null;

            await using var stream = await res.Content.ReadAsStreamAsync(timeout.Token);
            using var ms = new MemoryStream();
            var buffer = new byte[8192];
            int read;
            while ((read = await stream.ReadAsync(buffer, timeout.Token)) > 0)
            {
                if (ms.Length + read > MaxBytes) return null;
                ms.Write(buffer, 0, read);
            }

            return ms.Length == 0 ? null : new FaviconEntry(ms.ToArray(), type);
        }
        catch
        {
            return null;
        }
    }
}
