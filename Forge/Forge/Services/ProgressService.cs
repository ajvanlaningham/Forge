using System.Globalization;
using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Views;

namespace Forge.Services
{
    /// <summary>
    /// Consistency feedback (PBIs 4.2, 4.3): the daily streak, and the level-up moment.
    /// </summary>
    public sealed class ProgressService
    {
        private const string LastCelebratedKey = "Progress.LastCelebratedLevel";

        private readonly IProgramService _programs;
        private readonly IStatsStore _stats;
        private bool _showing;

        public ProgressService(IProgramService programs, IStatsStore stats)
        {
            _programs = programs;
            _stats = stats;
        }

        /// <summary>Current and longest daily streak: days you trained or did active rest.</summary>
        public async Task<(int Current, int Longest)> GetStreakAsync()
        {
            // Same days the XP multiplier reads (Decision 26), so the flame and the ×1.2 agree.
            var days = await _programs.ActiveDaysAsync();
            var today = DateOnly.FromDateTime(DateTime.Today);
            return (StreakMath.Current(days, today), StreakMath.Longest(days));
        }

        /// <summary>
        /// Shows the level-up screen if a level was crossed since the last one celebrated. Fires
        /// exactly once per level: the celebrated level is persisted, so navigation and restarts
        /// don't repeat it. The first ever check just records the current level — an existing
        /// install shouldn't get a surprise party for levels earned before this shipped.
        /// </summary>
        public async Task CheckLevelUpAsync()
        {
            if (_showing) return;
            await _stats.InitAsync();
            var level = GameMath.LevelFromXp((await _stats.GetUserStatsAsync()).Xp);
            var last = Preferences.Get(LastCelebratedKey, 0);

            if (last == 0 || level < last)
            {
                // First run, or XP was reclaimed (an undo): track the level quietly. Re-earning a
                // level after an undo doesn't celebrate twice.
                Preferences.Set(LastCelebratedKey, Math.Max(level, last == 0 ? level : last));
                return;
            }
            if (level <= last) return;

            Preferences.Set(LastCelebratedKey, level);
            _showing = true;
            try
            {
                await Shell.Current.Navigation.PushModalAsync(new LevelUpPage(level));
            }
            finally
            {
                _showing = false;
            }
        }
    }
}
