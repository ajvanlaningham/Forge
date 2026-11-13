namespace Forge.Training
{
    /// <summary>How hard a side quest was.</summary>
    public enum Effort { Easy, Moderate, Hard }

    /// <summary>What a side quest stands in for.</summary>
    public enum SideQuestCounts { Rest, Training }

    /// <summary>
    /// Side quests (user, 2026-11-13): training done your own way, logged so it counts. Three hours of
    /// HEMA or a gym class can stand in for the day's quest workout; a yoga practice or a hike can be
    /// the day's active rest. A side quest feeds the same records as the quest (a training day done
    /// "via HEMA", or active rest), so the streak, XP, map, chest and guild all follow on their own,
    /// and the XP is that day's, so it's capped per day by construction. Nothing is moved: a side
    /// quest fills today's stone rather than pushing the workout to tomorrow, so nothing piles up.
    /// </summary>
    public static class SideQuests
    {
        /// <summary>Training days a side quest may fill each week, so the weeks still mostly follow the plan.</summary>
        public const int MaxTrainingPerWeek = 2;

        /// <summary>The least a side quest needs to be suggested as a training day.</summary>
        public const int TrainingMinutes = 30;

        /// <summary>A side quest this big suggests resting the next day.</summary>
        public const int BigDayMinutes = 90;

        /// <summary>Offered before you've saved any of your own.</summary>
        public static readonly string[] Starters = { "HEMA", "Gym class", "Yoga", "Hike", "Run", "Climbing", "Swim", "Sport", "Dance" };

        /// <summary>A training day when it was long and hard enough; anything lighter is a rest day.</summary>
        public static SideQuestCounts Suggest(int minutes, Effort effort)
            => minutes >= TrainingMinutes && effort >= Effort.Moderate ? SideQuestCounts.Training : SideQuestCounts.Rest;

        /// <summary>
        /// Whether a side quest can fill a training day now: one must be on offer today (not already
        /// done), and fewer than <see cref="MaxTrainingPerWeek"/> filled this week.
        /// </summary>
        public static bool CanStandIn(bool trainingOnOffer, int filledThisWeek)
            => trainingOnOffer && filledThisWeek < MaxTrainingPerWeek;

        /// <summary>After a big hard day, the next day's card suggests rest instead (never enforced).</summary>
        public static bool BigDay(int minutes, Effort effort) => effort == Effort.Hard && minutes >= BigDayMinutes;

        /// <summary>An icon key for an activity: a sword for HEMA or fencing, a lotus for yoga…</summary>
        public static string Icon(string activity)
        {
            var a = activity.ToLowerInvariant();
            bool Has(params string[] words) => words.Any(a.Contains);
            if (Has("hema", "sword", "fenc", "sabre", "saber", "longsword", "kendo")) return "sword";
            if (Has("yoga", "pilates", "stretch", "meditat")) return "yoga";
            if (Has("hike", "hiking", "walk", "trek")) return "hike";
            if (Has("run", "jog")) return "run";
            if (Has("climb", "boulder")) return "climb";
            if (Has("swim")) return "swim";
            if (Has("bike", "cycl", "spin")) return "bike";
            if (Has("dance", "zumba")) return "dance";
            if (Has("box", "martial", "judo", "karate", "jiu", "bjj", "muay", "wrestl")) return "fist";
            if (Has("class", "gym", "crossfit", "hiit", "bootcamp", "lift")) return "class";
            if (Has("sport", "ball", "soccer", "football", "tennis", "hockey", "frisbee")) return "sport";
            return "star";
        }

        /// <summary>"45 min", "1 h", "1 h 30", "3 h".</summary>
        public static string Duration(int minutes)
        {
            if (minutes < 60) return $"{minutes} min";
            var h = minutes / 60;
            var m = minutes % 60;
            return m == 0 ? $"{h} h" : $"{h} h {m:00}";
        }
    }
}
