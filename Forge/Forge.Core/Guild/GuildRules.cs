using Forge.Common;

namespace Forge.Guild
{
    /// <summary>A member as the rules see them: when they joined (local date) and what they reported.</summary>
    public sealed record GuildMember(string Id, DateOnly JoinedOn, MemberStatus Status)
    {
        private Dictionary<DateOnly, GuildDayKind>? _days;

        public GuildDayKind On(DateOnly date)
        {
            _days ??= Status.Days
                .Select(d => (Ok: DateOnly.TryParseExact(d.Date, "yyyy-MM-dd", out var day), Day: day, d.Kind))
                .Where(x => x.Ok)
                .GroupBy(x => x.Day)
                .ToDictionary(g => g.Key, g => g.Max(x => x.Kind));
            return date < JoinedOn ? GuildDayKind.None : _days.GetValueOrDefault(date);
        }

        public bool MemberOn(DateOnly date) => date >= JoinedOn;

        /// <summary>Quests forged since joining: the guild's banners count these.</summary>
        public int ForgedSinceJoining => Status.ForgedDates
            .Count(d => DateOnly.TryParseExact(d, "yyyy-MM-dd", out var day) && day >= JoinedOn);

        public int Target => Status.WeeklyTarget > 0 ? Status.WeeklyTarget : 3;
    }

    /// <summary>One guild bonus for one player: stable key, whether it's earned right now, its XP.</summary>
    public sealed record GuildReward(string Key, bool Earned, int Xp);

    /// <summary>
    /// The guild's three shared goals (Decision 24). All of them need at least two members:
    /// a guild of one earns nothing extra.
    /// <list type="bullet">
    /// <item><b>Joint weekly quest:</b> the guild logs training days adding up to 75% of everyone's
    /// weekly targets (the same 75% forging uses). Members who trained that week earn
    /// <see cref="WeeklyQuestXp"/>.</item>
    /// <item><b>Hearth:</b> a day every member moved (trained or active rest). Everyone earns
    /// <see cref="HearthXp"/>; it's the streak bonus, counted per day so undo works.</item>
    /// <item><b>Banners:</b> one each time every member has forged another quest since joining.
    /// Everyone earns <see cref="BannerXp"/> per banner.</item>
    /// </list>
    /// </summary>
    public static class GuildRules
    {
        public const int WeeklyQuestXp = 100;
        public const int HearthXp = 15;
        public const int BannerXp = 200;
        public const double WeeklyShare = 0.75;

        /// <summary>Members who belonged to the guild on that day.</summary>
        private static List<GuildMember> On(IEnumerable<GuildMember> members, DateOnly day)
            => members.Where(m => m.MemberOn(day)).ToList();

        public static int WeeklyGoal(IEnumerable<GuildMember> members, DateOnly anyDay)
        {
            var sunday = WeekMath.SundayOf(anyDay);
            var targets = On(members, sunday).Sum(m => m.Target);
            return (int)Math.Ceiling(targets * WeeklyShare);
        }

        public static int TrainedDays(IEnumerable<GuildMember> members, DateOnly anyDay)
            => Days(anyDay).Sum(d => members.Count(m => m.On(d) == GuildDayKind.Trained));

        public static int TrainedDays(GuildMember member, DateOnly anyDay)
            => Days(anyDay).Count(d => member.On(d) == GuildDayKind.Trained);

        private static IEnumerable<DateOnly> Days(DateOnly anyDay)
        {
            var monday = WeekMath.MondayOf(anyDay);
            return Enumerable.Range(0, 7).Select(i => monday.AddDays(i));
        }

        public static bool WeeklyQuestMet(IReadOnlyCollection<GuildMember> members, DateOnly anyDay)
            => On(members, WeekMath.SundayOf(anyDay)).Count >= 2
               && TrainedDays(members, anyDay) >= WeeklyGoal(members, anyDay);

        /// <summary>Every member of that day moved, and there were at least two of them.</summary>
        public static bool HearthLit(IEnumerable<GuildMember> members, DateOnly day)
        {
            var present = On(members, day);
            return present.Count >= 2 && present.All(m => m.On(day) != GuildDayKind.None);
        }

        public static int Banners(IReadOnlyCollection<GuildMember> members)
            => members.Count < 2 ? 0 : members.Min(m => m.ForgedSinceJoining);

        /// <summary>
        /// The bonuses one player has earned (or no longer has) within the last <paramref name="windowDays"/>.
        /// Keys outside the window aren't returned, so data that has aged out of what phones send
        /// never takes back XP. Feed each to <see cref="XpLedger.Reconcile"/>.
        /// </summary>
        public static IReadOnlyList<GuildReward> RewardsFor(IReadOnlyCollection<GuildMember> members, string myId, DateOnly today, int windowDays = 21)
        {
            var me = members.FirstOrDefault(m => m.Id == myId);
            if (me is null) return [];
            var rewards = new List<GuildReward>();
            var from = today.AddDays(-(windowDays - 1));
            if (from < me.JoinedOn) from = me.JoinedOn;

            for (var day = from; day <= today; day = day.AddDays(1))
                rewards.Add(new GuildReward($"hearth:{WeekMath.DateKey(day)}", HearthLit(members, day), HearthXp));

            for (var monday = WeekMath.MondayOf(from); monday <= today; monday = monday.AddDays(7))
            {
                var earned = WeeklyQuestMet(members, monday) && TrainedDays(me, monday) > 0;
                rewards.Add(new GuildReward($"week:{WeekMath.DateKey(monday)}", earned, WeeklyQuestXp));
            }

            for (var k = 1; k <= Banners(members); k++)
                rewards.Add(new GuildReward($"banner:{k}", true, BannerXp));

            return rewards;
        }
    }
}
