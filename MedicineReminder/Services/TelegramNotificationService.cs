using System.Net.Http.Headers;
using System.Text;
using MedicineReminder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicineReminder.Services;

/// <inheritdoc cref="ISmsNotificationService"/>
public sealed class TwilioSmsNotificationService : ISmsNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<TwilioSettings> _settings;
    private readonly ILogger<TwilioSmsNotificationService> _logger;

    public TwilioSmsNotificationService(HttpClient httpClient, IOptions<TwilioSettings> settings, ILogger<TwilioSmsNotificationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendSmsAsync(string message, string? toPhoneNumber = null, CancellationToken cancellationToken = default)
    {
        TwilioSettings settings = _settings.Value;
        if (string.IsNullOrWhiteSpace(settings.AccountSid)
            || string.IsNullOrWhiteSpace(settings.AuthToken)
            || string.IsNullOrWhiteSpace(settings.FromPhoneNumber))
        {
            // Twilio is an optional channel; silently skip when unconfigured.
            return;
        }

        string recipient = string.IsNullOrWhiteSpace(toPhoneNumber)
            ? settings.ToPhoneNumber ?? string.Empty
            : toPhoneNumber;

        if (string.IsNullOrWhiteSpace(recipient))
        {
            _logger.LogWarning("Twilio SMS skipped: no recipient phone number configured.");
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            string url = $"https://api.twilio.com/2010-04-01/Accounts/{Uri.EscapeDataString(settings.AccountSid)}/Messages.json";

            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            string basicAuth = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.AccountSid}:{settings.AuthToken}"));
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basicAuth);
            request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["To"] = recipient,
                ["From"] = settings.FromPhoneNumber,
                ["Body"] = message,
            });

            using HttpResponseMessage response = await _httpClient
                .SendAsync(request, cts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Twilio SMS sent to {Recipient}.", recipient);
            }
            else
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Twilio SMS failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let an SMS failure break email/Telegram delivery.
            _logger.LogWarning(ex, "Failed to send Twilio SMS.");
        }
    }
}
