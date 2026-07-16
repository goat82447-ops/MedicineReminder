using System.ComponentModel.DataAnnotations;
using MedicineReminder.Models;
using MedicineReminder.Repositories;
using MedicineReminder.Templates;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace MedicineReminder.Services;

/// <inheritdoc cref="IReminderSender"/>
public sealed class ReminderSender : IReminderSender
{
    private const int DailyWindowMinutes = 30;

    private readonly IOptions<MedicineSettings> _medicineSettings;
    private readonly IOptions<EmailSettings> _emailSettings;
    private readonly IReminderRepository _reminderRepository;
    private readonly IEmailService _emailService;
    private readonly ILogger<ReminderSender> _logger;

    public ReminderSender(
        IOptions<MedicineSettings> medicineSettings,
        IOptions<EmailSettings> emailSettings,
        IReminderRepository reminderRepository,
        IEmailService emailService,
        ILogger<ReminderSender> logger)
    {
        _medicineSettings = medicineSettings ?? throw new ArgumentNullException(nameof(medicineSettings));
        _emailSettings = emailSettings ?? throw new ArgumentNullException(nameof(emailSettings));
        _reminderRepository = reminderRepository ?? throw new ArgumentNullException(nameof(reminderRepository));
        _emailService = emailService ?? throw new ArgumentNullException(nameof(emailService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> SendMedicineReminderAsync(CancellationToken cancellationToken = default)
    {
        MedicineSettings medicineSettings = _medicineSettings.Value;
        EmailSettings emailSettings = _emailSettings.Value;

        DateTime today = DateTime.Now;
        DateTime medicineDate = today.AddDays(1);

        string subject = $"Medicine Reminder: {medicineSettings.MedicineName}";
        string htmlBody = ReminderEmailTemplate.BuildHtml(
            medicineSettings.MedicineName, description: null, medicineSettings.ReminderMessage, today, medicineDate);
        string plainTextBody = ReminderEmailTemplate.BuildPlainText(
            medicineSettings.MedicineName, description: null, medicineSettings.ReminderMessage, today, medicineDate);

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromMinutes(2));
            await _emailService.SendReminderEmailAsync(emailSettings.ReceiverEmail, subject, htmlBody, plainTextBody, cts.Token);
            _logger.LogInformation("Medicine reminder email sent for {MedicineName}.", medicineSettings.MedicineName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send the medicine reminder email.");
            return false;
        }
    }

    public async Task<bool> SendDailyRemindersAsync(CancellationToken cancellationToken = default)
    {
        EmailSettings emailSettings = _emailSettings.Value;

        using var loadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        loadCts.CancelAfter(TimeSpan.FromMinutes(1));
        IReadOnlyList<ReminderItem> reminders = await _reminderRepository.GetAllAsync(loadCts.Token).ConfigureAwait(false);

        // All date/time matching is done in UTC, matching the GitHub Actions
        // cron schedule (which is always UTC), so behavior is identical
        // whether run via CLI, GitHub Actions, or the in-process scheduler.
        // Reminders are emailed on their own ReminderDate (not the day
        // before), at the reminder's chosen time, accurate to within the
        // current 30-minute window.
        DateTime nowUtc = DateTime.UtcNow;
        DateOnly today = DateOnly.FromDateTime(nowUtc);
        int nowMinutesOfDay = nowUtc.Hour * 60 + nowUtc.Minute;
        int windowStartMinutes = nowMinutesOfDay / DailyWindowMinutes * DailyWindowMinutes;
        int windowEndMinutes = windowStartMinutes + DailyWindowMinutes;

        bool IsInCurrentWindow(TimeOnly time)
        {
            int minutesOfDay = time.Hour * 60 + time.Minute;
            return minutesOfDay >= windowStartMinutes && minutesOfDay < windowEndMinutes;
        }

        List<ReminderItem> dueReminders = reminders
            .Where(reminder => reminder.ReminderDate == today
                && IsValid(reminder)
                && IsInCurrentWindow(reminder.ReminderTime))
            .ToList();

        if (dueReminders.Count == 0)
        {
            _logger.LogInformation(
                "No reminders due for {Today:yyyy-MM-dd} in the {WindowStart}-{WindowEnd} UTC window. Nothing to send.",
                today,
                $"{windowStartMinutes / 60:D2}:{windowStartMinutes % 60:D2}",
                $"{windowEndMinutes / 60:D2}:{windowEndMinutes % 60:D2}");
            return true;
        }

        _logger.LogInformation("{Count} reminder(s) due for {Today:yyyy-MM-dd}.", dueReminders.Count, today);

        bool allSucceeded = true;

        foreach (ReminderItem reminder in dueReminders)
        {
            try
            {
                string toEmail = string.IsNullOrWhiteSpace(reminder.ReceiverEmail)
                    ? emailSettings.ReceiverEmail
                    : reminder.ReceiverEmail;
                string subject = $"Medicine Reminder: {reminder.MedicineName}";
                DateTime sentOn = DateTime.Now;
                DateTime scheduledDate = reminder.ReminderDate.ToDateTime(reminder.ReminderTime);
                string htmlBody = ReminderEmailTemplate.BuildHtml(
                    reminder.MedicineName, reminder.Description, reminder.ReminderMessage, sentOn, scheduledDate);
                string plainTextBody = ReminderEmailTemplate.BuildPlainText(
                    reminder.MedicineName, reminder.Description, reminder.ReminderMessage, sentOn, scheduledDate);

                // Guard against a hung connection so the caller (a GitHub
                // Actions job or the in-process scheduler) fails/recovers
                // fast instead of hanging indefinitely.
                using var sendCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                sendCts.CancelAfter(TimeSpan.FromMinutes(2));
                await _emailService.SendReminderEmailAsync(toEmail, subject, htmlBody, plainTextBody, sendCts.Token);

                _logger.LogInformation("Reminder email sent to {ToEmail}: {Description}", toEmail, reminder.Description);
            }
            catch (Exception ex)
            {
                allSucceeded = false;
                _logger.LogError(ex, "Failed to send reminder: {Description}", reminder.Description);
            }
        }

        return allSucceeded;
    }

    private bool IsValid(ReminderItem reminder)
    {
        var context = new ValidationContext(reminder);
        var results = new List<ValidationResult>();
        bool isValid = Validator.TryValidateObject(reminder, context, results, validateAllProperties: true);

        if (!isValid)
        {
            _logger.LogWarning(
                "Skipping invalid reminder ({Description}): {Errors}",
                reminder.Description,
                string.Join("; ", results.Select(r => r.ErrorMessage)));
        }

        return isValid;
    }
}
