using System.Net;
using MedicineReminder.Models;

namespace MedicineReminder.Templates;

/// <summary>
/// Renders the local dashboard page (--mode=dashboard) used to view, add,
/// and delete reminders. All user-supplied values are HTML-encoded before
/// being written into the page to prevent stored XSS.
/// </summary>
public static class DashboardPage
{
    public static string Render(IReadOnlyList<ReminderItem> reminders, string? error = null, string? success = null)
    {
        // Pair each reminder with its original index (needed so the Delete
        // form posts to the correct position in reminders.json) before
        // sorting soonest-first for display.
        List<(ReminderItem Reminder, int Index)> indexed = reminders
            .Select((reminder, index) => (Reminder: reminder, Index: index))
            .OrderBy(x => x.Reminder.ReminderDate)
            .ThenBy(x => x.Reminder.ReminderTime)
            .ToList();

        string rows = indexed.Count == 0
            ? """<tr><td colspan="7" style="padding:24px; text-align:center; color:#9ca3af;">No reminders yet — add one below.</td></tr>"""
            : string.Concat(indexed.Select(x => BuildRow(x.Reminder, x.Index)));

        string errorBanner = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""<div style="background:#fee2e2; color:#991b1b; padding:12px 16px; border-radius:8px; margin-bottom:16px; font-size:14px;">⚠️ {WebUtility.HtmlEncode(error)}</div>""";

        string successBanner = string.IsNullOrWhiteSpace(success)
            ? string.Empty
            : $"""<div style="background:#dcfce7; color:#166534; padding:12px 16px; border-radius:8px; margin-bottom:16px; font-size:14px;">✅ {WebUtility.HtmlEncode(success)}</div>""";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>MedicineReminder Dashboard</title>
                <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ctext y='.9em' font-size='90'%3E%F0%9F%92%8A%3C/text%3E%3C/svg%3E" />
              </head>
              <body style="margin:0; padding:0; background-color:#f3f4f6; font-family:'Segoe UI', Roboto, Arial, sans-serif;">
                <div style="max-width:820px; margin:0 auto; padding:32px 16px;">
                  <div style="background:linear-gradient(135deg,#2563eb,#7c3aed); border-radius:14px; padding:28px 32px; margin-bottom:24px; box-shadow:0 4px 16px rgba(37,99,235,0.25);">
                    <p style="margin:0; color:#ffffff; font-size:12px; letter-spacing:1.5px; text-transform:uppercase; opacity:0.85;">💊 MedicineReminder</p>
                    <h1 style="margin:8px 0 0; color:#ffffff; font-size:28px;">Reminder Dashboard</h1>
                    <p style="margin:6px 0 0; color:#e0e7ff; font-size:13px;">Add a reminder below — you'll be emailed at the chosen time, one day before the target date (all times UTC).</p>
                  </div>

                  {successBanner}
                  {errorBanner}

                  <div style="background:#ffffff; border-radius:14px; padding:24px 28px; box-shadow:0 2px 10px rgba(0,0,0,0.06); margin-bottom:24px;">
                    <h2 style="margin:0 0 16px; font-size:18px; color:#111827;">➕ Add a reminder</h2>
                    <form method="post" action="/reminders">
                      <div style="margin-bottom:14px;">
                        <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">Description</label>
                        <input name="description" required maxlength="200" placeholder="e.g. Blood pressure tablet" style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px;" />
                      </div>
                      <div style="margin-bottom:14px;">
                        <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">Medicine name</label>
                        <input name="medicineName" required maxlength="200" placeholder="e.g. Amlodipine" style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px;" />
                      </div>
                      <div style="margin-bottom:14px;">
                        <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">Reminder message</label>
                        <textarea name="reminderMessage" required maxlength="500" rows="2" placeholder="What should the email say?" style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px; font-family:inherit; resize:vertical;"></textarea>
                      </div>
                      <div style="display:flex; gap:12px; margin-bottom:14px;">
                        <div style="flex:1;">
                          <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">Target (medicine) date</label>
                          <input type="date" name="reminderDate" required style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px;" />
                        </div>
                        <div style="flex:1;">
                          <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">Send time (UTC)</label>
                          <input type="time" name="reminderTime" required value="09:00" style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px;" />
                        </div>
                      </div>
                      <div style="margin-bottom:8px;">
                        <label style="display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px;">To email (optional)</label>
                        <input type="email" name="receiverEmail" maxlength="200" placeholder="Leave blank to use the default receiver" style="width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px;" />
                      </div>
                      <p style="margin:6px 0 16px; font-size:12px; color:#9ca3af;">You'll be emailed at this time (UTC), the day before the target date. The daily check runs every 30 minutes, so actual delivery may vary by up to ~30 minutes.</p>
                      <button type="submit" style="background:#2563eb; color:#ffffff; border:none; padding:11px 22px; border-radius:8px; font-size:14px; font-weight:600; cursor:pointer;">Add reminder</button>
                    </form>
                  </div>

                  <div style="background:#ffffff; border-radius:14px; padding:24px 28px; box-shadow:0 2px 10px rgba(0,0,0,0.06);">
                    <h2 style="margin:0 0 16px; font-size:18px; color:#111827;">📋 Upcoming reminders</h2>
                    <table style="width:100%; border-collapse:collapse; font-size:14px;">
                      <thead>
                        <tr style="text-align:left; color:#6b7280; border-bottom:2px solid #e5e7eb;">
                          <th style="padding:8px;">Date</th>
                          <th style="padding:8px;">Time (UTC)</th>
                          <th style="padding:8px;">Medicine</th>
                          <th style="padding:8px;">Description</th>
                          <th style="padding:8px;">To</th>
                          <th style="padding:8px;">Status</th>
                          <th style="padding:8px;"></th>
                        </tr>
                      </thead>
                      <tbody>
                        {rows}
                      </tbody>
                    </table>
                  </div>
                </div>
              </body>
            </html>
            """;
    }

    private static string BuildRow(ReminderItem reminder, int index)
    {
        int daysUntil = reminder.ReminderDate.DayNumber - DateOnly.FromDateTime(DateTime.Now).DayNumber;
        string badge = BuildStatusBadge(daysUntil);

        return $"""
            <tr style="border-bottom:1px solid #f3f4f6;">
              <td style="padding:10px 8px; color:#111827; font-weight:600; white-space:nowrap;">{reminder.ReminderDate:yyyy-MM-dd}</td>
              <td style="padding:10px 8px; color:#6b7280; white-space:nowrap;">{reminder.ReminderTime:HH:mm}</td>
              <td style="padding:10px 8px;">{WebUtility.HtmlEncode(reminder.MedicineName)}</td>
              <td style="padding:10px 8px; color:#6b7280;">{WebUtility.HtmlEncode(reminder.Description)}</td>
              <td style="padding:10px 8px; color:#6b7280;">{(string.IsNullOrWhiteSpace(reminder.ReceiverEmail) ? "<span style=\"color:#9ca3af;\">default</span>" : WebUtility.HtmlEncode(reminder.ReceiverEmail))}</td>
              <td style="padding:10px 8px; white-space:nowrap;">{badge}</td>
              <td style="padding:10px 8px; text-align:right;">
                <form method="post" action="/reminders/{index}/delete" onsubmit="return confirm('Delete this reminder?');">
                  <button type="submit" style="background:#fee2e2; color:#991b1b; border:none; padding:6px 12px; border-radius:6px; font-size:12px; cursor:pointer;">Delete</button>
                </form>
              </td>
            </tr>
            """;
    }

    private static string BuildStatusBadge(int daysUntil)
    {
        (string label, string background, string color) = daysUntil switch
        {
            < 0 => ("Past — missed", "#f3f4f6", "#6b7280"),
            0 => ("Due today — emails today", "#fef3c7", "#92400e"),
            1 => ("Tomorrow", "#dbeafe", "#1d4ed8"),
            <= 7 => ($"In {daysUntil} days", "#dbeafe", "#1d4ed8"),
            _ => ($"In {daysUntil} days", "#f3f4f6", "#374151"),
        };

        return $"""<span style="background:{background}; color:{color}; padding:4px 10px; border-radius:999px; font-size:12px; font-weight:600;">{label}</span>""";
    }
}
