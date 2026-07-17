using System.Net;
using MedicineReminder.Models;

namespace MedicineReminder.Templates;

/// <summary>
/// Renders the dashboard page (--mode=dashboard) used to view, add, and
/// delete reminders. All user-supplied values are HTML-encoded before being
/// written into the page to prevent stored XSS. Enhancements (search,
/// quick time presets, live local-time preview, medicine autocomplete) are
/// implemented as small progressive-enhancement inline scripts — the page
/// still works with JavaScript disabled.
/// </summary>
public static class DashboardPage
{
    // Plain (non-interpolated) raw strings so the CSS/JS braces below don't
    // need escaping — only the small dynamic pieces use $"""...""" further down.
    private const string StyleBlock = """
        <style>
          * { box-sizing: border-box; }
          body { margin:0; padding:0; background-color:#f3f4f6; font-family:'Segoe UI', Roboto, Arial, sans-serif; }
          .card { background:#ffffff; border-radius:14px; box-shadow:0 2px 10px rgba(0,0,0,0.06); transition:box-shadow .2s ease; }
          .card:hover { box-shadow:0 6px 20px rgba(0,0,0,0.09); }
          input, textarea { width:100%; box-sizing:border-box; padding:10px 12px; border:1px solid #d1d5db; border-radius:8px; font-size:14px; font-family:inherit; transition:border-color .15s ease, box-shadow .15s ease; }
          input:focus, textarea:focus { outline:none; border-color:#2563eb; box-shadow:0 0 0 3px rgba(37,99,235,0.15); }
          label.field-label { display:block; font-size:13px; font-weight:600; color:#374151; margin-bottom:4px; }
          button { cursor:pointer; transition:transform .1s ease, background-color .15s ease, box-shadow .15s ease; }
          button:active { transform:scale(0.98); }
          .btn-primary { background:#2563eb; color:#fff; border:none; padding:12px 24px; border-radius:8px; font-size:14px; font-weight:600; box-shadow:0 2px 6px rgba(37,99,235,0.35); }
          .btn-primary:hover { background:#1d4ed8; }
          .chip { background:#eef2ff; color:#3730a3; border:1px solid #e0e7ff; padding:6px 12px; border-radius:999px; font-size:12px; font-weight:600; }
          .chip:hover { background:#e0e7ff; }
          .icon-btn { background:#fee2e2; color:#991b1b; border:none; padding:7px 14px; border-radius:6px; font-size:12px; font-weight:600; }
          .icon-btn:hover { background:#fecaca; }
          table.reminders { width:100%; border-collapse:collapse; font-size:14px; }
          table.reminders tbody tr { border-bottom:1px solid #f3f4f6; transition:background-color .1s ease; }
          table.reminders tbody tr:hover { background-color:#f9fafb; }
          .banner { padding:12px 16px; border-radius:8px; margin-bottom:16px; font-size:14px; animation:slideIn .25s ease; }
          .banner-error { background:#fee2e2; color:#991b1b; }
          .banner-success { background:#dcfce7; color:#166534; }
          @keyframes slideIn { from { opacity:0; transform:translateY(-6px); } to { opacity:1; transform:translateY(0); } }
          @media (max-width: 700px) {
            .form-grid { grid-template-columns:1fr !important; }
            .stats-grid { grid-template-columns:1fr 1fr !important; }
            table.reminders thead { display:none; }
            table.reminders, table.reminders tbody, table.reminders tr, table.reminders td { display:block; width:100%; }
            table.reminders tr { padding:12px 8px; border-bottom:8px solid #f3f4f6; }
            table.reminders td { padding:4px 0; text-align:left !important; }
            table.reminders td[data-label]::before { content: attr(data-label); display:block; font-size:11px; font-weight:700; color:#9ca3af; text-transform:uppercase; letter-spacing:0.5px; }
          }
        </style>
        """;

    private const string ScriptBlock = """
        <script>
          function setTime(value) {
            document.getElementById('reminderTime').value = value;
            updateLocalPreview();
          }

          function pad(n) { return String(n).padStart(2, '0'); }

          function suggestNextRun() {
            var now = new Date();
            var thirtyMinMs = 30 * 60 * 1000;
            var next = new Date(Math.ceil(now.getTime() / thirtyMinMs) * thirtyMinMs + 5 * 60 * 1000);
            document.getElementById('reminderDate').value =
              next.getUTCFullYear() + '-' + pad(next.getUTCMonth() + 1) + '-' + pad(next.getUTCDate());
            document.getElementById('reminderTime').value = pad(next.getUTCHours()) + ':' + pad(next.getUTCMinutes());
            updateLocalPreview();
          }

          function updateLocalPreview() {
            var dateVal = document.getElementById('reminderDate').value;
            var timeVal = document.getElementById('reminderTime').value;
            var preview = document.getElementById('localPreview');
            if (!dateVal || !timeVal) { preview.textContent = ''; return; }
            var utcDate = new Date(dateVal + 'T' + timeVal + ':00Z');
            if (isNaN(utcDate.getTime())) { preview.textContent = ''; return; }
            preview.textContent = 'Your local time: ' + utcDate.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
          }

          function tickClock() {
            var now = new Date();
            var utcStr = pad(now.getUTCHours()) + ':' + pad(now.getUTCMinutes()) + ' UTC';
            var localStr = now.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' }) + ' local';
            var clock = document.getElementById('clock');
            if (clock) { clock.textContent = utcStr + '   \u2022   ' + localStr; }
          }

          function renderLocalTimes() {
            document.querySelectorAll('.local-time[data-utc]').forEach(function (cell) {
              var utcDate = new Date(cell.getAttribute('data-utc'));
              if (!isNaN(utcDate.getTime())) {
                cell.textContent = utcDate.toLocaleString(undefined, { dateStyle: 'medium', timeStyle: 'short' });
              }
            });
          }

          function filterRows() {
            var query = document.getElementById('searchBox').value.trim().toLowerCase();
            document.querySelectorAll('#reminderRows tr[data-search]').forEach(function (row) {
              var match = row.getAttribute('data-search').indexOf(query) !== -1;
              row.style.display = match ? '' : 'none';
            });
          }

          tickClock();
          setInterval(tickClock, 1000);
          renderLocalTimes();
        </script>
        """;

    public static string Render(IReadOnlyList<ReminderItem> reminders, string? error = null, string? success = null)
    {
        DateOnly today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Pair each reminder with its original index (needed so the Delete
        // form posts to the correct position in reminders.json) before
        // sorting soonest-first for display.
        List<(ReminderItem Reminder, int Index)> indexed = reminders
            .Select((reminder, index) => (Reminder: reminder, Index: index))
            .OrderBy(x => x.Reminder.ReminderDate)
            .ThenBy(x => x.Reminder.ReminderTime)
            .ToList();

        int dueTodayCount = reminders.Count(r => r.ReminderDate == today);
        int dueThisWeekCount = reminders.Count(r => r.ReminderDate >= today && r.ReminderDate <= today.AddDays(7));

        string rows = indexed.Count == 0
            ? """
                <tr>
                  <td colspan="9" style="padding:40px 16px; text-align:center; color:#9ca3af;">
                    <div style="font-size:40px; margin-bottom:8px;">🗓️</div>
                    No reminders yet — add one below to get started.
                  </td>
                </tr>
                """
            : string.Concat(indexed.Select(x => BuildRow(x.Reminder, x.Index, today)));

        string medicineOptions = string.Concat(reminders
            .Select(r => r.MedicineName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(name => $"""<option value="{WebUtility.HtmlEncode(name)}"></option>"""));

        string errorBanner = string.IsNullOrWhiteSpace(error)
            ? string.Empty
            : $"""<div class="banner banner-error">⚠️ {WebUtility.HtmlEncode(error)}</div>""";

        string successBanner = string.IsNullOrWhiteSpace(success)
            ? string.Empty
            : $"""<div class="banner banner-success">✅ {WebUtility.HtmlEncode(success)}</div>""";

        return $"""
            <!DOCTYPE html>
            <html lang="en">
              <head>
                <meta charset="utf-8" />
                <meta name="viewport" content="width=device-width, initial-scale=1.0" />
                <title>MedicineReminder Dashboard</title>
                <link rel="icon" href="data:image/svg+xml,%3Csvg xmlns='http://www.w3.org/2000/svg' viewBox='0 0 100 100'%3E%3Ctext y='.9em' font-size='90'%3E%F0%9F%92%8A%3C/text%3E%3C/svg%3E" />
                {StyleBlock}
              </head>
              <body>
                <div style="max-width:900px; margin:0 auto; padding:32px 16px;">
                  <div style="background:linear-gradient(135deg,#2563eb,#7c3aed); border-radius:14px; padding:28px 32px; margin-bottom:20px; box-shadow:0 4px 16px rgba(37,99,235,0.25);">
                    <div style="display:flex; justify-content:space-between; align-items:flex-start; flex-wrap:wrap; gap:12px;">
                      <div>
                        <p style="margin:0; color:#ffffff; font-size:12px; letter-spacing:1.5px; text-transform:uppercase; opacity:0.85;">💊 MedicineReminder</p>
                        <h1 style="margin:8px 0 0; color:#ffffff; font-size:28px;">Reminder Dashboard</h1>
                        <p style="margin:6px 0 0; color:#e0e7ff; font-size:13px;">You'll be emailed at the chosen time, on the target date itself (all times UTC).</p>
                      </div>
                      <div id="clock" style="color:#e0e7ff; font-size:13px; font-family:'Consolas','Courier New',monospace; text-align:right; white-space:nowrap;">—</div>
                    </div>
                  </div>

                  <div class="stats-grid" style="display:grid; grid-template-columns:repeat(3,1fr); gap:14px; margin-bottom:20px;">
                    <div class="card" style="padding:16px 20px;">
                      <p style="margin:0; color:#6b7280; font-size:12px; font-weight:600; text-transform:uppercase;">Total reminders</p>
                      <p style="margin:6px 0 0; color:#111827; font-size:26px; font-weight:700;">{indexed.Count}</p>
                    </div>
                    <div class="card" style="padding:16px 20px;">
                      <p style="margin:0; color:#6b7280; font-size:12px; font-weight:600; text-transform:uppercase;">Due today</p>
                      <p style="margin:6px 0 0; color:#dc2626; font-size:26px; font-weight:700;">{dueTodayCount}</p>
                    </div>
                    <div class="card" style="padding:16px 20px;">
                      <p style="margin:0; color:#6b7280; font-size:12px; font-weight:600; text-transform:uppercase;">Next 7 days</p>
                      <p style="margin:6px 0 0; color:#1d4ed8; font-size:26px; font-weight:700;">{dueThisWeekCount}</p>
                    </div>
                  </div>

                  {successBanner}
                  {errorBanner}

                  <div class="card" style="padding:24px 28px; margin-bottom:20px;">
                    <h2 style="margin:0 0 16px; font-size:18px; color:#111827;">➕ Add a reminder</h2>
                    <form method="post" action="/reminders" id="addForm">
                      <div class="form-grid" style="display:grid; grid-template-columns:1fr 1fr; gap:14px; margin-bottom:14px;">
                        <div>
                          <label class="field-label">📝 Description</label>
                          <input name="description" required maxlength="200" placeholder="e.g. Blood pressure tablet" />
                        </div>
                        <div>
                          <label class="field-label">💊 Medicine name</label>
                          <input name="medicineName" required maxlength="200" placeholder="e.g. Amlodipine" list="medicineList" />
                          <datalist id="medicineList">{medicineOptions}</datalist>
                        </div>
                      </div>
                      <div style="margin-bottom:14px;">
                        <label class="field-label">💬 Reminder message</label>
                        <textarea name="reminderMessage" required maxlength="500" rows="2" placeholder="What should the email say?"></textarea>
                      </div>
                      <div class="form-grid" style="display:grid; grid-template-columns:1fr 1fr; gap:14px; margin-bottom:8px;">
                        <div>
                          <label class="field-label">📅 Target date</label>
                          <input type="date" name="reminderDate" id="reminderDate" required onchange="updateLocalPreview()" />
                        </div>
                        <div>
                          <label class="field-label">⏰ Send time (UTC)</label>
                          <input type="time" name="reminderTime" id="reminderTime" required value="09:00" onchange="updateLocalPreview()" />
                        </div>
                      </div>
                      <div style="display:flex; flex-wrap:wrap; gap:8px; margin-bottom:8px;">
                        <button type="button" class="chip" onclick="setTime('08:00')">🌅 Morning 08:00</button>
                        <button type="button" class="chip" onclick="setTime('14:00')">☀️ Afternoon 14:00</button>
                        <button type="button" class="chip" onclick="setTime('20:00')">🌙 Evening 20:00</button>
                        <button type="button" class="chip" onclick="suggestNextRun()">⚡ Next available run</button>
                      </div>
                      <p id="localPreview" style="margin:0 0 14px; font-size:12px; color:#2563eb; font-weight:600; min-height:16px;"></p>
                      <div style="margin-bottom:8px;">
                        <label class="field-label">✉️ To email (optional)</label>
                        <input type="email" name="receiverEmail" maxlength="200" placeholder="Leave blank to use the default receiver" />
                      </div>
                      <div style="margin-bottom:8px;">
                        <label class="field-label">📨 Telegram Chat ID (optional)</label>
                        <input type="text" name="telegramChatId" maxlength="64" inputmode="numeric" placeholder="e.g. 8999390672 — the person must Start your bot first" />
                      </div>
                      <p style="margin:6px 0 16px; font-size:12px; color:#9ca3af;">
                        Delivery is checked every 30 minutes, so actual send time may vary by up to ~30 minutes from what you pick.
                      </p>
                      <button type="submit" class="btn-primary">Add reminder</button>
                    </form>
                  </div>

                  <div class="card" style="padding:24px 28px;">
                    <div style="display:flex; justify-content:space-between; align-items:center; flex-wrap:wrap; gap:10px; margin-bottom:16px;">
                      <h2 style="margin:0; font-size:18px; color:#111827;">📋 Upcoming reminders</h2>
                      <input type="search" id="searchBox" placeholder="🔎 Filter by medicine, description or email…" oninput="filterRows()" style="max-width:260px;" />
                    </div>
                    <div style="overflow-x:auto;">
                      <table class="reminders">
                        <thead>
                          <tr style="text-align:left; color:#6b7280; border-bottom:2px solid #e5e7eb;">
                            <th style="padding:8px;">Date</th>
                            <th style="padding:8px;">Time (UTC)</th>
                            <th style="padding:8px;">Your local time</th>
                            <th style="padding:8px;">Medicine</th>
                            <th style="padding:8px;">Description</th>
                            <th style="padding:8px;">To</th>
                            <th style="padding:8px;">Telegram</th>
                            <th style="padding:8px;">Status</th>
                            <th style="padding:8px;"></th>
                          </tr>
                        </thead>
                        <tbody id="reminderRows">
                          {rows}
                        </tbody>
                      </table>
                    </div>
                  </div>

                  <p style="text-align:center; color:#9ca3af; font-size:12px; margin-top:24px;">💊 MedicineReminder — automated, self-hosted reminder emails.</p>
                </div>

                {ScriptBlock}
              </body>
            </html>
            """;
    }

    private static string BuildRow(ReminderItem reminder, int index, DateOnly today)
    {
        int daysUntil = reminder.ReminderDate.DayNumber - today.DayNumber;
        string badge = BuildStatusBadge(daysUntil);
        string isoUtc = reminder.ReminderDate.ToDateTime(reminder.ReminderTime).ToString("yyyy-MM-ddTHH:mm:00Z");
        string searchText = WebUtility.HtmlEncode(
            $"{reminder.MedicineName} {reminder.Description} {reminder.ReceiverEmail} {reminder.TelegramChatId}".ToLowerInvariant());

        return $"""
            <tr data-search="{searchText}">
              <td data-label="Date" style="padding:10px 8px; color:#111827; font-weight:600; white-space:nowrap;">{reminder.ReminderDate:yyyy-MM-dd}</td>
              <td data-label="Time (UTC)" style="padding:10px 8px; color:#6b7280; white-space:nowrap;">{reminder.ReminderTime:HH:mm}</td>
              <td data-label="Your local time" class="local-time" data-utc="{isoUtc}" style="padding:10px 8px; color:#6b7280; white-space:nowrap;">–</td>
              <td data-label="Medicine" style="padding:10px 8px;">{WebUtility.HtmlEncode(reminder.MedicineName)}</td>
              <td data-label="Description" style="padding:10px 8px; color:#6b7280;">{WebUtility.HtmlEncode(reminder.Description)}</td>
              <td data-label="To" style="padding:10px 8px; color:#6b7280;">{(string.IsNullOrWhiteSpace(reminder.ReceiverEmail) ? "<span style=\"color:#9ca3af;\">default</span>" : WebUtility.HtmlEncode(reminder.ReceiverEmail))}</td>
              <td data-label="Telegram" style="padding:10px 8px; color:#6b7280;">{(string.IsNullOrWhiteSpace(reminder.TelegramChatId) ? "<span style=\"color:#9ca3af;\">default</span>" : WebUtility.HtmlEncode(reminder.TelegramChatId))}</td>
              <td data-label="Status" style="padding:10px 8px; white-space:nowrap;">{badge}</td>
              <td style="padding:10px 8px; text-align:right;">
                <form method="post" action="/reminders/{index}/delete" onsubmit="return confirm('Delete this reminder?');">
                  <button type="submit" class="icon-btn">🗑️ Delete</button>
                </form>
              </td>
            </tr>
            """;
    }

    private static string BuildStatusBadge(int daysUntil)
    {
        (string label, string background, string color) = daysUntil switch
        {
            < 0 => ("⚪ Past — missed", "#f3f4f6", "#6b7280"),
            0 => ("🔴 Due today — emails today", "#fee2e2", "#991b1b"),
            1 => ("🔵 Tomorrow", "#dbeafe", "#1d4ed8"),
            <= 7 => ($"🔵 In {daysUntil} days", "#dbeafe", "#1d4ed8"),
            _ => ($"⚫ In {daysUntil} days", "#f3f4f6", "#374151"),
        };

        return $"""<span style="background:{background}; color:{color}; padding:4px 10px; border-radius:999px; font-size:12px; font-weight:600;">{label}</span>""";
    }
}
