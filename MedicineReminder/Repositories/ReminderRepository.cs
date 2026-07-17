using System.Text.Json;
using MedicineReminder.Infrastructure;
using MedicineReminder.Models;
using MedicineReminder.Serialization;
using Microsoft.Extensions.Logging;

namespace MedicineReminder.Repositories;

/// <summary>
/// Reads and writes the list of <see cref="ReminderItem"/> entries in
/// reminders.json. A single process-wide semaphore serializes reads/writes
/// to avoid corrupting the file when the dashboard is used concurrently.
/// </summary>
public sealed class ReminderRepository : IReminderRepository
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        Converters = { new DateOnlyJsonConverter(), new TimeOnlyJsonConverter() },
    };

    private static readonly SemaphoreSlim FileLock = new(1, 1);

    private readonly string _filePath;
    private readonly ILogger<ReminderRepository> _logger;

    public ReminderRepository(ILogger<ReminderRepository> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        // REMINDERS_FILE_PATH lets a deployment point this at a persistent
        // disk mount (e.g. Render Disks at /data/reminders.json), since the
        // default path next to the .csproj is on an ephemeral filesystem
        // when the app doesn't run from a persistent git checkout.
        string? overridePath = Environment.GetEnvironmentVariable("REMINDERS_FILE_PATH");
        _filePath = string.IsNullOrWhiteSpace(overridePath)
            ? Path.Combine(ProjectPaths.ProjectRoot, "reminders.json")
            : overridePath;
    }


    public async Task<IReadOnlyList<ReminderItem>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<ReminderItem> reminders = await ReadAllUnlockedAsync(cancellationToken).ConfigureAwait(false);

            if (reminders.Count == 0)
            {
                _logger.LogWarning("No reminders found in {FilePath}.", _filePath);
            }
            else
            {
                _logger.LogInformation("Loaded {Count} reminder(s) from {FilePath}.", reminders.Count, _filePath);
            }

            return reminders;
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task AddAsync(ReminderItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<ReminderItem> reminders = await ReadAllUnlockedAsync(cancellationToken).ConfigureAwait(false);
            reminders.Add(item);
            await WriteAllUnlockedAsync(reminders, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Added reminder '{Description}' to {FilePath}.", item.Description, _filePath);
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<bool> UpdateAsync(int index, ReminderItem item, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);

        await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<ReminderItem> reminders = await ReadAllUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (index < 0 || index >= reminders.Count)
            {
                return false;
            }

            reminders[index] = item;
            await WriteAllUnlockedAsync(reminders, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Updated reminder at index {Index} in {FilePath}.", index, _filePath);
            return true;
        }
        finally
        {
            FileLock.Release();
        }
    }

    public async Task<bool> RemoveAsync(int index, CancellationToken cancellationToken = default)
    {
        await FileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            List<ReminderItem> reminders = await ReadAllUnlockedAsync(cancellationToken).ConfigureAwait(false);
            if (index < 0 || index >= reminders.Count)
            {
                return false;
            }

            reminders.RemoveAt(index);
            await WriteAllUnlockedAsync(reminders, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Removed reminder at index {Index} from {FilePath}.", index, _filePath);
            return true;
        }
        finally
        {
            FileLock.Release();
        }
    }

    private async Task<List<ReminderItem>> ReadAllUnlockedAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_filePath))
        {
            return new List<ReminderItem>();
        }

        await using FileStream stream = File.OpenRead(_filePath);
        List<ReminderItem>? reminders = await JsonSerializer
            .DeserializeAsync<List<ReminderItem>>(stream, SerializerOptions, cancellationToken)
            .ConfigureAwait(false);

        return reminders ?? new List<ReminderItem>();
    }

    private async Task WriteAllUnlockedAsync(List<ReminderItem> reminders, CancellationToken cancellationToken)
    {
        // Defensive: if _filePath points at a disk mount (REMINDERS_FILE_PATH)
        // whose parent directory doesn't exist yet (e.g. no persistent disk
        // attached on the hosting platform), create it instead of throwing
        // DirectoryNotFoundException.
        string? directory = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await using FileStream stream = File.Create(_filePath);
        await JsonSerializer.SerializeAsync(stream, reminders, SerializerOptions, cancellationToken).ConfigureAwait(false);
    }
}
