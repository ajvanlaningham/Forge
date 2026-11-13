namespace Forge.Guild
{
    /// <summary>What a member did on a day, as the guild sees it. Nothing more specific ever leaves the phone.</summary>
    public enum GuildDayKind
    {
        None = 0,
        ActiveRest = 1,
        Trained = 2,
    }

    public sealed record DayMark(string Date, GuildDayKind Kind);

    /// <summary>A forged piece the hero wears: gear slot (0 weapon, 1 defence, 2 light) and metal.</summary>
    public sealed record GearMark(int Slot, string Metal);

    /// <summary>
    /// Everything a phone tells the guild about its player (Decision 24): game state only. No
    /// weights, sets, bodyweight, heart rate, sleep, steps or test results.
    /// </summary>
    public sealed record MemberStatus(
        string Name,
        string Race,
        string Class,
        int Level,
        int WeeklyTarget,
        string? QuestName,
        int QuestWeek,
        int Streak,
        IReadOnlyList<DayMark> Days,
        IReadOnlyList<string> ForgedDates,
        IReadOnlyList<GearMark> Gear);

    // ---- API messages (shared by the app and the nidavellir server) ----

    public sealed record CreateGuildRequest(string GuildName, MemberStatus Member);
    public sealed record JoinGuildRequest(string InviteCode, MemberStatus Member);
    public sealed record JoinedResponse(string GuildId, string GuildName, string InviteCode, string MemberId, string Token);

    public sealed record GuildMemberDto(string Id, DateTime JoinedUtc, DateTime UpdatedUtc, MemberStatus Status);
    public sealed record GuildDto(string Id, string Name, string InviteCode, IReadOnlyList<GuildMemberDto> Members);

    public static class GuildLimits
    {
        public const int MaxMembers = 6;
        public const int MaxNameLength = 32;
        public const int MaxGuildNameLength = 40;
        /// <summary>Days of history a phone sends; enough for the reward window and this week.</summary>
        public const int DaysSent = 28;
        public const int MaxDays = 60;
        public const int MaxForged = 200;
        public const int MaxGear = 12;
        /// <summary>An encrypted backup upload, compressed. Forge's database is well under 1 MB today.</summary>
        public const int MaxBackupBytes = 16 * 1024 * 1024;

        /// <summary>Server-side sanity check on what a phone sends: bounded sizes, known values.</summary>
        public static string? Validate(MemberStatus m)
        {
            if (string.IsNullOrWhiteSpace(m.Name) || m.Name.Length > MaxNameLength) return "name";
            if (m.Race.Length is 0 or > 24 || m.Class.Length is 0 or > 24) return "hero";
            if (m.Level is < 0 or > 10_000 || m.WeeklyTarget is < 0 or > 7 || m.Streak is < 0 or > 100_000) return "numbers";
            if ((m.QuestName?.Length ?? 0) > 80 || m.QuestWeek is < 0 or > 8) return "quest";
            if (m.Days.Count > MaxDays || m.Days.Any(d => !DateOnly.TryParseExact(d.Date, "yyyy-MM-dd", out _) || !Enum.IsDefined(d.Kind))) return "days";
            if (m.ForgedDates.Count > MaxForged || m.ForgedDates.Any(d => !DateOnly.TryParseExact(d, "yyyy-MM-dd", out _))) return "forged";
            if (m.Gear.Count > MaxGear || m.Gear.Any(g => g.Slot is < 0 or > 4 || g.Metal.Length is 0 or > 16)) return "gear";
            return null;
        }
    }
}
