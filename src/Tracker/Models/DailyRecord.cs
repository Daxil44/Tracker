using System.Text.Json.Serialization;

namespace Tracker.Models;

/// <summary>One calendar day of intake. The goal is snapshotted so history stays accurate if the goal changes later.</summary>
public sealed class DailyRecord
{
    public DateOnly Date { get; set; }
    public int GoalMl { get; set; }
    public List<WaterEntry> Entries { get; set; } = [];

    [JsonIgnore]
    public int TotalMl => Entries.Sum(e => e.AmountMl);
}

/// <summary>Root object persisted to localStorage. Days are keyed by ISO date (yyyy-MM-dd).</summary>
public sealed class HydrationHistory
{
    public int Version { get; set; } = 1;
    public Dictionary<string, DailyRecord> Days { get; set; } = [];
}
