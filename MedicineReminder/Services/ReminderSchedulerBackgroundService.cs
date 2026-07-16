using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace MedicineReminder.Services;

/// <summary>
/// Runs the medicine/daily reminder checks on an internal 30-minute UTC
/// timer, for deployments (e.g. Render) that run this app as a single
/// long-lived web service instead of separate scheduled GitHub Actions
/// cron jobs. Only registered when Scheduler:Enabled is true — see
/// Program.cs's dashboard mode.
/// </summary>
public sealed class ReminderSchedulerBackgroundService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IReminderSender _reminderSender;
    private readonly ILogger<ReminderSchedulerBackgroundService> _logger;

    public ReminderSchedulerBackgroundService(IReminderSender reminderSender, ILogger<ReminderSchedulerBackgroundService> logger)
    {
        _reminderSender = reminderSender ?? throw new ArgumentNullException(nameof(reminderSender));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Reminder scheduler enabled; checking every {Minutes} minutes (UTC-aligned).", Interval.TotalMinutes);

        // Align the first tick to the next :00/:30 UTC boundary so the
        // window matches exactly what ReminderSender expects.
        await DelayUntilNextBoundaryAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(Interval);
        do
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false));
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            DateTime nowUtc = DateTime.UtcNow;
            int windowStartMinutes = nowUtc.Hour * 60 + nowUtc.Minute;
            windowStartMinutes = windowStartMinutes / 30 * 30;

            // Fires once, in the 08:00-08:30 UTC window on the 9th of every
            // month — mirrors the old GitHub Actions cron schedule "0 8 9 * *".
            if (nowUtc.Day == 9 && windowStartMinutes == 8 * 60)
            {
                await _reminderSender.SendMedicineReminderAsync(cancellationToken).ConfigureAwait(false);
            }

            await _reminderSender.SendDailyRemindersAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reminder scheduler tick failed.");
        }
    }

    private static async Task DelayUntilNextBoundaryAsync(CancellationToken cancellationToken)
    {
        DateTime now = DateTime.UtcNow;
        int minutesPastBoundary = now.Minute % 30;
        int secondsToNextBoundary = ((30 - minutesPastBoundary) % 30 * 60) - now.Second;
        if (secondsToNextBoundary <= 0)
        {
            secondsToNextBoundary += 30 * 60;
        }

        await Task.Delay(TimeSpan.FromSeconds(secondsToNextBoundary), cancellationToken).ConfigureAwait(false);
    }
}
