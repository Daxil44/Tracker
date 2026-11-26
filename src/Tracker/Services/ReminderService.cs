using Tracker.Models;
using Tracker.Serialization;
using Microsoft.JSInterop;

namespace Tracker.Services;

/// <summary>
/// Decides when to remind the user to drink, and shows a browser notification plus an in-app banner.
/// A reminder is due <c>interval</c> minutes after the latest of: office hours starting, the app being
/// opened, the last reminder, or the last drink. Logging water therefore pushes the next reminder back.
/// Runs only while the page is open (browsers give pages no way to wake up when closed).
/// </summary>
public sealed class ReminderService(
    IJSRuntime js,
    LocalStorageService storage,
    PreferencesService preferences,
    WaterTrackingService tracking,
    TimeProvider clock,
    ILogger<ReminderService> logger) : IDisposable
{
    public const int SnoozeMinutes = 15;

    private static readonly TimeSpan TickInterval = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ActiveReminderLifetime = TimeSpan.FromHours(2);

    private ReminderState _state = new();
    private DateTimeOffset _sessionStart;
    private CancellationTokenSource? _loop;
    private string _signature = "";

    public event Action? Changed;

    /// <summary>"default", "granted", "denied" or "unsupported".</summary>
    public string Permission { get; private set; } = "default";

    public bool NotificationsAllowed => Permission == "granted";

    public async Task InitializeAsync()
    {
        _state = await storage.GetAsync(StorageKeys.Reminders, AppJsonContext.Default.ReminderState) ?? new();
        Permission = await js.InvokeAsync<string>("tracker.notify.permission");

        // Don't fire an "overdue" reminder the moment the app opens; start counting from now.
        _sessionStart = clock.GetLocalNow();
        tracking.Changed += OnTrackingChanged;

        _loop = new CancellationTokenSource();
        _ = RunAsync(_loop.Token);
    }

    public ReminderStatus GetStatus(DateTimeOffset now)
    {
        var prefs = preferences.Current;
        if (!prefs.RemindersEnabled)
        {
            return new(ReminderStatusKind.Off);
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        if (_state.PausedOn == today)
        {
            return new(ReminderStatusKind.Paused);
        }

        if (prefs.ReminderStopAtGoal && tracking.TodayProgress.GoalReached)
        {
            return new(ReminderStatusKind.GoalReached);
        }

        if (CurrentWindow(now, prefs) is not { } window)
        {
            return new(ReminderStatusKind.OutsideHours, NextWindowStart(now, prefs));
        }

        var lastDrink = LastDrink();

        // A snooze applies to this office-hours window, unless the user drank since snoozing.
        if (_state.SnoozedUntil is { } until && until >= window.Start && !(lastDrink >= _state.SnoozedAt))
        {
            return now < until
                ? new(ReminderStatusKind.Snoozed, until)
                : new(ReminderStatusKind.Scheduled, until);
        }

        var anchor = Latest(window.Start, _sessionStart, _state.LastRemindedAt, lastDrink);
        var due = anchor.AddMinutes(prefs.ReminderIntervalMinutes);
        return due < window.End
            ? new(ReminderStatusKind.Scheduled, due)
            : new(ReminderStatusKind.OutsideHours, NextWindowStart(window.End, prefs));
    }

    /// <summary>True while a reminder has fired and the user hasn't drunk, snoozed or dismissed since.</summary>
    public bool IsReminderActive(DateTimeOffset now) =>
        _state.ActiveSince is { } since
        && preferences.Current.RemindersEnabled
        && now - since < ActiveReminderLifetime
        && DateOnly.FromDateTime(since.DateTime) == DateOnly.FromDateTime(now.DateTime)
        && !(LastDrink() >= since);

    public async Task SetEnabledAsync(bool enabled)
    {
        if (enabled)
        {
            _sessionStart = clock.GetLocalNow();
        }
        else
        {
            _state.ActiveSince = null;
            _state.SnoozedAt = _state.SnoozedUntil = null;
            await SaveAsync();
        }

        await preferences.UpdateAsync(p => p with { RemindersEnabled = enabled });
        RaiseChanged();
    }

    public async Task<string> RequestPermissionAsync()
    {
        Permission = await js.InvokeAsync<string>("tracker.notify.request");
        RaiseChanged();
        return Permission;
    }

    public async Task SnoozeAsync()
    {
        var now = clock.GetLocalNow();
        _state.SnoozedAt = now;
        _state.SnoozedUntil = now.AddMinutes(SnoozeMinutes);
        _state.ActiveSince = null;
        await SaveAsync();
        RaiseChanged();
    }

    public async Task PauseForTodayAsync()
    {
        _state.PausedOn = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
        _state.SnoozedAt = _state.SnoozedUntil = null;
        _state.ActiveSince = null;
        await SaveAsync();
        RaiseChanged();
    }

    public async Task ResumeAsync()
    {
        _state.PausedOn = null;
        _state.SnoozedAt = _state.SnoozedUntil = null;
        _sessionStart = clock.GetLocalNow();
        await SaveAsync();
        RaiseChanged();
    }

    public async Task DismissAsync()
    {
        _state.ActiveSince = null;
        await SaveAsync();
        RaiseChanged();
    }

    /// <returns><c>false</c> if the browser refused to show it (blocked, unsupported, or mobile Chrome).</returns>
    public async Task<bool> SendTestAsync() =>
        await js.InvokeAsync<bool>("tracker.notify.show", "Water reminders are on", ProgressMessage());

    public string ProgressMessage()
    {
        var p = tracking.TodayProgress;
        var unit = preferences.Current.Unit;
        if (p.GoalReached)
        {
            return $"Goal reached: {VolumeFormat.Format(p.IntakeMl, unit)} today.";
        }

        var bottles = VolumeFormat.Bottles(p.BottlesRemaining);
        var noun = p.BottlesRemaining == 1 ? "bottle" : "bottles";
        return p.IntakeMl == 0
            ? $"Nothing logged yet. Your goal today is {VolumeFormat.Format(p.GoalMl, unit)}."
            : $"{VolumeFormat.Format(p.IntakeMl, unit)} of {VolumeFormat.Format(p.GoalMl, unit)} so far, {bottles} {noun} to go.";
    }

    private async Task RunAsync(CancellationToken cancellationToken)
    {
        using var timer = new PeriodicTimer(TickInterval);
        try
        {
            while (await timer.WaitForNextTickAsync(cancellationToken))
            {
                await TickAsync();
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    private async Task TickAsync()
    {
        try
        {
            // Pick up changes another open tab may have made (e.g. it already fired this reminder).
            _state = await storage.GetAsync(StorageKeys.Reminders, AppJsonContext.Default.ReminderState) ?? _state;
            Permission = await js.InvokeAsync<string>("tracker.notify.permission");
            tracking.CheckForNewDay();

            var now = clock.GetLocalNow();
            if (GetStatus(now) is { Kind: ReminderStatusKind.Scheduled, At: { } due } && due <= now)
            {
                await FireAsync(now);
            }

            RaiseChangedIfDifferent(now);
        }
        catch (Exception ex) when (ex is JSException or InvalidOperationException)
        {
            logger.LogWarning(ex, "Reminder check failed.");
        }
    }

    private async Task FireAsync(DateTimeOffset now)
    {
        _state.LastRemindedAt = now;
        _state.SnoozedAt = _state.SnoozedUntil = null;
        _state.ActiveSince = now;
        await SaveAsync();

        if (NotificationsAllowed)
        {
            await js.InvokeAsync<bool>("tracker.notify.show", "Time for some water", ProgressMessage());
        }

        RaiseChanged();
    }

    private async void OnTrackingChanged()
    {
        // Drinking answers the current reminder and restarts the countdown.
        if (_state.ActiveSince is { } since && LastDrink() >= since)
        {
            _state.ActiveSince = null;
            await SaveAsync();
        }

        RaiseChanged();
    }

    private DateTimeOffset? LastDrink() => tracking.TodayEntries.MaxBy(e => e.LoggedAt)?.LoggedAt;

    private async Task SaveAsync() =>
        await storage.SetAsync(StorageKeys.Reminders, _state, AppJsonContext.Default.ReminderState);

    private void RaiseChanged()
    {
        _signature = Signature(clock.GetLocalNow());
        Changed?.Invoke();
    }

    private void RaiseChangedIfDifferent(DateTimeOffset now)
    {
        if (Signature(now) != _signature)
        {
            RaiseChanged();
        }
    }

    private string Signature(DateTimeOffset now)
    {
        var status = GetStatus(now);
        return $"{status.Kind}|{status.At:O}|{IsReminderActive(now)}|{Permission}";
    }

    /// <summary>The office-hours window containing <paramref name="now"/>. Supports overnight hours (end before start).</summary>
    private static (DateTimeOffset Start, DateTimeOffset End)? CurrentWindow(DateTimeOffset now, UserPreferences prefs)
    {
        if (prefs.OfficeStart == prefs.OfficeEnd)
        {
            return null;
        }

        var today = DateOnly.FromDateTime(now.DateTime);
        foreach (var day in new[] { today.AddDays(-1), today })
        {
            var start = At(day, prefs.OfficeStart, now.Offset);
            var end = prefs.OfficeEnd > prefs.OfficeStart
                ? At(day, prefs.OfficeEnd, now.Offset)
                : At(day.AddDays(1), prefs.OfficeEnd, now.Offset);
            if (start <= now && now < end)
            {
                return (start, end);
            }
        }

        return null;
    }

    private static DateTimeOffset? NextWindowStart(DateTimeOffset after, UserPreferences prefs)
    {
        if (prefs.OfficeStart == prefs.OfficeEnd)
        {
            return null;
        }

        var day = DateOnly.FromDateTime(after.DateTime);
        for (var i = 0; i < 2; i++)
        {
            var start = At(day.AddDays(i), prefs.OfficeStart, after.Offset);
            if (start >= after)
            {
                return start;
            }
        }

        return null;
    }

    private static DateTimeOffset At(DateOnly day, TimeOnly time, TimeSpan offset) =>
        new(day.ToDateTime(time), offset);

    private static DateTimeOffset Latest(DateTimeOffset first, params DateTimeOffset?[] others)
    {
        var latest = first;
        foreach (var value in others)
        {
            if (value is { } v && v > latest)
            {
                latest = v;
            }
        }

        return latest;
    }

    public void Dispose()
    {
        tracking.Changed -= OnTrackingChanged;
        _loop?.Cancel();
        _loop?.Dispose();
    }
}
