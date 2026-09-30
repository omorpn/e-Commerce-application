using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace e_Commerce_application.Services
{
    public record PaymentStart(bool Ok, string? AuthorizationUrl, string? Error);

    public record PaymentCheck(bool Paid, long AmountMinor, string? Currency, string? Error);

    public interface IPaymentGateway
    {
        string Name { get; }
        bool IsConfigured { get; }
        Task<PaymentStart> StartAsync(string reference, string email, decimal amount, string currency, string callbackUrl, CancellationToken ct = default);
        Task<PaymentCheck> VerifyAsync(string reference, CancellationToken ct = default);
        bool IsValidWebhook(string body, string? signature);
    }

    public class PaystackOptions
    {
        public string? SecretKey { get; set; }
        public string BaseUrl { get; set; } = "https://api.paystack.co";
    }

    // Paystack (card, bank transfer, USSD). https://paystack.com/docs/api/transaction
    public class PaystackGateway : IPaymentGateway
    {
        private readonly HttpClient _http;
        private readonly PaystackOptions _options;
        private readonly ILogger<PaystackGateway> _logger;

        public PaystackGateway(HttpClient http, IOptions<PaystackOptions> options, ILogger<PaystackGateway> logger)
        {
            _http = http;
            _options = options.Value;
            _logger = logger;
            _http.BaseAddress = new Uri(_options.BaseUrl.TrimEnd('/') + "/");
            if (IsConfigured)
            {
                _http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
            }
        }

        public string Name => "Paystack";

        public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.SecretKey);

        public static long ToMinorUnits(decimal amount) => (long)Math.Round(amount * 100, MidpointRounding.AwayFromZero);

        public async Task<PaymentStart> StartAsync(string reference, string email, decimal amount, string currency, string callbackUrl, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.PostAsJsonAsync("transaction/initialize", new
                {
                    email,
                    amount = ToMinorUnits(amount),
                    currency,
                    reference,
                    callback_url = callbackUrl
                }, ct);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = json.RootElement;
                if (response.IsSuccessStatusCode && root.TryGetProperty("status", out var status) && status.GetBoolean())
                {
                    return new PaymentStart(true, root.GetProperty("data").GetProperty("authorization_url").GetString(), null);
                }
                var message = root.TryGetProperty("message", out var m) ? m.GetString() : response.ReasonPhrase;
                _logger.LogWarning("Paystack initialize failed for {Reference}: {Message}", reference, message);
                return new PaymentStart(false, null, message);
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException)
            {
                _logger.LogError(ex, "Paystack initialize error for {Reference}", reference);
                return new PaymentStart(false, null, "The payment service could not be reached. Please try again.");
            }
        }

        public async Task<PaymentCheck> VerifyAsync(string reference, CancellationToken ct = default)
        {
            try
            {
                var response = await _http.GetAsync($"transaction/verify/{Uri.EscapeDataString(reference)}", ct);
                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
                var root = json.RootElement;
                if (!response.IsSuccessStatusCode || !root.TryGetProperty("data", out var data) || data.ValueKind != JsonValueKind.Object)
                {
                    var message = root.TryGetProperty("message", out var m) ? m.GetString() : response.ReasonPhrase;
                    return new PaymentCheck(false, 0, null, message);
                }

                var paid = data.GetProperty("status").GetString() == "success";
                var amount = data.TryGetProperty("amount", out var a) && a.ValueKind == JsonValueKind.Number ? a.GetInt64() : 0;
                var currency = data.TryGetProperty("currency", out var c) ? c.GetString() : null;
                return new PaymentCheck(paid, amount, currency, paid ? null : data.GetProperty("gateway_response").GetString());
            }
            catch (Exception ex) when (ex is HttpRequestException or JsonException or TaskCanceledException or KeyNotFoundException or InvalidOperationException)
            {
                _logger.LogError(ex, "Paystack verify error for {Reference}", reference);
                return new PaymentCheck(false, 0, null, "The payment could not be verified. Please try again.");
            }
        }

        // Paystack signs each webhook body with HMAC-SHA512 using the secret key.
        public bool IsValidWebhook(string body, string? signature)
        {
            if (!IsConfigured || string.IsNullOrEmpty(signature))
            {
                return false;
            }

            var expected = HMACSHA512.HashData(Encoding.UTF8.GetBytes(_options.SecretKey!), Encoding.UTF8.GetBytes(body));
            byte[] given;
            try
            {
                given = Convert.FromHexString(signature);
            }
            catch (FormatException)
            {
                return false;
            }
            return CryptographicOperations.FixedTimeEquals(expected, given);
        }
    }
}
