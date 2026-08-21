using Forge.Constants;

namespace Forge.Services
{
    /// <summary>
    /// User-tunable targets, backed by <see cref="Preferences"/>.
    /// </summary>
    /// <remarks>
    /// These are personal goals, not game rules — they must never be compile-time constants,
    /// because changing one should not require a rebuild and redeploy. Values in
    /// <see cref="GameConstants.Defaults"/> are starting points only.
    /// Weight targets (start / milestone / goal) land here with PBI 3.1.
    /// </remarks>
    public static class UserSettings
    {
        private const string WeeklyConditioningGoalKey = "Settings.WeeklyConditioningGoalMinutes";

        /// <summary>Weekly cardio goal in minutes. Defaults to 3 hours.</summary>
        public static int WeeklyConditioningGoalMinutes
        {
            get => Preferences.Get(
                WeeklyConditioningGoalKey,
                GameConstants.Defaults.WeeklyConditioningGoalMinutes);
            set => Preferences.Set(WeeklyConditioningGoalKey, Math.Max(0, value));
        }
    }
}
