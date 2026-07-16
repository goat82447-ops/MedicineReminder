using MedicineReminder.Models;
using MedicineReminder.Infrastructure;
using MedicineReminder.Repositories;
using MedicineReminder.Services;
using MedicineReminder.Templates;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;

// ---------------------------------------------------------------------------
// MedicineReminder
// Three independent modes share this app, selected via --mode:
//
//   --mode=medicine  (.github/workflows/medicine-reminder.yml, cron: 9th of
//                      every month)
//     Sends the single, fixed medicine reminder (MedicineSettings section
//     of appsettings.json) every time it runs. Fully recurring — no date
//     bookkeeping required, because the schedule itself guarantees it only
//     fires on the 9th (one day before the 10th).
//
//   --mode=daily     (.github/workflows/daily-reminder.yml, cron: every day)
//     Loads reminders.json (the "portal" — add entries there, no code
//     changes needed) and emails any entry whose ReminderDate is tomorrow.
//     Used for one-off, dated reminders.
//
//   --mode=dashboard (local use only, not used by GitHub Actions)
//     Runs a small local web server (http://localhost:5080) with a page to
//     add/view/delete reminders — writes directly to the git-tracked
//     reminders.json so changes just need a commit + push to take effect.
// ---------------------------------------------------------------------------

string mode = ExtractMode(args);

if (mode == "dashboard")
{
    return await RunDashboardAsync(args);
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = ProjectPaths.ProjectRoot,
});

// Configuration precedence: appsettings.json -> appsettings.Local.json
// (optional, gitignored, for local secrets only — never commit real
// credentials) -> environment variables (GitHub Secrets use the
// "Section__Property" convention) -> command line (e.g. --mode=medicine).
builder.Configuration
    .AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false)
    .AddCommandLine(args);

builder.Services
    .AddOptions<EmailSettings>()
    .Bind(builder.Configuration.GetSection(AppConfig.EmailSettingsSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services
    .AddOptions<MedicineSettings>()
    .Bind(builder.Configuration.GetSection(AppConfig.MedicineSettingsSection))
    .ValidateDataAnnotations()
    .ValidateOnStart();

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IReminderRepository, ReminderRepository>();

builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options =>
{
    options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
    options.SingleLine = true;
});

using IHost host = builder.Build();

var logger = host.Services.GetRequiredService<ILogger<Program>>();

try
{
    logger.LogInformation("MedicineReminder started at {Time:u} (mode: {Mode}).", DateTimeOffset.UtcNow, mode);

    var emailService = host.Services.GetRequiredService<IEmailService>();

    bool success = mode == "medicine"
        ? await RunMedicineReminderAsync()
        : await RunDailyReminderAsync();

    if (!success)
    {
        logger.LogCritical("MedicineReminder completed with one or more failures.");
        return 1;
    }

    logger.LogInformation("MedicineReminder completed successfully.");
    return 0;
}
catch (Exception ex)
{
    logger.LogCritical(ex, "MedicineReminder failed unexpectedly.");
    return 1;
}

async Task<bool> RunMedicineReminderAsync()
{
    var medicineSettings = host.Services.GetRequiredService<IOptions<MedicineSettings>>().Value;
    var emailSettings = host.Services.GetRequiredService<IOptions<EmailSettings>>().Value;

    DateTime today = DateTime.Now;
    DateTime medicineDate = today.AddDays(1);

    string subject = $"Medicine Reminder: {medicineSettings.MedicineName}";
    string htmlBody = ReminderEmailTemplate.BuildHtml(
        medicineSettings.MedicineName, description: null, medicineSettings.ReminderMessage, today, medicineDate);
    string plainTextBody = ReminderEmailTemplate.BuildPlainText(
        medicineSettings.MedicineName, description: null, medicineSettings.ReminderMessage, today, medicineDate);

    try
    {
        var emailService = host.Services.GetRequiredService<IEmailService>();
        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await emailService.SendReminderEmailAsync(emailSettings.ReceiverEmail, subject, htmlBody, plainTextBody, cts.Token);
        logger.LogInformation("Medicine reminder email sent for {MedicineName}.", medicineSettings.MedicineName);
        return true;
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Failed to send the medicine reminder email.");
        return false;
    }
}

async Task<bool> RunDailyReminderAsync()
{
    const int windowMinutes = 30;

    var reminderRepository = host.Services.GetRequiredService<IReminderRepository>();
    var emailService = host.Services.GetRequiredService<IEmailService>();
    var emailSettings = host.Services.GetRequiredService<IOptions<EmailSettings>>().Value;

    using var loadCts = new CancellationTokenSource(TimeSpan.FromMinutes(1));
    IReadOnlyList<ReminderItem> reminders = await reminderRepository.GetAllAsync(loadCts.Token);

    // All date/time matching is done in UTC, matching the GitHub Actions
    // cron schedule (which is always UTC), so behavior is identical whether
    // run locally or in CI.
    DateTime nowUtc = DateTime.UtcNow;
    DateOnly tomorrow = DateOnly.FromDateTime(nowUtc.AddDays(1));
    int nowMinutesOfDay = nowUtc.Hour * 60 + nowUtc.Minute;
    int windowStartMinutes = nowMinutesOfDay / windowMinutes * windowMinutes;
    int windowEndMinutes = windowStartMinutes + windowMinutes;

    bool IsInCurrentWindow(TimeOnly time)
    {
        int minutesOfDay = time.Hour * 60 + time.Minute;
        return minutesOfDay >= windowStartMinutes && minutesOfDay < windowEndMinutes;
    }

    List<ReminderItem> dueReminders = reminders
        .Where(reminder => reminder.ReminderDate == tomorrow
            && IsValid(reminder, logger)
            && IsInCurrentWindow(reminder.ReminderTime))
        .ToList();

    if (dueReminders.Count == 0)
    {
        logger.LogInformation(
            "No reminders due for {Tomorrow:yyyy-MM-dd} in the {WindowStart}-{WindowEnd} UTC window. Nothing to send.",
            tomorrow,
            $"{windowStartMinutes / 60:D2}:{windowStartMinutes % 60:D2}",
            $"{windowEndMinutes / 60:D2}:{windowEndMinutes % 60:D2}");
        return true;
    }

    logger.LogInformation("{Count} reminder(s) due for {Tomorrow:yyyy-MM-dd}.", dueReminders.Count, tomorrow);

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

            // Guard against a hung connection so the GitHub Actions job fails
            // fast instead of running until the workflow timeout.
            using var sendCts = new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await emailService.SendReminderEmailAsync(toEmail, subject, htmlBody, plainTextBody, sendCts.Token);

            logger.LogInformation("Reminder email sent to {ToEmail}: {Description}", toEmail, reminder.Description);
        }
        catch (Exception ex)
        {
            allSucceeded = false;
            logger.LogError(ex, "Failed to send reminder: {Description}", reminder.Description);
        }
    }

    return allSucceeded;
}

static bool IsValid(ReminderItem reminder, ILogger logger)
{
    var context = new ValidationContext(reminder);
    var results = new List<ValidationResult>();
    bool isValid = Validator.TryValidateObject(reminder, context, results, validateAllProperties: true);

    if (!isValid)
    {
        logger.LogWarning(
            "Skipping invalid reminder ({Description}): {Errors}",
            reminder.Description,
            string.Join("; ", results.Select(r => r.ErrorMessage)));
    }

    return isValid;
}

static string ExtractMode(string[] rawArgs)
{
    const string prefix = "--mode=";

    foreach (string arg in rawArgs)
    {
        if (arg.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return arg[prefix.Length..].Trim().ToLowerInvariant();
        }
    }

    return "daily";
}

async Task<int> RunDashboardAsync(string[] rawArgs)
{
    const string dashboardUrl = "http://localhost:5080";

    var webAppOptions = new WebApplicationOptions
    {
        Args = rawArgs,
        ContentRootPath = ProjectPaths.ProjectRoot,
    };
    WebApplicationBuilder webBuilder = WebApplication.CreateBuilder(webAppOptions);
    webBuilder.Logging.ClearProviders();
    webBuilder.Logging.AddSimpleConsole(options =>
    {
        options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
        options.SingleLine = true;
    });
    // Same config precedence as the other modes: appsettings.json ->
    // appsettings.Local.json (optional, gitignored) -> environment variables.
    webBuilder.Configuration.AddJsonFile("appsettings.Local.json", optional: true, reloadOnChange: false);
    // Bind to localhost only — this dashboard is a personal, local-only tool
    // and is never exposed to the network.
    webBuilder.WebHost.UseUrls(dashboardUrl);
    webBuilder.Services.AddSingleton<IReminderRepository, ReminderRepository>();

    WebApplication app = webBuilder.Build();
    var dashboardLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var repository = app.Services.GetRequiredService<IReminderRepository>();

    app.MapGet("/", async (HttpRequest request) =>
    {
        IReadOnlyList<ReminderItem> reminders = await repository.GetAllAsync();
        string? success = request.Query["added"].ToString() switch
        {
            "1" => "Reminder added. You'll be emailed the day before its target date.",
            _ => null,
        };
        return Results.Content(DashboardPage.Render(reminders, success: success), "text/html");
    });

    app.MapPost("/reminders", async (HttpRequest request) =>
    {
        Microsoft.AspNetCore.Http.IFormCollection form = await request.ReadFormAsync();
        string description = form["description"].ToString().Trim();
        string medicineName = form["medicineName"].ToString().Trim();
        string reminderMessage = form["reminderMessage"].ToString().Trim();
        string reminderDateRaw = form["reminderDate"].ToString().Trim();
        string reminderTimeRaw = form["reminderTime"].ToString().Trim();
        string receiverEmailRaw = form["receiverEmail"].ToString().Trim();

        bool hasValidDate = DateOnly.TryParse(reminderDateRaw, out DateOnly parsedDate);
        bool hasValidTime = TimeOnly.TryParse(reminderTimeRaw, out TimeOnly parsedTime);

        var item = new ReminderItem
        {
            Description = description,
            MedicineName = medicineName,
            ReminderMessage = reminderMessage,
            ReminderDate = hasValidDate ? parsedDate : default,
            ReminderTime = hasValidTime ? parsedTime : new TimeOnly(9, 0),
            ReceiverEmail = string.IsNullOrWhiteSpace(receiverEmailRaw) ? null : receiverEmailRaw,
        };

        var validationContext = new ValidationContext(item);
        var validationResults = new List<ValidationResult>();
        bool isValid = hasValidDate && hasValidTime
            && Validator.TryValidateObject(item, validationContext, validationResults, validateAllProperties: true);

        if (!isValid)
        {
            dashboardLogger.LogWarning("Rejected invalid dashboard reminder submission for '{Description}'.", description);
            return Results.Content(
                DashboardPage.Render(await repository.GetAllAsync(), error: "Please fill in all fields with a valid date and time."),
                "text/html");
        }

        await repository.AddAsync(item);
        dashboardLogger.LogInformation(
            "Dashboard added reminder '{Description}' for {ReminderDate:yyyy-MM-dd} at {ReminderTime:HH:mm} UTC.",
            description, item.ReminderDate, item.ReminderTime);

        // No email is sent here — reminders are only emailed by the scheduled
        // daily/medicine GitHub Actions, the day before ReminderDate.
        return Results.Redirect("/?added=1");
    });

    app.MapPost("/reminders/{index:int}/delete", async (int index) =>
    {
        bool removed = await repository.RemoveAsync(index);
        if (removed)
        {
            dashboardLogger.LogInformation("Dashboard deleted reminder at index {Index}.", index);
        }

        return Results.Redirect("/");
    });

    // Avoid noisy 404 log lines for the browser's automatic favicon request
    // (the page already serves an inline SVG favicon via a <link> tag).
    app.MapGet("/favicon.ico", () => Results.NoContent());

    dashboardLogger.LogInformation("MedicineReminder dashboard running at {Url} — press Ctrl+C to stop.", dashboardUrl);
    await app.RunAsync();
    return 0;
}

