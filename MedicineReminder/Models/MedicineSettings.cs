using System.ComponentModel.DataAnnotations;

namespace MedicineReminder.Models;

/// <summary>
/// The single, fixed medicine reminder bound from the "MedicineSettings"
/// section of appsettings.json. Used by the monthly "--mode=medicine" run,
/// which fires every 9th of the month regardless of any date bookkeeping.
/// </summary>
public sealed class MedicineSettings
{
    [Required(ErrorMessage = "Medicine name is required.")]
    public string MedicineName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Reminder message is required.")]
    public string ReminderMessage { get; set; } = string.Empty;
}
