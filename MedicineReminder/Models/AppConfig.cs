namespace MedicineReminder.Models;

/// <summary>
/// Centralized configuration section names to avoid magic strings
/// when binding options in Program.cs.
/// </summary>
public static class AppConfig
{
    public const string EmailSettingsSection = "EmailSettings";
    public const string MedicineSettingsSection = "MedicineSettings";
    public const string SchedulerSection = "Scheduler";
}
