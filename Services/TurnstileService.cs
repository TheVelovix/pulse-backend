using System.Text.Json;

namespace pulse.Services;

public class TurnstileService
{
    private readonly HttpClient _http = new();
    private readonly string _secretKey = Environment.GetEnvironmentVariable("TURNSTILE_SECRET")!;

    public async Task<bool> VerifyTurnstile(string token)
    {
        var response = await _http.PostAsync("https://challenges.cloudflare.com/turnstile/v0/siteverify", new FormUrlEncodedContent(
            new Dictionary<string, string>
            {
                { "secret", _secretKey },
                { "response", token}
            })
        );

        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        return json.GetProperty("success").GetBoolean();
    }
}
