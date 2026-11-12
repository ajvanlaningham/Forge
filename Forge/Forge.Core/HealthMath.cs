using System.Globalization;

namespace Forge.Constants
{
    /// <summary>Where a daily check-in value came from. Manual always wins over synced.</summary>
    public enum DataSource
    {
        None = 0,
        Manual = 1,
        Synced = 2,
    }

    /// <summary>One sleep stage inside a session. <see cref="Stage"/> uses Health Connect's numbering.</summary>
    public sealed record SleepStage(DateTimeOffset Start, DateTimeOffset End, int Stage);

    public sealed record SleepSession(DateTimeOffset Start, DateTimeOffset End, IReadOnlyList<SleepStage> Stages);

    /// <summary>What a health source knows about one day. Null = no data.</summary>
    public sealed record HealthDay(int? RestingHr, int? SleepMinutes, int? Steps);

    /// <summary>
    /// Pure rules for daily recovery data (resting HR, sleep, steps), shared by every source
    /// (Health Connect, HealthKit, hand entry).
    /// </summary>
    public static class HealthMath
    {
        // Health Connect's SleepSessionRecord stage types that are not sleep.
        public const int StageAwake = 1;
        public const int StageOutOfBed = 3;
        public const int StageAwakeInBed = 7;

        /// <summary>
        /// A day's sleep is the night that ended that morning: sessions ending between 18:00 the
        /// evening before and 18:00 on the day, local time. An afternoon nap counts toward the
        /// day it was taken; a late night counts toward the morning after.
        /// </summary>
        public static (DateTimeOffset From, DateTimeOffset To) SleepWindow(DateOnly date, TimeZoneInfo zone)
        {
            var to = date.ToDateTime(new TimeOnly(18, 0));
            var from = to.AddDays(-1);
            return (new DateTimeOffset(from, zone.GetUtcOffset(from)), new DateTimeOffset(to, zone.GetUtcOffset(to)));
        }

        /// <summary>
        /// Minutes asleep: each session's length minus its awake / out-of-bed stages. Sessions
        /// from two sources covering the same night would double-count, so overlapping
        /// sessions are merged first and only the longest night's stages are subtracted.
        /// </summary>
        public static int? SleepMinutes(IEnumerable<SleepSession> sessions)
        {
            var list = sessions.Where(s => s.End > s.Start).OrderBy(s => s.Start).ToList();
            if (list.Count == 0) return null;

            var total = TimeSpan.Zero;
            var groupStart = list[0].Start;
            var groupEnd = list[0].End;
            var groupAwake = Awake(list[0]);
            foreach (var s in list.Skip(1))
            {
                if (s.Start < groupEnd)
                {
                    if (s.End > groupEnd) groupEnd = s.End;
                    groupAwake = Max(groupAwake, Awake(s));
                    continue;
                }
                total += groupEnd - groupStart - groupAwake;
                groupStart = s.Start;
                groupEnd = s.End;
                groupAwake = Awake(s);
            }
            total += groupEnd - groupStart - groupAwake;
            return Math.Max(0, (int)Math.Round(total.TotalMinutes));
        }

        private static TimeSpan Awake(SleepSession s)
            => s.Stages
                .Where(st => st.Stage is StageAwake or StageOutOfBed or StageAwakeInBed && st.End > st.Start)
                .Aggregate(TimeSpan.Zero, (acc, st) => acc + (st.End - st.Start));

        private static TimeSpan Max(TimeSpan a, TimeSpan b) => a > b ? a : b;

        /// <summary>Synced data may fill a field only if nobody typed it by hand.</summary>
        public static bool SyncMayWrite(DataSource current) => current != DataSource.Manual;

        /// <summary>"7:30", "7h30", "7.5" or "7" (hours) → minutes. Null when it isn't a plausible night.</summary>
        public static int? ParseSleep(string? text)
        {
            var t = (text ?? "").Trim().ToLowerInvariant().Replace("h", ":").Replace("m", "");
            if (t.Length == 0) return null;
            int minutes;
            if (t.Contains(':'))
            {
                var parts = t.Split(':', StringSplitOptions.TrimEntries);
                if (!int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var h)) return null;
                var m = 0;
                if (parts.Length > 1 && parts[1].Length > 0
                    && (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out m) || m >= 60)) return null;
                minutes = h * 60 + m;
            }
            else
            {
                if (!double.TryParse(t.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var hours)) return null;
                minutes = (int)Math.Round(hours * 60);
            }
            return minutes is > 0 and <= 24 * 60 ? minutes : null;
        }

        /// <summary>480 → "8:00".</summary>
        public static string FormatSleep(int minutes) => $"{minutes / 60}:{minutes % 60:00}";

        /// <summary>A resting heart rate a person could actually have.</summary>
        public static bool PlausibleRestingHr(int bpm) => bpm is >= 25 and <= 150;
    }
}
