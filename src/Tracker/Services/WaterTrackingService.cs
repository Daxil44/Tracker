using System.Globalization;
using Tracker.Models;
using Tracker.Serialization;

namespace Tracker.Services;

/// <summary>
/// Owns the intake history. Each calendar day (browser local time) gets its own record;
/// a new day starts automatically the first time the date is observed to have changed.
/// </summary>
public sealed class WaterTrackingService(
    LocalStorageService storage,
    PreferencesService preferences,
    TimeProvider clock)
{
    public const int MaxEntryMl = 5_000;

    private HydrationHistory _history = new();

    public event Action? Changed;

    public DateOnly Today { get; private set; }

    public HydrationProgress TodayProgress => GetProgress(Today);

    public IReadOnlyList<WaterEntry> TodayEntries => GetEntries(Today);

    public async Task InitializeAsync()
    {
        _history = await storage.GetAsync(StorageKeys.History, AppJsonContext.Default.HydrationHistory)
                   ?? new HydrationHistory();
        Today = LocalToday();
        preferences.Changed += OnPreferencesChanged;
    }

    /// <summary>Call periodically; starts a fresh day when midnight has passed.</summary>
    public bool CheckForNewDay()
    {
        var today = LocalToday();
        if (today == Today)
        {
            return false;
        }

        Today = today;
        Changed?.Invoke();
        return true;
    }

    public IReadOnlyList<WaterEntry> GetEntries(DateOnly date) =>
        _history.Days.TryGetValue(Key(date), out var record) ? record.Entries : [];

    public int GetTotal(DateOnly date) =>
        _history.Days.TryGetValue(Key(date), out var record) ? record.TotalMl : 0;

    /// <summary>Today always uses the live goal; past days use the goal that was set on that day.</summary>
    public int GetGoal(DateOnly date)
    {
        if (date != Today && _history.Days.TryGetValue(Key(date), out var record) && record.GoalMl > 0)
        {
            return record.GoalMl;
        }

        return preferences.Current.DailyGoalMl;
    }

    public HydrationProgress GetProgress(DateOnly date) =>
        new(GetTotal(date), GetGoal(date), preferences.Current.BottleCapacityMl);

    /// <summary>The last <paramref name="days"/> days ending today, oldest first.</summary>
    public IReadOnlyList<DaySummary> GetRecentDays(int days)
    {
        var result = new List<DaySummary>(days);
        for (var offset = days - 1; offset >= 0; offset--)
        {
            var date = Today.AddDays(-offset);
            result.Add(new DaySummary(date, GetTotal(date), GetGoal(date)));
        }

        return result;
    }

    /// <returns><c>false</c> if the entry was recorded but could not be saved to the browser.</returns>
    public async Task<bool> AddAsync(int amountMl, string? label = null)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(amountMl, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(amountMl, MaxEntryMl);

        CheckForNewDay();
        var record = GetOrCreateToday();
        record.Entries.Add(new WaterEntry
        {
            AmountMl = amountMl,
            LoggedAt = clock.GetLocalNow(),
            Label = label,
        });

        return await PersistAsync();
    }

    /// <summary>Removes the most recent entry logged today.</summary>
    public async Task<WaterEntry?> UndoLastAsync()
    {
        CheckForNewDay();
        if (!_history.Days.TryGetValue(Key(Today), out var record) || record.Entries.Count == 0)
        {
            return null;
        }

        var last = record.Entries.MaxBy(e => e.LoggedAt)!;
        record.Entries.Remove(last);
        await PersistAsync();
        return last;
    }

    public async Task<bool> RemoveAsync(DateOnly date, Guid entryId)
    {
        if (!_history.Days.TryGetValue(Key(date), out var record))
        {
            return false;
        }

        var removed = record.Entries.RemoveAll(e => e.Id == entryId) > 0;
        if (removed)
        {
            await PersistAsync();
        }

        return removed;
    }

    private DailyRecord GetOrCreateToday()
    {
        var key = Key(Today);
        if (!_history.Days.TryGetValue(key, out var record))
        {
            record = new DailyRecord { Date = Today, GoalMl = preferences.Current.DailyGoalMl };
            _history.Days[key] = record;
        }

        return record;
    }

    private async Task<bool> PersistAsync()
    {
        if (_history.Days.TryGetValue(Key(Today), out var today))
        {
            today.GoalMl = preferences.Current.DailyGoalMl;
        }

        // Drop days that no longer have any entries so history only contains real data.
        foreach (var key in _history.Days.Where(d => d.Value.Entries.Count == 0).Select(d => d.Key).ToList())
        {
            _history.Days.Remove(key);
        }

        var saved = await storage.SetAsync(StorageKeys.History, _history, AppJsonContext.Default.HydrationHistory);
        Changed?.Invoke();
        return saved;
    }

    private async void OnPreferencesChanged()
    {
        // Keep today's goal snapshot in sync; past days keep the goal they had.
        if (_history.Days.ContainsKey(Key(Today)))
        {
            await PersistAsync();
        }
        else
        {
            Changed?.Invoke();
        }
    }

    private DateOnly LocalToday() => DateOnly.FromDateTime(clock.GetLocalNow().DateTime);

    private static string Key(DateOnly date) => date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
