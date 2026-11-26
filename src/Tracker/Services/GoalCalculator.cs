using Tracker.Models;

namespace Tracker.Services;

public sealed record GoalLine(string Label, int Ml);

public sealed record GoalEstimate(
    int? TotalMl,
    IReadOnlyList<GoalLine> Lines,
    IReadOnlyList<string> Notes,
    string? BlockedReason);

/// <summary>
/// A transparent, conservative estimate of how much to drink per day. Not medical advice.
///
/// Drinks-only baseline of ~30 ml per kg body weight (food supplies roughly a fifth of total water),
/// with a floor from reference intakes (EFSA adequate intake of total water: 2.0 L women / 2.5 L men,
/// ~80% of which comes from drinks), plus common add-ons for exercise, heat, creatine, pregnancy
/// (+300 ml) and breastfeeding (+700 ml).
/// </summary>
public static class GoalCalculator
{
    public const int MinAge = 18;
    public const int MaxAge = 100;
    public const double MinWeightKg = 30;
    public const double MaxWeightKg = 250;
    public const double KgPerLb = 0.45359237;

    private const int MlPerKg = 30;
    private const int CapMl = 5_000;

    public static int FloorFor(Sex sex) => sex switch
    {
        Sex.Male => 2_000,
        Sex.Female => 1_600,
        _ => 1_800,
    };

    public static int ExerciseExtra(ExerciseLevel level) => level switch
    {
        ExerciseLevel.About30Minutes => 350,
        ExerciseLevel.About1Hour => 700,
        ExerciseLevel.TwoHoursPlus => 1_000,
        _ => 0,
    };

    public static bool CanBePregnant(Sex sex) => sex == Sex.Female;

    public static GoalEstimate Suggest(GoalProfile profile)
    {
        if (profile.FluidRestriction)
        {
            return Blocked("If a doctor has given you a fluid amount, or you have a heart, kidney or liver condition, " +
                           "please use the goal from your doctor instead of an estimate.");
        }

        // The form only offers Female/Male; Unspecified is the "not chosen yet" state (and old saved answers).
        if (profile.Sex == Sex.Unspecified || profile.Age is not { } age || profile.WeightKg is not { } weightKg)
        {
            return Blocked("Choose female or male, then enter your age and weight to see a suggestion.");
        }

        if (age < MinAge)
        {
            return Blocked($"Suggestions are for adults ({MinAge}+). Ask a parent, doctor or school nurse for a goal that fits you.");
        }

        if (age > MaxAge || weightKg is < MinWeightKg or > MaxWeightKg)
        {
            return Blocked("Check your age and weight; they look outside the normal range.");
        }

        var lines = new List<GoalLine>();
        var notes = new List<string>();

        var baseMl = (int)Math.Round(weightKg * MlPerKg);
        lines.Add(new GoalLine($"Body weight: {weightKg:0.#} kg × {MlPerKg} ml", baseMl));

        var floor = FloorFor(profile.Sex);
        if (baseMl < floor)
        {
            var who = profile.Sex switch
            {
                Sex.Male => "men",
                Sex.Female => "women",
                _ => "adults",
            };
            lines.Add(new GoalLine($"Raised to the usual minimum for {who}", floor - baseMl));
        }

        var exercise = ExerciseExtra(profile.Exercise);
        if (exercise > 0)
        {
            var label = profile.Exercise switch
            {
                ExerciseLevel.About30Minutes => "about 30 min",
                ExerciseLevel.About1Hour => "about 1 hour",
                _ => "2 hours or more",
            };
            lines.Add(new GoalLine($"Exercise ({label} a day)", exercise));
        }

        if (profile.HotClimate)
        {
            lines.Add(new GoalLine("Hot or humid weather / outdoor work", 500));
        }

        if (profile.TakesCreatine)
        {
            lines.Add(new GoalLine("Creatine", 500));
        }

        if (CanBePregnant(profile.Sex) && profile.Pregnant)
        {
            lines.Add(new GoalLine("Pregnancy", 300));
        }

        if (CanBePregnant(profile.Sex) && profile.Breastfeeding)
        {
            lines.Add(new GoalLine("Breastfeeding", 700));
        }

        var total = RoundTo(lines.Sum(l => l.Ml), 50);
        if (total > CapMl)
        {
            notes.Add($"Capped at {CapMl:N0} ml. If you need more than this (for example long endurance training), " +
                      "ask a doctor or sports dietitian.");
            total = CapMl;
        }

        if (total >= 3_500)
        {
            notes.Add("Spread it through the day. Don't drink more than about 1 liter in a single hour.");
        }

        if (profile.TakesCreatine)
        {
            notes.Add("Extra water with creatine is a common recommendation, not a strict rule.");
        }

        if (age >= 65)
        {
            notes.Add("Thirst gets weaker with age, so sip regularly even when you don't feel thirsty.");
        }

        notes.Add("This is a general estimate, not medical advice. Your needs can be different; " +
                  "pale yellow urine is a simple sign you're drinking enough.");

        return new GoalEstimate(total, lines, notes, null);
    }

    /// <summary>Assumed waking hours in a day, used to split the daily total across office hours.</summary>
    public const int WakingHours = 16;

    public static double OfficeHours(TimeOnly start, TimeOnly end)
    {
        if (start == end)
        {
            return 0;
        }

        var span = end > start ? end - start : TimeSpan.FromHours(24) - (start - end);
        return span.TotalHours;
    }

    /// <summary>
    /// The part of a whole-day total that falls in office hours, assuming water is drunk evenly
    /// over ~16 waking hours. Returns null when office hours cover the whole waking day.
    /// </summary>
    public static int? OfficeShare(int dailyMl, TimeOnly start, TimeOnly end)
    {
        var hours = OfficeHours(start, end);
        if (hours <= 0 || hours >= WakingHours)
        {
            return null;
        }

        var ml = RoundTo((int)Math.Round(dailyMl * hours / WakingHours), 50);
        return ml >= UserPreferences.MinGoalMl ? ml : null;
    }

    /// <summary>The goal rounded to the nearest half bottle, so it reads nicely in bottles.</summary>
    public static int RoundToHalfBottles(int ml, int bottleMl)
    {
        if (bottleMl <= 0)
        {
            return ml;
        }

        var halves = Math.Max(1, Math.Round(ml * 2.0 / bottleMl, MidpointRounding.AwayFromZero));
        return (int)Math.Round(halves * bottleMl / 2);
    }

    private static int RoundTo(int value, int step) =>
        (int)(Math.Round(value / (double)step, MidpointRounding.AwayFromZero) * step);

    private static GoalEstimate Blocked(string reason) => new(null, [], [], reason);
}
