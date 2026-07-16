namespace MedicineReminder.Infrastructure;

/// <summary>
/// Reliably resolves the project directory (containing MedicineReminder.csproj,
/// appsettings.json, appsettings.Local.json, and reminders.json) regardless of
/// the process's current working directory or how it was launched (dotnet
/// run, dotnet run --project, GitHub Actions, a published exe, etc.).
/// </summary>
internal static class ProjectPaths
{
    /// <summary>
    /// The project root directory, found by walking up from the build output
    /// directory (AppContext.BaseDirectory) until a folder containing a
    /// .csproj file is found.
    /// </summary>
    public static string ProjectRoot { get; } = Resolve();

    private static string Resolve()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        for (int i = 0; i < 6 && directory is not null; i++)
        {
            if (directory.GetFiles("*.csproj").Length > 0)
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        // Fallback (should not normally be reached): the build output directory.
        return AppContext.BaseDirectory;
    }
}
