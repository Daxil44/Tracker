namespace Tracker.Models;

public enum Sex
{
    Female,
    Male,
    /// <summary>Not chosen yet. Kept so older saved answers still load.</summary>
    Unspecified,
}

public enum ExerciseLevel
{
    None,
    About30Minutes,
    About1Hour,
    TwoHoursPlus,
}

public enum WeightUnit
{
    Kg,
    Lb,
}

/// <summary>
/// Answers from the "suggest a goal" questions, kept so the form is prefilled next time.
/// Stays in this browser only (and in the user's own backup file).
/// Uses <c>set</c> rather than <c>init</c> so missing fields keep their defaults when read from storage.
/// </summary>
public sealed record GoalProfile
{
    public Sex Sex { get; set; } = Sex.Unspecified;
    public int? Age { get; set; }
    public double? WeightKg { get; set; }
    public WeightUnit WeightUnit { get; set; } = WeightUnit.Kg;
    public ExerciseLevel Exercise { get; set; } = ExerciseLevel.None;
    public bool HotClimate { get; set; }
    public bool TakesCreatine { get; set; }
    public bool Pregnant { get; set; }
    public bool Breastfeeding { get; set; }

    /// <summary>Heart, kidney or liver condition, or told by a doctor to limit fluids.</summary>
    public bool FluidRestriction { get; set; }
}
