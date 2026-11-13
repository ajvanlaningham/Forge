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
    /// The guild's three shared goals (Decisions 24, 26). A guild is official at
    /// <see cref="OfficialSize"/> members; with fewer it's a pre-guild that earns nothing extra.
    /// <list type="bullet">
    /// <item><b>Joint weekly quest:</b> the guild logs training days adding up to 75% of everyone's
    /// weekly targets (the same 75% forging uses). Members who trained that week earn
    /// <see cref="WeeklyQuestXp"/>.</item>
    /// <item><b>Campfire:</b> a day at least two thirds of the members moved (trained or active
    /// rest). Each member who moved that day earns <see cref="CampfireXp"/>; counted per day so
    /// undo works.</item>
    /// <item><b>Banners:</b> one each time every member has forged another quest since joining.
    /// Everyone earns <see cref="BannerXp"/> per banner.</item>
    /// </list>
    /// </summary>
    public static class GuildRules
    {
        public const int WeeklyQuestXp = 100;
        public const int CampfireXp = 15;
        public const int BannerXp = 200;
        public const double WeeklyShare = 0.75;
        /// <summary>A guild becomes official, and starts earning bonuses, at three members (Decision 26).</summary>
        public const int OfficialSize = 3;
        /// <summary>Share of members who must move for the campfire to be lit (Decision 26): 2 of 3, 4 of 6.</summary>
        public const double CampfireShare = 2.0 / 3.0;

        public static bool IsOfficial(int members) => members >= OfficialSize;

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
            => IsOfficial(On(members, WeekMath.SundayOf(anyDay)).Count)
               && TrainedDays(members, anyDay) >= WeeklyGoal(members, anyDay);

        /// <summary>How many of that day's members moved, out of how many.</summary>
        public static (int Moved, int Members) Campfire(IEnumerable<GuildMember> members, DateOnly day)
        {
            var present = On(members, day);
            return (present.Count(m => m.On(day) != GuildDayKind.None), present.Count);
        }

        /// <summary>An official guild, and at least two thirds of that day's members moved.</summary>
        public static bool CampfireLit(IEnumerable<GuildMember> members, DateOnly day)
        {
            var (moved, count) = Campfire(members, day);
            // Small epsilon: 2 of 3 is exactly 0.666…, which must count.
            return IsOfficial(count) && moved >= count * CampfireShare - 1e-9;
        }

        public static int Banners(IReadOnlyCollection<GuildMember> members)
            => !IsOfficial(members.Count) ? 0 : members.Min(m => m.ForgedSinceJoining);

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
                // Key kept as "hearth:" so rows paid before the rename still reconcile.
                rewards.Add(new GuildReward($"hearth:{WeekMath.DateKey(day)}",
                    CampfireLit(members, day) && me.On(day) != GuildDayKind.None, CampfireXp));

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
