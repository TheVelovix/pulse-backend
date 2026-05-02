using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using pulse.Data;
using System.Net.Http.Headers;
using pulse.Constants;
using System.Text;
using pulse.Models;

namespace pulse.Controllers;

[ApiController]
[Route("api/ga-import")]
public class GaImportController(MyDbContext db, IWebHostEnvironment env, IHttpClientFactory httpClientFactory) : BaseController
{
    private readonly string _clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!;
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!;
    private readonly bool _isProduction = env.IsProduction();
    private string RedirectUri => _isProduction
        ? "https://pulse.velovix.com/api/ga-import/callback"
        : "http://localhost:5119/api/ga-import/callback";
    private readonly MyDbContext _db = db;

    [Authorize]
    [HttpGet("connect/{projectId}")]
    public IActionResult Connect(Guid projectId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var scope = "https://www.googleapis.com/auth/analytics.readonly";
        var state = $"{projectId}:{userId}";
        var url = $"https://accounts.google.com/o/oauth2/v2/auth" +
                  $"?client_id={_clientId}" +
                  $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}" +
                  $"&response_type=code" +
                  $"&scope={Uri.EscapeDataString(scope)}" +
                  $"&access_type=offline" +
                  $"&prompt=consent" +
                  $"&state={Uri.EscapeDataString(state)}";

        return Redirect(url);
    }

    [AllowAnonymous]
    [HttpGet("callback")]
    public async Task<IActionResult> Callback([FromQuery] string code, [FromQuery] string state)
    {
        var parts = state.Split(':');
        if (parts.Length != 2) return BadRequest("invalid-state");

        var projectId = Guid.Parse(parts[0]);
        var userId = long.Parse(parts[1]);

        var project = await _db.Projects.FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");

        var httpClient = httpClientFactory.CreateClient();
        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = RedirectUri,
            ["grant_type"] = "authorization_code"
        }));

        if (!tokenResponse.IsSuccessStatusCode)
        {
            var error = await tokenResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"GA token exchange failed: {error}");
            return StatusCode(500, "failed-to-exchange-code");
        }

        var json = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(json);
        var accessToken = tokenData.GetProperty("access_token").GetString();
        var refreshToken = tokenData.GetProperty("refresh_token").GetString();

        // Fetch GA4 properties
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        var accountsResponse = await httpClient.GetAsync("https://analyticsadmin.googleapis.com/v1beta/accounts");

        if (!accountsResponse.IsSuccessStatusCode)
        {
            var error = await accountsResponse.Content.ReadAsStringAsync();
            Console.WriteLine($"Failed to fetch GA4 accounts: {error}");
            return StatusCode(500, "failed-to-fetch-accounts");
        }
        var accountsJson = await accountsResponse.Content.ReadAsStringAsync();
        var accountsData = JsonSerializer.Deserialize<JsonElement>(accountsJson);

        var properties = new List<object>();
        if (accountsData.TryGetProperty("accounts", out var accounts))
        {
            foreach (var account in accounts.EnumerateArray())
            {
                var accountId = account.GetProperty("name").GetString()!.Replace("accounts/", "");
                var propertiesResponse = await httpClient.GetAsync($"https://analyticsadmin.googleapis.com/v1beta/properties?filter=parent:accounts/{accountId}");

                if (!propertiesResponse.IsSuccessStatusCode) continue;

                var propertiesJson = await propertiesResponse.Content.ReadAsStringAsync();
                var propertiesData = JsonSerializer.Deserialize<JsonElement>(propertiesJson);

                if (propertiesData.TryGetProperty("properties", out var props))
                {
                    properties.AddRange(props.EnumerateArray().Select(p => (object)new
                    {
                        Id = p.GetProperty("name").GetString()!.Replace("properties/", ""),
                        DisplayName = p.GetProperty("displayName").GetString()!
                    }));
                }
            }
        }
        var frontendUrl = _isProduction ? "https://pulse.velovix.com" : "http://localhost:3000";
        var propertiesParam = Uri.EscapeDataString(JsonSerializer.Serialize(properties));
        var accessTokenParam = Uri.EscapeDataString(accessToken!);
        var refreshTokenParam = Uri.EscapeDataString(refreshToken!);

        return Redirect($"{frontendUrl}/dashboard/project/{projectId}?ga-properties={propertiesParam}&ga-access-token={accessTokenParam}&ga-refresh-token={refreshTokenParam}");
    }

    [Authorize]
    [HttpPost("import/{projectId}")]
    public async Task<IActionResult> Import(Guid projectId, [FromBody] ImportBody body)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == userId && p.Id == projectId);
        if (project == null) return NotFound("project-not-found");

        if (project.User.SubscriptionPlan != Plans.Pro) return Forbid("pro-required");

        var httpClient = httpClientFactory.CreateClient();
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", body.AccessToken);

        var startDate = DateTime.UtcNow.AddYears(-2).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var requestBody = JsonSerializer.Serialize(new
        {
            dateRanges = new[] { new { startDate, endDate } },
            dimensions = new[]
            {
                new { name = "date" },
                new { name = "pagePath" },
                new { name = "sessionSource" },
                new { name = "deviceCategory" },
                new { name = "mobileDeviceBranding" },
                new { name = "mobileDeviceModel" },
                new { name = "browser" },
                new { name = "country" },
                new { name = "operatingSystem" },
            },

            metrics = new[] { new { name = "screenPageViews" } },
            limit = 100000
        });

        var response = await httpClient.PostAsync($"https://analyticsdata.googleapis.com/v1beta/properties/{body.PropertyId}:runReport", new StringContent(requestBody, Encoding.UTF8, "application/json"));
        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            Console.WriteLine($"GA4 data fetch failed: {error}");
            return StatusCode(500, "failed-to-fetch-ga-data");
        }
        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        if (!data.TryGetProperty("rows", out var rows)) return Ok("no-data");

        var pageViews = new List<PageView>();

        foreach (var row in rows.EnumerateArray())
        {
            var dims = row.GetProperty("dimensionValues");
            var metrics = row.GetProperty("metricValues");

            var date = DateTime.ParseExact(dims[0].GetProperty("value").GetString()!, "yyyyMMdd", null);
            var pagePath = dims[1].GetProperty("value").GetString()!;
            var source = dims[2].GetProperty("value").GetString();
            var device = dims[3].GetProperty("value").GetString();
            var deviceBrand = dims[4].GetProperty("value").GetString();
            var deviceModel = dims[5].GetProperty("value").GetString();
            var browser = dims[6].GetProperty("value").GetString();
            var country = dims[7].GetProperty("value").GetString();
            var os = dims[8].GetProperty("value").GetString();
            var count = int.Parse(metrics[0].GetProperty("value").GetString()!);

            for (int i = 0; i < count; i++)
            {
                pageViews.Add(new PageView
                {
                    ProjectId = projectId,
                    Url = pagePath,
                    Referrer = source == "(none)" || source == "(direct)" || source == "(not set)" ? null : source,
                    Device = device == "(not set)" ? null : device,
                    Browser = browser == "(not set)" ? null : browser,
                    Os = os == "(not set)" ? null : os,
                    SessionId = Guid.NewGuid().ToString(),
                    IsImported = true,
                    DeviceBrand = deviceBrand == "(not set)" ? null : deviceBrand,
                    DeviceModel = deviceModel == "(not set)" ? null : deviceModel,
                    CreatedAt = date,
                    UpdatedAt = date
                });
            }
        }
        await _db.PageViews.AddRangeAsync(pageViews);
        await _db.SaveChangesAsync();

        return Ok(new { imported = pageViews.Count });
    }
}

public class ImportBody
{
    public string AccessToken { get; set; } = string.Empty;
    public string PropertyId { get; set; } = string.Empty;
}
