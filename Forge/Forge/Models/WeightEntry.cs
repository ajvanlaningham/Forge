namespace Forge.Models
{
    /// <summary>
    /// Domain model for one bodyweight log. Weight is always in pounds; the UI toggle in
    /// <see cref="Forge.Services.UserSettings"/> only affects display, not what is stored.
    /// <see cref="Pounds"/> is <see cref="decimal"/> so the round-trip through the DB never
    /// loses precision — convert to <c>double</c> only at UI/math boundaries.
    /// </summary>
    public sealed class WeightEntry
    {
        public DateOnly Date { get; init; }
        public decimal Pounds { get; init; }
        public string? Note { get; init; }
        public DateTimeOffset LoggedAt { get; init; }
    }
}
