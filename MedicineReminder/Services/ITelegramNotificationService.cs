namespace MedicineReminder.Services;

/// <summary>
/// Sends a plain-text notification via a Telegram bot, as an optional
/// second channel alongside email. Implementations must not throw — a
/// missing/invalid Telegram configuration or a failed request should be
/// logged and swallowed so it never affects the primary email delivery.
/// </summary>
public interface ITelegramNotificationService
{
    Task SendMessageAsync(string message, CancellationToken cancellationToken = default);
}
