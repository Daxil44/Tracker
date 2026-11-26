using System.Globalization;
using System.Text.Json;
using Microsoft.JSInterop;
using Tracker.Models;
using Tracker.Serialization;

namespace Tracker.Services;

/// <summary>
/// Backup file export/import and "delete all data". Everything stays on the device:
/// export saves a JSON file locally, import reads one the user picks.
/// </summary>
public sealed class DataBackupService(
    IJSRuntime js,
    PreferencesService preferences,
    WaterTrackingService water,
    ReminderService reminders,
    ThemeService theme,
    TimeProvider clock)
{
    public const long MaxBackupBytes = 5 * 1024 * 1024;

    private static readonly AppJsonContext Indented = new(new JsonSerializerOptions(AppJsonContext.Default.Options)
    {
        WriteIndented = true,
    });

    /// <summary>Downloads a backup file named like <c>tracker-backup-2026-11-26.json</c>.</summary>
    public async Task<string> DownloadBackupAsync()
    {
        var now = clock.GetLocalNow();
        var backup = new TrackerBackup
        {
            App = TrackerBackup.AppId,
            ExportedAt = now,
            Preferences = preferences.Current,
            Water = water.ExportHistory(),
        };

        var json = JsonSerializer.Serialize(backup, Indented.TrackerBackup);
        var fileName = $"tracker-backup-{now.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.json";
        await js.InvokeVoidAsync("tracker.files.download", fileName, json, "application/json");
        return fileName;
    }

    /// <summary>Reads and checks a backup file. Returns an error message for the user instead of throwing.</summary>
    public static (TrackerBackup? Backup, string? Error) Parse(string json)
    {
        TrackerBackup? backup;
        try
        {
            backup = JsonSerializer.Deserialize(json, AppJsonContext.Default.TrackerBackup);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            return (null, "That file isn't a Tracker backup (it couldn't be read).");
        }

        if (backup is null || !string.Equals(backup.App, TrackerBackup.AppId, StringComparison.OrdinalIgnoreCase))
        {
            return (null, "That file isn't a Tracker backup.");
        }

        if (backup.Format > TrackerBackup.CurrentFormat)
        {
            return (null, "This backup was made by a newer version of Tracker. Reload the page to update, then try again.");
        }

        if (backup.Preferences is null && backup.Water is null)
        {
            return (null, "This backup is empty.");
        }

        return (backup, null);
    }

    public async Task<ImportSummary> ImportAsync(TrackerBackup backup, bool restoreSettings)
    {
        var (added, duplicates, invalid) = backup.Water is { } history
            ? await water.MergeAsync(history)
            : (0, 0, 0);

        var settingsRestored = false;
        if (restoreSettings && backup.Preferences is { } prefs)
        {
            await preferences.UpdateAsync(_ => prefs);
            await theme.ApplyAsync(preferences.Current.Theme);
            settingsRestored = true;
        }

        return new ImportSummary(added, duplicates, invalid, settingsRestored);
    }

    /// <summary>Removes every Tracker value from this browser and returns to defaults.</summary>
    public async Task DeleteAllAsync()
    {
        await water.ResetAsync();
        await reminders.ResetAsync();
        await preferences.ResetAsync();
        await theme.ApplyAsync(preferences.Current.Theme);
    }

    public static int EntryCount(TrackerBackup backup) =>
        backup.Water?.Days.Values.Sum(d => d?.Entries?.Count ?? 0) ?? 0;

    public static int DayCount(TrackerBackup backup) =>
        backup.Water?.Days.Values.Count(d => d?.Entries is { Count: > 0 }) ?? 0;
}
