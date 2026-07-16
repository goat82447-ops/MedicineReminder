namespace MedicineReminder.Services;

/// <summary>
/// Sends the two kinds of reminder emails this app supports. Shared by the
/// CLI --mode=medicine / --mode=daily entry points (invoked once per run,
/// e.g. by GitHub Actions) and by <see cref="ReminderSchedulerBackgroundService"/>
/// (invoked repeatedly on a timer when running as a long-lived host, e.g. on Render).
/// </summary>
public interface IReminderSender
{
    /// <summary>
    /// Sends the single, fixed monthly medicine reminder configured in
    /// MedicineSettings. Returns false if the send failed.
    /// </summary>
    Task<bool> SendMedicineReminderAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads reminders.json and emails any entry whose date is today and
    /// whose reminder time falls in the current 30-minute UTC window.
    /// Returns false if any individual send failed.
    /// </summary>
    Task<bool> SendDailyRemindersAsync(CancellationToken cancellationToken = default);
}
