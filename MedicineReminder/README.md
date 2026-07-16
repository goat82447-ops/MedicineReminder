# MedicineReminder

A production-ready .NET 8 console application with **two independent,
separately scheduled email reminders** — fully automated via GitHub Actions,
no manual work required:

1. **Monthly medicine reminder** ([medicine-reminder.yml](.github/workflows/medicine-reminder.yml)) —
   runs on the **9th of every month**, one day before the fixed medicine date
   (the 10th). Sends the single reminder configured in `MedicineSettings`.
2. **Daily portal reminder** ([daily-reminder.yml](.github/workflows/daily-reminder.yml)) —
   runs **every day**, checks `reminders.json` (your "portal"), and emails
   any one-off entry whose date is tomorrow.

Both share the same app (`MedicineReminder.dll`) and `EmailSettings`, and are
selected via a `--mode=medicine` / `--mode=daily` command-line flag.

```
Monthly (9th)                          Daily (every day)
      ↓                                       ↓
GitHub Action (cron)                  GitHub Action (cron)
      ↓                                       ↓
--mode=medicine                        --mode=daily
      ↓                                       ↓
Send fixed MedicineSettings email     Load reminders.json → match tomorrow
      ↓                                       ↓
              Done (no manual work, both flows)
```

## Adding a new one-off reminder

**Option A — local dashboard (recommended):**

```powershell
cd MedicineReminder
dotnet run --configuration Release -- --mode=dashboard
```

Then open http://localhost:5080 in a browser. It shows all current
reminders with a Delete button, and a form to add a new one (description,
medicine name, message, target date). Submissions are written directly to
the git-tracked `reminders.json` — **no email is sent immediately**; the
reminder is only emailed on its scheduled date (the day before the target
date, via the daily GitHub Action). Press `Ctrl+C` to stop the server when
done, then commit and push `reminders.json` so the daily Action picks it up.
This is a local-only tool (bound to `localhost`, no authentication) meant to
run on your own machine while you use it.

**Option B — edit reminders.json directly:**

Edit [reminders.json](reminders.json) — either locally or directly in
GitHub's web editor — and add a new object to the array:

```json
{
  "description": "Blood pressure tablet",
  "medicineName": "Amlodipine",
  "reminderMessage": "Don't forget to take your blood pressure tablet tomorrow!",
  "reminderDate": "2026-09-05"
}
```

Either way, commit and push the change. The next daily run will pick it up
automatically and email you the day before `reminderDate`. Because reminder
descriptions may contain personal health information, keep this repository
**private**.

To change the **fixed monthly medicine reminder** instead, edit the
`MedicineSettings` section of `appsettings.json` (or override
`MEDICINE_NAME` / `REMINDER_MESSAGE` GitHub Secrets) — no date needed, since
the cron schedule itself fires only on the 9th.

## Project structure

```
MedicineReminder/
├── .github/
│   └── workflows/
│       ├── medicine-reminder.yml   # Monthly cron (9th) — fixed medicine reminder
│       └── daily-reminder.yml      # Daily cron — reminders.json portal items
├── Models/
│   ├── AppConfig.cs                # Configuration section name constants
│   ├── EmailSettings.cs            # Gmail SMTP configuration model
│   ├── MedicineSettings.cs         # Fixed monthly medicine name / message model
│   └── ReminderItem.cs             # Description / medicine / message / date model
├── Repositories/
│   ├── IReminderRepository.cs      # Reminder data-access abstraction (DI)
│   └── ReminderRepository.cs       # Reads/writes reminders.json (add, remove, list)
├── Serialization/
│   └── DateOnlyJsonConverter.cs    # Strict "yyyy-MM-dd" DateOnly (de)serialization
├── Services/
│   ├── IEmailService.cs            # Email service abstraction (DI)
│   └── EmailService.cs             # MailKit-based Gmail SMTP implementation
├── Templates/
│   ├── ReminderEmailTemplate.cs    # Styled HTML/plain-text email body builder
│   └── DashboardPage.cs            # HTML for the local --mode=dashboard page
├── appsettings.json                # SMTP + medicine configuration (placeholders only)
├── appsettings.Local.json          # Optional, gitignored — real local secrets
├── reminders.json                  # Your one-off reminder items ("portal" data file)
├── Program.cs                      # Host bootstrap, DI, logging, error handling, modes
├── MedicineReminder.csproj
├── .gitignore
└── README.md
```

## How configuration works

`appsettings.json` only contains **placeholder** values — never commit real
credentials. At runtime the configuration is layered as:

1. `appsettings.json` (placeholders, committed)
2. `appsettings.Local.json` (optional, **gitignored** — put your real local
   secrets here for testing; never committed)
3. Environment variables (override the above) — this is how GitHub Actions
   injects secrets, using the `Section__Property` naming convention, e.g.
   `EmailSettings__SenderPassword`.
4. Command-line arguments (highest precedence) — also used for `--mode=medicine`
   / `--mode=daily`.

`EmailSettings` and `MedicineSettings` are bound via the Options pattern and
validated on startup with Data Annotations (`ValidateOnStart`), so missing or
invalid configuration fails fast with a clear error message. One-off
reminder items live in `reminders.json` instead (see "Adding a new one-off
reminder" above); each is validated individually at runtime and an invalid
entry is skipped with a warning rather than failing the whole run.

## Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- A Gmail account with **2-Step Verification** enabled
- A **Gmail App Password** (Google Account → Security → 2-Step Verification →
  App passwords → generate a 16-character password). This is required
  because Gmail no longer accepts your normal account password for SMTP.

## Run locally

```powershell
cd MedicineReminder

dotnet restore
dotnet build --configuration Release

# Test the fixed monthly medicine reminder:
dotnet run --configuration Release -- --mode=medicine

# Test the daily reminders.json portal check:
dotnet run --configuration Release -- --mode=daily
```

Real credentials for local testing go in `appsettings.Local.json`
(gitignored, never committed) — copy the shape of `appsettings.json` and
fill in `EmailSettings.SenderEmail` / `SenderPassword`. Alternatively, set
them as environment variables for the current session:

```powershell
$env:EmailSettings__SenderEmail = "your-gmail-address@gmail.com"
$env:EmailSettings__SenderPassword = "your-16-char-app-password"
$env:EmailSettings__ReceiverEmail = "receiver-email@example.com"
```

## Publish this repository to GitHub

Run these commands from the `MedicineReminder` folder (replace
`<your-username>` with your GitHub username or org):

```powershell
cd MedicineReminder

git init
git add .
git commit -m "Initial commit: MedicineReminder automated email reminder"
git branch -M main

# Create the repository on GitHub first (via the website), or with GitHub CLI:
gh repo create <your-username>/MedicineReminder --private --source=. --remote=origin

# If you created the repo manually on GitHub instead of via `gh repo create`:
git remote add origin https://github.com/<your-username>/MedicineReminder.git

git push -u origin main
```

## Configure GitHub Secrets

The workflow reads credentials from **GitHub Secrets** (never from the
repository files). Set them with the GitHub CLI:

```powershell
gh secret set SENDER_EMAIL --body "your-gmail-address@gmail.com" --repo <your-username>/MedicineReminder
gh secret set GMAIL_APP_PASSWORD --body "your-16-char-app-password" --repo <your-username>/MedicineReminder
gh secret set RECEIVER_EMAIL --body "receiver-email@example.com" --repo <your-username>/MedicineReminder

# Used only by medicine-reminder.yml (optional — falls back to appsettings.json if unset):
gh secret set MEDICINE_NAME --body "Vitamin D3" --repo <your-username>/MedicineReminder
gh secret set REMINDER_MESSAGE --body "Take your medicine tomorrow!" --repo <your-username>/MedicineReminder
```

Or via the GitHub website: **Repository → Settings → Secrets and variables →
Actions → New repository secret**, and add:
`SENDER_EMAIL`, `GMAIL_APP_PASSWORD`, `RECEIVER_EMAIL` (used by both
workflows), plus optionally `MEDICINE_NAME` and `REMINDER_MESSAGE` (used only
by the monthly workflow).

One-off reminder descriptions/messages are **not** secrets — they live in
`reminders.json` in the repository itself (keep the repo private if that
content is sensitive).

## Verify the automation

- Go to the **Actions** tab of the repository and confirm both
  **Medicine Reminder Email (Monthly)** and **Daily Reminder Email
  (reminders.json)** workflows are listed and enabled.
- Trigger either one manually to verify it works, without waiting for its
  schedule:

  ```powershell
  gh workflow run medicine-reminder.yml --repo <your-username>/MedicineReminder
  gh workflow run daily-reminder.yml --repo <your-username>/MedicineReminder
  ```

- After that: the monthly workflow runs on the **9th** at 08:00 UTC (fixed
  medicine reminder), and the daily workflow runs **every day** at 08:00 UTC
  (emails any `reminders.json` entry due tomorrow). Adjust the `cron`
  expressions in each workflow file if you need a different time/timezone.

## Notes

- GitHub Actions `schedule` triggers only run on the **default branch** and
  only while the repository has had activity; GitHub may delay scheduled
  runs by a few minutes during high load — this is expected behavior.
- Never commit real Gmail credentials to `appsettings.json`; always use
  GitHub Secrets / environment variables.
