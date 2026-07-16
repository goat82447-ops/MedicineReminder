using System.ComponentModel.DataAnnotations;

namespace MedicineReminder.Models;

/// <summary>
/// A single reminder entry loaded from reminders.json (your "portal"). Add,
/// edit, or remove entries directly in that file to schedule additional
/// reminders — no code changes or redeployment required. The app checks this
/// list on a schedule and emails any reminder whose ReminderDate is
/// tomorrow and whose ReminderTime matches the current check window.
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
    /// The date the medicine is due. The reminder email is sent the day before.
    /// JSON format: "yyyy-MM-dd".
    /// </summary>
    public DateOnly ReminderDate { get; set; }

    /// <summary>
    /// The time of day (in UTC) the reminder email should be sent, the day
    /// before ReminderDate. JSON format: "HH:mm". The daily GitHub Action
    /// runs every 30 minutes and matches reminders whose time falls within
    /// the current 30-minute window, so actual delivery time may vary by up
    /// to ~30 minutes.
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
}
