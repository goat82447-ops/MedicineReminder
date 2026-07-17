using System.ComponentModel.DataAnnotations;

namespace MedicineReminder.Models;

/// <summary>
/// A single reminder entry loaded from reminders.json (your "portal"). Add,
/// edit, or remove entries directly in that file to schedule additional
/// reminders — no code changes or redeployment required. The app checks this
/// list on a schedule and emails any reminder whose ReminderDate is today
/// and whose ReminderTime matches the current check window.
/// </summary>
public sealed class ReminderItem
{
    [Required(ErrorMessage = "Description is required.")]
    public string Description { get; set; } = string.Empty;

    [Required(ErrorMessage = "MedicineName is required.")]
    public string MedicineName { get; set; } = string.Empty;

    [Required(ErrorMessage = "ReminderMessage is required.")]
    public string ReminderMessage { get; set; } = string.Empty;

    /// <summary>
    /// The date the medicine is due. The reminder email is sent on this date.
    /// JSON format: "yyyy-MM-dd".
    /// </summary>
    public DateOnly ReminderDate { get; set; }

    /// <summary>
    /// The time of day (in UTC) the reminder email should be sent, on
    /// ReminderDate. JSON format: "HH:mm". The daily check runs every 30
    /// minutes and matches reminders whose time falls within the current
    /// 30-minute window, so actual delivery time may vary by up to ~30
    /// minutes.
    /// </summary>
    public TimeOnly ReminderTime { get; set; } = new(9, 0);

    /// <summary>
    /// Optional recipient email for this specific reminder. If left empty,
    /// falls back to the default "ReceiverEmail" configured in
    /// EmailSettings. Lets each reminder be sent to a different address
    /// instead of always using one hardcoded recipient.
    /// </summary>
    [EmailAddress(ErrorMessage = "Receiver email is not a valid email address.")]
    public string? ReceiverEmail { get; set; }

    /// <summary>
    /// Optional recipient mobile number (E.164 format, e.g. "+919876543210")
    /// for this specific reminder's SMS. If left empty, falls back to the
    /// default "ToPhoneNumber" configured in TwilioSettings. Lets each
    /// reminder's SMS be sent to a different person.
    /// </summary>
    [Phone(ErrorMessage = "Receiver phone is not a valid phone number.")]
    public string? ReceiverPhone { get; set; }

    /// <summary>
    /// Optional Telegram Chat ID (a numeric id such as "8999390672") for this
    /// specific reminder. The recipient must have pressed Start on the bot at
    /// least once. If left empty, falls back to the default "ChatId"
    /// configured in TelegramSettings. Lets each reminder's Telegram message
    /// be sent to a different person, entered directly in the dashboard.
    /// </summary>
    public string? TelegramChatId { get; set; }
}
