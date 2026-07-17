namespace MedicineReminder.Services;

/// <summary>
/// Sends an SMS notification via Twilio, as an optional channel alongside
/// email and Telegram. Unlike Telegram, SMS can reach any mobile number
/// directly without the recipient taking any action first. Implementations
/// must not throw — a missing/invalid Twilio configuration or a failed
/// request should be logged and swallowed so it never affects the primary
/// email delivery.
/// </summary>
public interface ISmsNotificationService
{
    /// <summary>
    /// Sends an SMS. When <paramref name="toPhoneNumber"/> is null/empty,
    /// falls back to the default recipient configured in TwilioSettings.
    /// </summary>
    Task SendSmsAsync(string message, string? toPhoneNumber = null, CancellationToken cancellationToken = default);
}
