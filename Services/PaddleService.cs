using System.Text.Json;

namespace pulse.Services;

public class PaddleService
{
    private readonly HttpClient _http = new HttpClient { BaseAddress = new Uri(Environment.GetEnvironmentVariable("PADDLE_BASE_URL")!) };
    private readonly string _apiKey = Environment.GetEnvironmentVariable("PADDLE_API_KEY")!;
    public string _webhookSecret = Environment.GetEnvironmentVariable("PADDLE_WEBHOOK_SECRET")!;
    private readonly string _proPlanPriceId = Environment.GetEnvironmentVariable("PRO_PLAN_PRICE_ID")!;
    private readonly string successUrl = Environment.GetEnvironmentVariable("PADDLE_SUCCESS_URL")!;
    public async Task<string> CreateCheckoutTransaction(string userEmail, long userId)
    {
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);
        var body = new
        {
            items = new[] { new { price_id = _proPlanPriceId, quantity = 1 } },
            customer = new { email = userEmail },
            custom_data = new { user_id = userId.ToString() },
            checkout = new { url = successUrl }
        };

        var response = await _http.PostAsJsonAsync("/transactions", body);
        var rawJson = await response.Content.ReadAsStringAsync();
        var data = JsonDocument.Parse(rawJson).RootElement;
        return data.GetProperty("data").GetProperty("checkout").GetProperty("url").GetString()!;
    }
    public async Task<bool> CancelSubscription(string subscriptionId, bool immediately = false)
    {
        _http.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _apiKey);

        var response = await _http.PostAsJsonAsync($"/subscriptions/{subscriptionId}/cancel", new
        {
            effective_at = immediately ? "immediately" : "next_billing_period"
        });
        return response.IsSuccessStatusCode;
    }
}
