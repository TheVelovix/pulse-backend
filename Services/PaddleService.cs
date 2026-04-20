using System.Text.Json;

namespace pulse.Services;

public class PaddleService
{
    private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("PADDLE_BASE_URL")!) };
    private readonly string _apiKey = Environment.GetEnvironmentVariable("PADDLE_API_KEY")!;
    public string _webhookSecret = Environment.GetEnvironmentVariable("PADDLE_WEBHOOK_SECRET")!;
    private readonly string _proPlanPriceId = Environment.GetEnvironmentVariable("PRO_PLAN_PRICE_ID")!;

    public async Task<string> CreateCheckoutTransaction(string userEmail, long userId)
    {
        var body = new
        {
            items = new[] { new { price_id = _proPlanPriceId, quantity = 1 } },
            customer = new { email = userEmail },
            custom_data = new { user_id = userId.ToString() }
        };

        var response = await _http.PostAsJsonAsync("/transactions", body);
        var data = await response.Content.ReadFromJsonAsync<JsonElement>();
        return data.GetProperty("data").GetProperty("checkout").GetProperty("url").GetString()!;
    }
}
