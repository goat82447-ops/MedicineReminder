namespace MedicineReminder.Services;

/// <summary>
/// Sends a plain-text notification via a Telegram bot, as an optional
/// second channel alongside email. Implementations must not throw — a
/// missing/invalid Telegram configuration or a failed request should be
/// logged and swallowed so it never affects the primary email delivery.
/// </summary>
public interface ITelegramNotificationService
{
    /// <summary>
    /// Sends a Telegram message. When <paramref name="chatId"/> is provided it
    /// overrides the default ChatId from configuration, allowing a per-reminder
    /// recipient. When null/empty, the configured default ChatId is used.
    /// </summary>
    Task SendMessageAsync(string message, string? chatId = null, CancellationToken cancellationToken = default);
}
