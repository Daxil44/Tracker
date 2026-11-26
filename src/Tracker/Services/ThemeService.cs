using Tracker.Models;
using Microsoft.JSInterop;

namespace Tracker.Services;

public sealed class ThemeService(IJSRuntime js, PreferencesService preferences)
{
    public ValueTask ApplyAsync(ThemePreference theme) =>
        js.InvokeVoidAsync("tracker.theme.apply", theme.ToString().ToLowerInvariant());

    /// <summary>Flips between light and dark based on what is currently on screen (including the OS setting).</summary>
    public async Task ToggleAsync()
    {
        var effective = await js.InvokeAsync<string>("tracker.theme.effective");
        var next = effective == "dark" ? ThemePreference.Light : ThemePreference.Dark;
        await ApplyAsync(next);
        await preferences.UpdateAsync(p => p with { Theme = next });
    }
}
