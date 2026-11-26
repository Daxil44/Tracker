namespace Tracker.Models;

/// <summary>
/// Runtime reminder state that must survive a page refresh (kept apart from preferences,
/// which only hold what the user configured).
/// </summary>
public sealed class ReminderState
{
    public DateTimeOffset? LastRemindedAt { get; set; }

    /// <summary>When the user pressed snooze, and when the snooze ends.</summary>
    public DateTimeOffset? SnoozedAt { get; set; }
    public DateTimeOffset? SnoozedUntil { get; set; }

    /// <summary>Reminders are paused for the rest of this day.</summary>
    public DateOnly? PausedOn { get; set; }

    /// <summary>The reminder currently shown in the app (cleared by logging water, snoozing or dismissing).</summary>
    public DateTimeOffset? ActiveSince { get; set; }
}

public enum ReminderStatusKind
{
    Off,
    Paused,
    GoalReached,
    Snoozed,
    OutsideHours,
    Scheduled,
}

public sealed record ReminderStatus(ReminderStatusKind Kind, DateTimeOffset? At = null);
