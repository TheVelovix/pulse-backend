using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using pulse.Data;
using Microsoft.EntityFrameworkCore;
using pulse.Models;
using System.Text.Json;
using pulse.Constants;

namespace pulse.Controllers;

[ApiController]
[Route("api/search-console")]
public class SearchConsoleController(MyDbContext db, IWebHostEnvironment env, IHttpClientFactory httpClientFactory) : BaseController
{
    private readonly MyDbContext _db = db;
    private readonly string _clientId = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_ID")!;
    private readonly string _clientSecret = Environment.GetEnvironmentVariable("GOOGLE_CLIENT_SECRET")!;
    private readonly bool _isProduction = env.IsProduction();

    [Authorize]
    [HttpGet("connect/{projectId}")]
    public async Task<IActionResult> Connect(Guid projectId)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == userId);
        if (user == null || user.SubscriptionPlan != Plans.Pro) return Forbid("pro-plan-required");
        var scope = "https://www.googleapis.com/auth/webmasters.readonly";
        var state = $"{projectId}:{userId}";
        string redirectUri = _isProduction ? "https://pulse.velovix.com/api/search-console/callback" : "http://localhost:5119/api/search-console/callback";
        var url = $"https://accounts.google.com/o/oauth2/v2/auth?client_id={_clientId}&redirect_uri={Uri.EscapeDataString(redirectUri)}&response_type=code&scope={Uri.EscapeDataString(scope)}&access_type=offline&prompt=consent&state={Uri.EscapeDataString(state)}";
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

        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.UserId == userId && p.Id == projectId);
        if (project == null) return NotFound("project-not-found");
        if (project.User.SubscriptionPlan != Plans.Pro) return Forbid("pro-plan-required");
        // Exchange code for tokens
        var httpClient = httpClientFactory.CreateClient();
        var tokenResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["code"] = code,
            ["client_id"] = _clientId,
            ["client_secret"] = _clientSecret,
            ["redirect_uri"] = _isProduction ? "https://pulse.velovix.com/api/search-console/callback" : "http://localhost:5119/api/search-console/callback",
            ["grant_type"] = "authorization_code"
        }));
        if (!tokenResponse.IsSuccessStatusCode)
        {
            var error = await tokenResponse.Content.ReadAsStringAsync();
            return StatusCode(500, "failed-to-exchange-code");
        }

        var json = await tokenResponse.Content.ReadAsStringAsync();
        var tokenData = JsonSerializer.Deserialize<JsonElement>(json);

        var accessToken = tokenData.GetProperty("access_token").GetString()!;
        var refreshToken = tokenData.GetProperty("refresh_token").GetString()!;
        var expiresIn = tokenData.GetProperty("expires_in").GetInt32();

        var existing = await _db.SearchConsoleTokens.FirstOrDefaultAsync(t => t.ProjectId == projectId);
        if (existing != null)
        {
            existing.AccessToken = accessToken;
            existing.RefreshToken = refreshToken;
            existing.ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn);
        }
        else
        {
            _db.SearchConsoleTokens.Add(new SearchConsoleToken
            {
                ProjectId = projectId,
                AccessToken = accessToken,
                RefreshToken = refreshToken,
                ExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn)
            });
        }
        await _db.SaveChangesAsync();
        string frontendUrl = _isProduction ? "https://pulse.velovix.com" : "http://localhost:3000";
        return Redirect($"{frontendUrl}/dashboard/project/{projectId}?search-console=connected");
    }

    [Authorize]
    [HttpGet("{projectId}")]
    public async Task<IActionResult> GetSearchConsoleData(Guid projectId, [FromQuery] int? days)
    {
        var userId = GetUserId();
        if (userId == null) return Unauthorized();

        var project = await _db.Projects.Include(p => p.User).FirstOrDefaultAsync(p => p.Id == projectId && p.UserId == userId);
        if (project == null) return NotFound("project-not-found");
        if (project.User.SubscriptionPlan != Plans.Pro) return Forbid("pro-plan-required");
        var token = await _db.SearchConsoleTokens.FirstOrDefaultAsync(t => t.ProjectId == projectId);
        if (token == null) return NotFound("search-console-not-connected");

        // Refresh token if expired
        if (token.ExpiresAt <= DateTime.UtcNow)
        {
            var httpClient = httpClientFactory.CreateClient();
            var refreshResponse = await httpClient.PostAsync("https://oauth2.googleapis.com/token", new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = _clientId,
                ["client_secret"] = _clientSecret,
                ["refresh_token"] = token.RefreshToken,
                ["grant_type"] = "refresh_token"
            }));

            if (!refreshResponse.IsSuccessStatusCode) return StatusCode(500, "failed-to-refresh-token");

            var refreshJson = await refreshResponse.Content.ReadAsStringAsync();
            var refreshData = JsonSerializer.Deserialize<JsonElement>(refreshJson);
            token.AccessToken = refreshData.GetProperty("access_token").GetString()!;
            token.ExpiresAt = DateTime.UtcNow.AddSeconds(refreshData.GetProperty("expires_in").GetInt32());
            await _db.SaveChangesAsync();
        }

        var startDate = DateTime.UtcNow.AddDays(-(days ?? 28)).ToString("yyyy-MM-dd");
        var endDate = DateTime.UtcNow.ToString("yyyy-MM-dd");

        var client = httpClientFactory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);

        var requestBody = JsonSerializer.Serialize(new
        {
            startDate,
            endDate,
            dimensions = new[] { "query" },
            rowLimit = 20
        });

        var response = await client.PostAsync(
            $"https://www.googleapis.com/webmasters/v3/sites/{Uri.EscapeDataString($"sc-domain:{project.Domain}")}/searchAnalytics/query",
            new StringContent(requestBody, System.Text.Encoding.UTF8, "application/json")
        );

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            if (response.StatusCode == System.Net.HttpStatusCode.Forbidden)
                return StatusCode(403, "domain-not-verified");
            return StatusCode(500, "failed-to-fetch-search-console-data");
        }


        var json = await response.Content.ReadAsStringAsync();
        var data = JsonSerializer.Deserialize<JsonElement>(json);

        var rows = data.TryGetProperty("rows", out var rowsElement)
            ? rowsElement.EnumerateArray().Select(r => new
            {
                Query = r.GetProperty("keys")[0].GetString(),
                Clicks = r.GetProperty("clicks").GetInt32(),
                Impressions = r.GetProperty("impressions").GetInt32(),
                Ctr = Math.Round(r.GetProperty("ctr").GetDouble() * 100, 2),
                Position = Math.Round(r.GetProperty("position").GetDouble(), 1)
            }).ToList()
            : [];

        return Ok(rows);
    }
}
