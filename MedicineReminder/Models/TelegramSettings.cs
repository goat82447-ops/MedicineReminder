namespace MedicineReminder.Models;

/// <summary>
/// Optional Telegram bot notification settings, bound from the "Telegram"
/// section of appsettings.json / "Telegram__BotToken" and "Telegram__ChatId"
/// environment variables. Both are optional — when either is unset, Telegram
/// notifications are silently skipped (email remains the primary channel).
/// </summary>
public sealed class TelegramSettings
{
    /// <summary>
    /// The bot token from @BotFather (looks like "123456789:AA...").
    /// Treat this like a password — never commit a real value.
    /// </summary>
    public string? BotToken { get; set; }

    /// <summary>
    /// The destination chat ID (your personal chat with the bot, or a group
    /// chat ID). Find yours by messaging the bot once, then visiting
    /// https://api.telegram.org/bot&lt;token&gt;/getUpdates.
    /// </summary>
    public string? ChatId { get; set; }
}
