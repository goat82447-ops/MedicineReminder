using System.Net.Http.Json;
using MedicineReminder.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicineReminder.Services;

/// <inheritdoc cref="ITelegramNotificationService"/>
public sealed class TelegramNotificationService : ITelegramNotificationService
{
    private readonly HttpClient _httpClient;
    private readonly IOptions<TelegramSettings> _settings;
    private readonly ILogger<TelegramNotificationService> _logger;

    public TelegramNotificationService(HttpClient httpClient, IOptions<TelegramSettings> settings, ILogger<TelegramNotificationService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task SendMessageAsync(string message, CancellationToken cancellationToken = default)
    {
        TelegramSettings settings = _settings.Value;
        if (string.IsNullOrWhiteSpace(settings.BotToken) || string.IsNullOrWhiteSpace(settings.ChatId))
        {
            // Telegram is an optional channel; silently skip when unconfigured.
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            string url = $"https://api.telegram.org/bot{Uri.EscapeDataString(settings.BotToken)}/sendMessage";
            var payload = new { chat_id = settings.ChatId, text = message };

            using HttpResponseMessage response = await _httpClient
                .PostAsJsonAsync(url, payload, cts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram notification sent.");
            }
            else
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Telegram notification failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let a Telegram failure break email delivery — log and move on.
            _logger.LogWarning(ex, "Failed to send Telegram notification.");
        }
    }
}
