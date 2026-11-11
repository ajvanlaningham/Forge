namespace Forge.Models
{
    /// <summary>Cardio minutes logged in a week against the weekly goal.</summary>
    public sealed class WeeklyConditioningProgress
    {
        public DateOnly WeekStart { get; init; }
        public DateOnly WeekEnd { get; init; }

        public int Minutes { get; init; }

        public int GoalMinutes { get; init; }

        public double Ratio => GoalMinutes <= 0 ? 0 : Math.Clamp((double)Minutes / GoalMinutes, 0.0, 1.0);

        public bool Completed => Minutes >= GoalMinutes;
    }
}
