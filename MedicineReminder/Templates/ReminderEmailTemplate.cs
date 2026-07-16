using System.Net;

namespace MedicineReminder.Templates;

/// <summary>
/// Builds the HTML (with a plain-text fallback) used for every reminder
/// email sent by the application, so both the monthly medicine reminder
/// and the daily portal reminders share one consistent, styled design.
/// Uses table-based layout with inline styles only (no &lt;style&gt; block
/// or CSS grid/flex) for maximum compatibility across email clients.
/// </summary>
public static class ReminderEmailTemplate
{
    public static string BuildHtml(
        string medicineName,
        string? description,
        string message,
        DateTime sentOn,
        DateTime scheduledDate)
    {
        string descriptionRow = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"""
                <tr>
                  <td style="padding:8px 0; color:#6b7280; font-size:13px; width:36px; vertical-align:top;">📌</td>
                  <td style="padding:8px 0; color:#6b7280; font-size:14px; vertical-align:top;">Description</td>
                  <td style="padding:8px 0; color:#111827; font-size:14px; font-weight:600; text-align:right; vertical-align:top;">{WebUtility.HtmlEncode(description)}</td>
                </tr>
                """;

        // The fixed monthly medicine reminder is sent the day before its
        // date; reminders.json entries are sent on their own date. Reflect
        // whichever is actually true instead of assuming "tomorrow".
        bool isToday = scheduledDate.Date == sentOn.Date;
        string urgencyDay = isToday ? "today" : "tomorrow";
        (string pillLabel, string pillBackground, string pillColor) = isToday
            ? ("⏰ TODAY", "#fee2e2", "#991b1b")
            : ("📆 TOMORROW", "#fef3c7", "#92400e");

        return $"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>Medicine Reminder</title>
              </head>
              <body style="margin:0; padding:0; background-color:#f3f4f6; font-family:'Segoe UI', Roboto, Arial, sans-serif;">
                <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#f3f4f6; padding:32px 0;">
                  <tr>
                    <td align="center">
                      <table role="presentation" width="520" cellpadding="0" cellspacing="0" style="background-color:#ffffff; border-radius:14px; overflow:hidden; box-shadow:0 4px 18px rgba(0,0,0,0.09); max-width:520px;">
                        <tr>
                          <td style="background:linear-gradient(135deg,#2563eb,#7c3aed); padding:32px 32px 28px;">
                            <table role="presentation" width="100%" cellpadding="0" cellspacing="0">
                              <tr>
                                <td style="vertical-align:middle;">
                                  <table role="presentation" cellpadding="0" cellspacing="0">
                                    <tr>
                                      <td style="width:52px; height:52px; background-color:rgba(255,255,255,0.18); border-radius:50%; text-align:center; vertical-align:middle; font-size:26px;">💊</td>
                                      <td style="padding-left:14px;">
                                        <p style="margin:0; color:#ffffff; font-size:11px; letter-spacing:1.5px; text-transform:uppercase; opacity:0.85;">Medicine Reminder</p>
                                        <h1 style="margin:4px 0 0; color:#ffffff; font-size:22px; font-weight:700;">{WebUtility.HtmlEncode(medicineName)}</h1>
                                      </td>
                                    </tr>
                                  </table>
                                </td>
                                <td style="text-align:right; vertical-align:middle;">
                                  <span style="display:inline-block; background:{pillBackground}; color:{pillColor}; padding:6px 14px; border-radius:999px; font-size:12px; font-weight:700; white-space:nowrap;">{pillLabel}</span>
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:28px 32px 4px;">
                            <p style="margin:0 0 20px; color:#374151; font-size:15px; line-height:1.6;">
                              {WebUtility.HtmlEncode(message)}
                            </p>
                            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb; padding-top:4px;">
                              {descriptionRow}
                              <tr>
                                <td style="padding:8px 0; color:#6b7280; font-size:13px; width:36px; vertical-align:top;">📅</td>
                                <td style="padding:8px 0; color:#6b7280; font-size:14px; vertical-align:top;">Reminder sent on</td>
                                <td style="padding:8px 0; color:#111827; font-size:14px; font-weight:600; text-align:right; vertical-align:top;">{sentOn:dddd, dd MMMM yyyy}</td>
                              </tr>
                              <tr>
                                <td style="padding:8px 0; color:#6b7280; font-size:13px; width:36px; vertical-align:top;">⏰</td>
                                <td style="padding:8px 0; color:#6b7280; font-size:14px; vertical-align:top;">Medicine date</td>
                                <td style="padding:8px 0; color:#dc2626; font-size:14px; font-weight:700; text-align:right; vertical-align:top;">{scheduledDate:dddd, dd MMMM yyyy}<br />at {scheduledDate:HH:mm} UTC</td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:20px 32px 28px;">
                            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#eff6ff; border-radius:10px; border:1px solid #dbeafe;">
                              <tr>
                                <td style="padding:16px 18px; color:#1d4ed8; font-size:13px; font-weight:600; line-height:1.5;">
                                  💊 Don't forget to take your medicine {urgencyDay}! Keep a glass of water nearby before it's time.
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="background-color:#f9fafb; padding:20px 32px; text-align:center; border-top:1px solid #f3f4f6;">
                            <p style="margin:0 0 4px; color:#6b7280; font-size:12px; font-weight:600;">💊 MedicineReminder</p>
                            <p style="margin:0; color:#9ca3af; font-size:11px;">
                              This is an automated message — please don't reply. Manage your reminders from the MedicineReminder dashboard.
                            </p>
                          </td>
                        </tr>
                      </table>
                    </td>
                  </tr>
                </table>
              </body>
            </html>
            """;
    }

    public static string BuildPlainText(
        string medicineName,
        string? description,
        string message,
        DateTime sentOn,
        DateTime scheduledDate)
    {
        bool isToday = scheduledDate.Date == sentOn.Date;
        string urgencyDay = isToday ? "today" : "tomorrow";
        string descriptionLine = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"Description : {description}\n";

        return $"""
            💊 MEDICINE REMINDER {(isToday ? "[TODAY]" : "[TOMORROW]")}
            ------------------------------------------------------

            {message}

            Medicine     : {medicineName}
            {descriptionLine}Sent on      : {sentOn:dddd, dd MMMM yyyy}
            Medicine date: {scheduledDate:dddd, dd MMMM yyyy} at {scheduledDate:HH:mm} UTC

            Don't forget to take your medicine {urgencyDay}!

            ------------------------------------------------------
            This is an automated message from MedicineReminder — please don't reply.
            """;
    }
}

