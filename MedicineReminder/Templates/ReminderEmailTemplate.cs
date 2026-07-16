using System.Net;

namespace MedicineReminder.Templates;

/// <summary>
/// Builds the HTML (with a plain-text fallback) used for every reminder
/// email sent by the application, so both the monthly medicine reminder
/// and the daily portal reminders share one consistent, styled design.
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
                  <td style="padding:6px 0; color:#6b7280; font-size:14px;">Description</td>
                  <td style="padding:6px 0; color:#111827; font-size:14px; font-weight:600; text-align:right;">{WebUtility.HtmlEncode(description)}</td>
                </tr>
                """;

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
                      <table role="presentation" width="480" cellpadding="0" cellspacing="0" style="background-color:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 2px 10px rgba(0,0,0,0.08); max-width:480px;">
                        <tr>
                          <td style="background:linear-gradient(135deg,#2563eb,#7c3aed); padding:28px 32px;">
                            <p style="margin:0; color:#ffffff; font-size:12px; letter-spacing:1.5px; text-transform:uppercase; opacity:0.85;">Medicine Reminder</p>
                            <h1 style="margin:8px 0 0; color:#ffffff; font-size:24px; font-weight:700;">{WebUtility.HtmlEncode(medicineName)}</h1>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:28px 32px 4px;">
                            <p style="margin:0 0 20px; color:#374151; font-size:15px; line-height:1.6;">
                              {WebUtility.HtmlEncode(message)}
                            </p>
                            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="border-top:1px solid #e5e7eb; padding-top:12px;">
                              {descriptionRow}
                              <tr>
                                <td style="padding:6px 0; color:#6b7280; font-size:14px;">Reminder sent on</td>
                                <td style="padding:6px 0; color:#111827; font-size:14px; font-weight:600; text-align:right;">{sentOn:dddd, dd MMMM yyyy}</td>
                              </tr>
                              <tr>
                                <td style="padding:6px 0; color:#6b7280; font-size:14px;">Medicine date</td>
                                <td style="padding:6px 0; color:#dc2626; font-size:14px; font-weight:700; text-align:right;">{scheduledDate:dddd, dd MMMM yyyy} at {scheduledDate:HH:mm} UTC</td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="padding:20px 32px 28px;">
                            <table role="presentation" width="100%" cellpadding="0" cellspacing="0" style="background-color:#eff6ff; border-radius:8px;">
                              <tr>
                                <td style="padding:14px 16px; color:#1d4ed8; font-size:13px; font-weight:600;">
                                  &#128138; Don't forget to take your medicine tomorrow!
                                </td>
                              </tr>
                            </table>
                          </td>
                        </tr>
                        <tr>
                          <td style="background-color:#f9fafb; padding:16px 32px; text-align:center;">
                            <p style="margin:0; color:#9ca3af; font-size:12px;">
                              Automated reminder sent by the MedicineReminder GitHub Action.
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
        string descriptionLine = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : $"Description: {description}\n";

        return $"""
            Hello,

            {message}

            Medicine: {medicineName}
            {descriptionLine}Reminder sent on: {sentOn:dddd, dd MMMM yyyy}
            Scheduled medicine date: {scheduledDate:dddd, dd MMMM yyyy} at {scheduledDate:HH:mm} UTC

            This is an automated reminder sent by the MedicineReminder GitHub Action.
            """;
    }
}
