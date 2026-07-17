using System.Text;
using System.Text.Json;
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

    public async Task SendMessageAsync(string message, string? chatId = null, CancellationToken cancellationToken = default)
    {
        TelegramSettings settings = _settings.Value;

        // Per-reminder chatId overrides the default; fall back to configuration.
        string? targetChatId = string.IsNullOrWhiteSpace(chatId) ? settings.ChatId : chatId;

        if (string.IsNullOrWhiteSpace(settings.BotToken) || string.IsNullOrWhiteSpace(targetChatId))
        {
            // Telegram is an optional channel; silently skip when unconfigured.
            return;
        }

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            string url = $"https://api.telegram.org/bot{settings.BotToken}/sendMessage";

            var payload = new
            {
                chat_id = targetChatId,
                text = message,
            };

            using var content = new StringContent(
                JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using HttpResponseMessage response = await _httpClient
                .PostAsync(url, content, cts.Token)
                .ConfigureAwait(false);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Telegram message sent to chat {ChatId}.", targetChatId);
            }
            else
            {
                string body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogWarning("Telegram send failed ({StatusCode}): {Body}", (int)response.StatusCode, body);
            }
        }
        catch (Exception ex)
        {
            // Never let a Telegram failure break the primary email delivery.
            _logger.LogWarning(ex, "Failed to send Telegram message.");
        }
    }
}
