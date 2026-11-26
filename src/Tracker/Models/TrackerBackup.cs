namespace Tracker.Models;

/// <summary>
/// A downloadable backup of everything stored in this browser. New trackers (protein, sleep…)
/// get their own optional property here, so older backups stay importable.
/// </summary>
public sealed class TrackerBackup
{
    public const string AppId = "tracker";
    public const int CurrentFormat = 1;

    /// <summary>No default on purpose: a file without <c>"app": "tracker"</c> is not our backup.</summary>
    public string? App { get; set; }
    public int Format { get; set; } = CurrentFormat;
    public DateTimeOffset ExportedAt { get; set; }

    public UserPreferences? Preferences { get; set; }
    public HydrationHistory? Water { get; set; }
}

/// <summary>What an import did, for showing the user.</summary>
public sealed record ImportSummary(int EntriesAdded, int Duplicates, int Invalid, bool SettingsRestored);
