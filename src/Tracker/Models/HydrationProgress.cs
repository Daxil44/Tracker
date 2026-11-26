namespace Tracker.Models;

/// <summary>Derived, read-only view of a day's progress. Bottle counts are deliberately not rounded.</summary>
public sealed record HydrationProgress(int IntakeMl, int GoalMl, int BottleCapacityMl)
{
    public int RemainingMl => Math.Max(0, GoalMl - IntakeMl);
    public double Fraction => GoalMl <= 0 ? 0 : (double)IntakeMl / GoalMl;
    public int PercentDisplay => (int)Math.Floor(Fraction * 100);
    public bool GoalReached => GoalMl > 0 && IntakeMl >= GoalMl;

    public double BottlesCompleted => BottleCapacityMl <= 0 ? 0 : (double)IntakeMl / BottleCapacityMl;
    public double BottlesRequired => BottleCapacityMl <= 0 ? 0 : (double)GoalMl / BottleCapacityMl;
    public double BottlesRemaining => BottleCapacityMl <= 0 ? 0 : (double)RemainingMl / BottleCapacityMl;
}

public sealed record DaySummary(DateOnly Date, int TotalMl, int GoalMl)
{
    public double Fraction => GoalMl <= 0 ? 0 : (double)TotalMl / GoalMl;
    public bool GoalMet => GoalMl > 0 && TotalMl >= GoalMl;
}
