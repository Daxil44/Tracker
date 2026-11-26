using Tracker.Models;
using Tracker.Serialization;

namespace Tracker.Services;

public sealed class PreferencesService(LocalStorageService storage)
{
    public UserPreferences Current { get; private set; } = new();

    public event Action? Changed;

    public async Task InitializeAsync()
    {
        var stored = await storage.GetAsync(StorageKeys.Preferences, AppJsonContext.Default.UserPreferences);
        Current = (stored ?? new UserPreferences()).Sanitized();
    }

    /// <returns><c>false</c> if the change applied in memory but could not be saved.</returns>
    public async Task<bool> UpdateAsync(Func<UserPreferences, UserPreferences> update)
    {
        Current = update(Current).Sanitized();
        var saved = await storage.SetAsync(StorageKeys.Preferences, Current, AppJsonContext.Default.UserPreferences);
        Changed?.Invoke();
        return saved;
    }
}
