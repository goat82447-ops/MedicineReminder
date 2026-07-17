using MedicineReminder.Models;

namespace MedicineReminder.Repositories;

/// <summary>
/// Reads and writes reminder entries in the reminders.json data file.
/// </summary>
public interface IReminderRepository
{
    Task<IReadOnlyList<ReminderItem>> GetAllAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Appends a new reminder and persists the updated list. Used by the
    /// local dashboard (--mode=dashboard).
    /// </summary>
    Task AddAsync(ReminderItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces the reminder at <paramref name="index"/> (as returned by
    /// <see cref="GetAllAsync"/>) with <paramref name="item"/> and persists
    /// the updated list. Returns <see langword="false"/> if the index is out
    /// of range.
    /// </summary>
    Task<bool> UpdateAsync(int index, ReminderItem item, CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes the reminder at <paramref name="index"/> (as returned by
    /// <see cref="GetAllAsync"/>) and persists the updated list. Returns
    /// <see langword="false"/> if the index is out of range.
    /// </summary>
    Task<bool> RemoveAsync(int index, CancellationToken cancellationToken = default);
}
