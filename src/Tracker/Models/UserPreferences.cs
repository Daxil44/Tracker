namespace Tracker.Models;

/// <summary>
/// Everything the user can personalise. Treat as immutable: change it with <c>with { ... }</c>
/// through <see cref="Services.PreferencesService.UpdateAsync"/>.
/// Properties use <c>set</c> (not <c>init</c>) on purpose: the source-generated JSON reader sets
/// missing init-only properties to 0/false, which would wipe these defaults for settings
/// saved before a new option existed.
/// </summary>
public sealed record UserPreferences
{
    public const int MinGoalMl = 250;
    public const int MaxGoalMl = 10_000;
    public const int MinBottleMl = 50;
    public const int MaxBottleMl = 5_000;
    public const int MinReminderMinutes = 15;
    public const int MaxReminderMinutes = 240;

    public const int MaxNameLength = 40;

    /// <summary>Optional first name used in the greeting. Null when not set.</summary>
    public string? DisplayName { get; set; }

    public int DailyGoalMl { get; set; } = 2_400;
    public int BottleCapacityMl { get; set; } = 600;
    public VolumeUnit Unit { get; set; } = VolumeUnit.Milliliters;
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    public TimeOnly OfficeStart { get; set; } = new(9, 0);
    public TimeOnly OfficeEnd { get; set; } = new(17, 0);
    public bool RemindersEnabled { get; set; }
    public int ReminderIntervalMinutes { get; set; } = 60;
    public bool ReminderStopAtGoal { get; set; } = true;

    public bool OnboardingCompleted { get; set; }

    /// <summary>Answers from "Suggest a goal", if the user has used it. Null otherwise.</summary>
    public GoalProfile? GoalProfile { get; set; }

    public UserPreferences Sanitized() => this with
    {
        DisplayName = CleanName(DisplayName),
        DailyGoalMl = Math.Clamp(DailyGoalMl, MinGoalMl, MaxGoalMl),
        BottleCapacityMl = Math.Clamp(BottleCapacityMl, MinBottleMl, MaxBottleMl),
        ReminderIntervalMinutes = Math.Clamp(ReminderIntervalMinutes, MinReminderMinutes, MaxReminderMinutes),
    };

    /// <summary>Trims whitespace, collapses inner spaces, caps the length; empty becomes null.</summary>
    public static string? CleanName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        var cleaned = string.Join(' ', name.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return cleaned.Length <= MaxNameLength ? cleaned : cleaned[..MaxNameLength].TrimEnd();
    }
}
