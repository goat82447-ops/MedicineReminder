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
using System.Security.Cryptography;
using System.Text;

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
//     changes needed) and emails any entry whose ReminderDate is today, at
//     the reminder's chosen time (UTC, within a 30-minute window).
//     Used for one-off, dated reminders.
//
//   --mode=dashboard
//     Runs a small web server (http://localhost:5080 by default) with a
//     page to add/view/delete reminders — writes directly to reminders.json
//     so changes just need a commit + push to take effect (local use).
//     When deployed as a long-lived host (e.g. Render), set the PORT env
//     var to bind 0.0.0.0:$PORT instead of localhost, set a
//     Dashboard__Password env var to require HTTP Basic Auth (since the
//     dashboard is then reachable over the network), and set
//     Scheduler__Enabled=true to also run the medicine/daily checks on an
//     internal 30-minute timer instead of relying on GitHub Actions cron.
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

// Telegram is an optional second notification channel — no validation, since
// it's fine for BotToken/ChatId to be unset (TelegramNotificationService
// silently skips sending in that case).
builder.Services.Configure<TelegramSettings>(builder.Configuration.GetSection(AppConfig.TelegramSection));
builder.Services.AddHttpClient<ITelegramNotificationService, TelegramNotificationService>();

// Twilio SMS is another optional channel — also unvalidated, since it's fine
// for the credentials to be unset (TwilioSmsNotificationService silently
// skips sending in that case).
builder.Services.Configure<TwilioSettings>(builder.Configuration.GetSection(AppConfig.TwilioSection));
builder.Services.AddHttpClient<ISmsNotificationService, TwilioSmsNotificationService>();

builder.Services.AddSingleton<IEmailService, EmailService>();
builder.Services.AddSingleton<IReminderRepository, ReminderRepository>();
builder.Services.AddSingleton<IReminderSender, ReminderSender>();

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

    var reminderSender = host.Services.GetRequiredService<IReminderSender>();

    bool success = mode == "medicine"
        ? await reminderSender.SendMedicineReminderAsync()
        : await reminderSender.SendDailyRemindersAsync();

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
    // PORT is set automatically by most hosting platforms (Render included).
    // Locally, it's unset, so we keep binding to localhost only.
    string? portEnv = Environment.GetEnvironmentVariable("PORT");
    bool isNetworkExposed = !string.IsNullOrWhiteSpace(portEnv);
    string dashboardUrl = isNetworkExposed ? $"http://0.0.0.0:{portEnv}" : "http://localhost:5080";

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
    webBuilder.WebHost.UseUrls(dashboardUrl);
    webBuilder.Services.AddSingleton<IReminderRepository, ReminderRepository>();

    // Only when explicitly enabled (Scheduler__Enabled=true) do we bind the
    // email settings and start the internal timer — keeps plain local
    // dashboard usage unaffected and free of any email configuration
    // requirement.
    bool schedulerEnabled = webBuilder.Configuration.GetValue<bool>($"{AppConfig.SchedulerSection}:Enabled");
    if (schedulerEnabled)
    {
        webBuilder.Services
            .AddOptions<EmailSettings>()
            .Bind(webBuilder.Configuration.GetSection(AppConfig.EmailSettingsSection))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        webBuilder.Services
            .AddOptions<MedicineSettings>()
            .Bind(webBuilder.Configuration.GetSection(AppConfig.MedicineSettingsSection))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        webBuilder.Services.Configure<TelegramSettings>(webBuilder.Configuration.GetSection(AppConfig.TelegramSection));
        webBuilder.Services.AddHttpClient<ITelegramNotificationService, TelegramNotificationService>();

        webBuilder.Services.Configure<TwilioSettings>(webBuilder.Configuration.GetSection(AppConfig.TwilioSection));
        webBuilder.Services.AddHttpClient<ISmsNotificationService, TwilioSmsNotificationService>();

        webBuilder.Services.AddSingleton<IEmailService, EmailService>();
        webBuilder.Services.AddSingleton<IReminderSender, ReminderSender>();
        webBuilder.Services.AddHostedService<ReminderSchedulerBackgroundService>();
    }

    WebApplication app = webBuilder.Build();
    var dashboardLogger = app.Services.GetRequiredService<ILogger<Program>>();
    var repository = app.Services.GetRequiredService<IReminderRepository>();

    // Require HTTP Basic Auth whenever a Dashboard__Password is configured
    // (always set this when deploying anywhere reachable over the network).
    string? dashboardPassword = app.Configuration["Dashboard:Password"];
    if (!string.IsNullOrEmpty(dashboardPassword))
    {
        string dashboardUsername = app.Configuration["Dashboard:Username"] ?? "admin";
        app.Use(async (context, next) =>
        {
            if (TryGetBasicAuthCredentials(context.Request, out string user, out string pass)
                && FixedTimeEquals(user, dashboardUsername)
                && FixedTimeEquals(pass, dashboardPassword))
            {
                await next(context);
                return;
            }

            context.Response.Headers.WWWAuthenticate = "Basic realm=\"MedicineReminder Dashboard\"";
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        });
    }
    else if (isNetworkExposed)
    {
        dashboardLogger.LogWarning(
            "Dashboard is bound to a network-reachable address but Dashboard:Password is not set — anyone with the URL can add/delete reminders.");
    }

    app.MapGet("/", async (HttpRequest request) =>
    {
        IReadOnlyList<ReminderItem> reminders = await repository.GetAllAsync();
        string? success = request.Query["added"].ToString() switch
        {
            "1" => "Reminder added. You'll be emailed on its target date.",
            _ => null,
        };
        success ??= request.Query["updated"].ToString() switch
        {
            "1" => "Reminder updated.",
            _ => null,
        };
        return Results.Content(DashboardPage.Render(reminders, success: success), "text/html");
    });

    app.MapPost("/reminders", async (HttpRequest request) =>
    {
        // Times are entered and displayed in IST in the dashboard, but stored
        // and scheduled in UTC. IST = UTC + 5:30.
        TimeSpan istOffset = TimeSpan.FromMinutes(330);

        Microsoft.AspNetCore.Http.IFormCollection form = await request.ReadFormAsync();
        string description = form["description"].ToString().Trim();
        string medicineName = form["medicineName"].ToString().Trim();
        string reminderMessage = form["reminderMessage"].ToString().Trim();
        string reminderDateRaw = form["reminderDate"].ToString().Trim();
        string reminderTimeRaw = form["reminderTime"].ToString().Trim();
        string receiverEmailRaw = form["receiverEmail"].ToString().Trim();
        string telegramChatIdRaw = form["telegramChatId"].ToString().Trim();
        string editIndexRaw = form["editIndex"].ToString().Trim();
        bool isEdit = int.TryParse(editIndexRaw, out int editIndex) && editIndex >= 0;

        bool hasValidDate = DateOnly.TryParse(reminderDateRaw, out DateOnly parsedIstDate);
        bool hasValidTime = TimeOnly.TryParse(reminderTimeRaw, out TimeOnly parsedIstTime);

        // Convert the IST date+time the user typed into the UTC date+time we
        // persist (this also correctly rolls the date back across midnight,
        // e.g. IST 02:00 -> UTC 20:30 the previous day).
        DateOnly utcDate = default;
        TimeOnly utcTime = new(9, 0);
        if (hasValidDate && hasValidTime)
        {
            DateTime utcDateTime = parsedIstDate.ToDateTime(parsedIstTime) - istOffset;
            utcDate = DateOnly.FromDateTime(utcDateTime);
            utcTime = TimeOnly.FromDateTime(utcDateTime);
        }

        var item = new ReminderItem
        {
            Description = description,
            MedicineName = medicineName,
            ReminderMessage = reminderMessage,
            ReminderDate = utcDate,
            ReminderTime = utcTime,
            ReceiverEmail = string.IsNullOrWhiteSpace(receiverEmailRaw) ? null : receiverEmailRaw,
            TelegramChatId = string.IsNullOrWhiteSpace(telegramChatIdRaw) ? null : telegramChatIdRaw,
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

        if (isEdit)
        {
            bool updated = await repository.UpdateAsync(editIndex, item);
            if (!updated)
            {
                dashboardLogger.LogWarning("Edit failed — reminder index {Index} out of range.", editIndex);
                return Results.Content(
                    DashboardPage.Render(await repository.GetAllAsync(), error: "That reminder no longer exists."),
                    "text/html");
            }

            dashboardLogger.LogInformation(
                "Dashboard updated reminder '{Description}' (index {Index}) for {ReminderDate:yyyy-MM-dd} at {ReminderTime:HH:mm} UTC.",
                description, editIndex, item.ReminderDate, item.ReminderTime);
            return Results.Redirect("/?updated=1");
        }

        await repository.AddAsync(item);
        dashboardLogger.LogInformation(
            "Dashboard added reminder '{Description}' for {ReminderDate:yyyy-MM-dd} at {ReminderTime:HH:mm} UTC.",
            description, item.ReminderDate, item.ReminderTime);

        // No email is sent here — reminders are only emailed by the scheduled
        // daily/medicine GitHub Actions (or the in-process scheduler), on
        // ReminderDate itself.
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

static bool TryGetBasicAuthCredentials(HttpRequest request, out string user, out string password)
{
    user = string.Empty;
    password = string.Empty;

    string? authHeader = request.Headers.Authorization;
    if (authHeader is null || !authHeader.StartsWith("Basic ", StringComparison.Ordinal))
    {
        return false;
    }

    try
    {
        string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authHeader["Basic ".Length..]));
        int separatorIndex = decoded.IndexOf(':');
        if (separatorIndex < 0)
        {
            return false;
        }

        user = decoded[..separatorIndex];
        password = decoded[(separatorIndex + 1)..];
        return true;
    }
    catch (FormatException)
    {
        return false;
    }
}

static bool FixedTimeEquals(string left, string right) =>
    CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));

