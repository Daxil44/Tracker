using System.Globalization;
using Tracker.Models;

namespace Tracker.Services;

public static class VolumeFormat
{
    public static string Format(int ml, VolumeUnit unit) => unit switch
    {
        VolumeUnit.Liters => $"{(ml / 1000.0).ToString("0.##", CultureInfo.CurrentCulture)} L",
        _ => $"{ml.ToString("N0", CultureInfo.CurrentCulture)} ml",
    };

    /// <summary>A bare number suitable for an input box in the given unit ("2400" or "2.4").</summary>
    public static string ToInput(int ml, VolumeUnit unit) => unit == VolumeUnit.Liters
        ? (ml / 1000.0).ToString("0.###", CultureInfo.CurrentCulture)
        : ml.ToString(CultureInfo.CurrentCulture);

    public static string Suffix(VolumeUnit unit) => unit == VolumeUnit.Liters ? "L" : "ml";

    /// <summary>Partial bottles are kept (2.5, 2.67) and never rounded to whole bottles.</summary>
    public static string Bottles(double bottles) => bottles.ToString("0.##", CultureInfo.CurrentCulture);

    /// <summary>Parses user input in the given unit into whole millilitres. Accepts "." or the culture's decimal separator.</summary>
    public static bool TryParse(string? input, VolumeUnit unit, out int ml)
    {
        ml = 0;
        if (string.IsNullOrWhiteSpace(input))
        {
            return false;
        }

        var text = input.Trim();
        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var value) &&
            !double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
        {
            return false;
        }

        if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0)
        {
            return false;
        }

        ml = (int)Math.Round(unit == VolumeUnit.Liters ? value * 1000 : value);
        return ml > 0;
    }
}
