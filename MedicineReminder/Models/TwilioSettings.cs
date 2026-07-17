namespace MedicineReminder.Models;

/// <summary>
/// Optional Twilio SMS notification settings, bound from the "Twilio"
/// section of appsettings.json / "Twilio__AccountSid" etc. environment
/// variables. When AccountSid, AuthToken, or FromPhoneNumber is unset, SMS
/// notifications are silently skipped (email/Telegram remain unaffected).
///
/// Unlike Telegram, Twilio can send to any mobile number directly — the
/// recipient does not need to take any action first.
/// </summary>
public sealed class TwilioSettings
{
    /// <summary>
    /// The Twilio Account SID (starts with "AC..."), from the Twilio Console.
    /// </summary>
    public string? AccountSid { get; set; }

    /// <summary>
    /// The Twilio Auth Token, from the Twilio Console. Treat this like a
    /// password — never commit a real value.
    /// </summary>
    public string? AuthToken { get; set; }

    /// <summary>
    /// The Twilio phone number that sends the SMS, in E.164 format
    /// (e.g. "+12025550123"). Purchased/assigned in the Twilio Console.
    /// </summary>
    public string? FromPhoneNumber { get; set; }

    /// <summary>
    /// Default recipient phone number in E.164 format (e.g. "+919876543210").
    /// Used when a reminder does not specify its own ReceiverPhone. Optional.
    /// </summary>
    public string? ToPhoneNumber { get; set; }
}
