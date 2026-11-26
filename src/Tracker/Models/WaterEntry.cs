namespace Tracker.Models;

public sealed record WaterEntry
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public int AmountMl { get; init; }
    public DateTimeOffset LoggedAt { get; init; }
    public string? Label { get; init; }
}
