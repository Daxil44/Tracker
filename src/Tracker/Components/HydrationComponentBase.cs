using System.Globalization;
using Tracker.Models;
using Tracker.Services;
using Microsoft.AspNetCore.Components;

namespace Tracker.Components;

/// <summary>
/// Base for components that display live hydration data. Re-renders whenever intake,
/// preferences or the current day change, and unsubscribes on dispose.
/// </summary>
public abstract class HydrationComponentBase : ComponentBase, IDisposable
{
    [Inject] protected WaterTrackingService Tracking { get; set; } = default!;
    [Inject] protected PreferencesService Preferences { get; set; } = default!;

    protected VolumeUnit Unit => Preferences.Current.Unit;

    protected string Fmt(int ml) => VolumeFormat.Format(ml, Unit);

    protected override void OnInitialized() => Tracking.Changed += HandleChanged;

    private void HandleChanged() => _ = InvokeAsync(StateHasChanged);

    public virtual void Dispose()
    {
        Tracking.Changed -= HandleChanged;
        GC.SuppressFinalize(this);
    }
}

/// <summary>Culture-invariant numbers for SVG attributes and inline CSS (never "62,5%").</summary>
public static class Css
{
    public static string Num(double value) => value.ToString("0.###", CultureInfo.InvariantCulture);
}

public static class EntryLabels
{
    public const string FullBottle = "Full bottle";
    public const string HalfBottle = "Half bottle";
    public const string Glass = "Glass";
    public const string Custom = "Custom";

    public static string IconFor(string? label) => label switch
    {
        FullBottle => "bottle",
        HalfBottle => "half",
        Glass => "glass",
        _ => "droplet",
    };
}
