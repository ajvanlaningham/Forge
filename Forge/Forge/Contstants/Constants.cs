using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Forge.Contstants
{
    public static class GameConstants
    {
        // Leveling
        public const int XpPerLevel = 1000;

        // Stat scoring
        public static class Stats
        {
            public const int MinScore = 1;
            public const int MaxScore = 20;
            public const double ScoreStepRatio = 0.10; // ±10% ⇒ ±1 step
        }

        // Storage
        public static class Db
        {
            public const string FileName = "forge.db3";
        }
    }

    public static class GameMath
    {
        public static double LevelProgress(int xp, int xpPerLevel = GameConstants.XpPerLevel)
            => Math.Clamp((double)xp / xpPerLevel, 0.0, 1.0);

        // Keep your old ScoreFrom, but parameterized by constants
        public static int ScoreFrom(double baseline, double current, bool inverse = false)
        {
            if (baseline <= 0 || current <= 0) return GameConstants.Stats.MinScore;

            var ratio = inverse ? (baseline / current) : (current / baseline);
            var steps = Math.Round((ratio - 1.0) / GameConstants.Stats.ScoreStepRatio);
            var raw = GameConstants.Stats.MinScore + (int)steps;

            return Math.Clamp(raw, GameConstants.Stats.MinScore, GameConstants.Stats.MaxScore);
        }
    }
}
