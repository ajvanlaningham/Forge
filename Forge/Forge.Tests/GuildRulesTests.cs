using Forge.Guild;

namespace Forge.Tests;

public class GuildRulesTests
{
    // Mon 2026-11-09 .. Sun 2026-11-15
    private static readonly DateOnly Mon = new(2026, 11, 9);
    private static string K(DateOnly d) => d.ToString("yyyy-MM-dd");

    private static GuildMember M(string id, DateOnly joined, int target, Dictionary<DateOnly, GuildDayKind> days, params DateOnly[] forged)
        => new(id, joined, new MemberStatus(id, "human", "warrior", 1, target, null, 1, 0,
            days.Select(kv => new DayMark(K(kv.Key), kv.Value)).ToList(),
            forged.Select(K).ToList(), []));

    private static Dictionary<DateOnly, GuildDayKind> Trained(params int[] offsets)
        => offsets.ToDictionary(o => Mon.AddDays(o), _ => GuildDayKind.Trained);

    [Fact]
    public void WeeklyGoal_Is75PercentOfTargets_RoundedUp()
    {
        var members = new[] { M("a", Mon, 4, new()), M("b", Mon, 3, new()) };
        Assert.Equal(6, GuildRules.WeeklyGoal(members, Mon)); // 7 × 0.75 = 5.25 → 6
    }

    [Fact]
    public void WeeklyQuest_MetWhenTheGuildLogsEnough()
    {
        var a = M("a", Mon, 4, Trained(0, 1, 3, 5));
        var b = M("b", Mon, 3, Trained(0, 2));
        Assert.True(GuildRules.WeeklyQuestMet(new[] { a, b }, Mon));     // 6 of 6
        var b2 = M("b", Mon, 3, Trained(0));
        Assert.False(GuildRules.WeeklyQuestMet(new[] { a, b2 }, Mon));   // 5 of 6
    }

    [Fact]
    public void WeeklyQuest_PaysOnlyThoseWhoTrained()
    {
        var a = M("a", Mon, 3, Trained(0, 1, 2, 3, 4));
        var b = M("b", Mon, 3, new());
        var members = new[] { a, b };
        Assert.True(GuildRules.WeeklyQuestMet(members, Mon)); // 5 ≥ ceil(4.5)
        Assert.Contains(GuildRules.RewardsFor(members, "a", Mon.AddDays(6)), r => r.Key == "week:2026-11-09" && r.Earned);
        Assert.Contains(GuildRules.RewardsFor(members, "b", Mon.AddDays(6)), r => r.Key == "week:2026-11-09" && !r.Earned);
    }

    [Fact]
    public void AGuildOfOne_EarnsNothing()
    {
        var a = M("a", Mon, 1, Trained(0, 1, 2, 3, 4, 5, 6), Mon.AddDays(3));
        Assert.All(GuildRules.RewardsFor(new[] { a }, "a", Mon.AddDays(6)), r => Assert.False(r.Earned));
        Assert.Equal(0, GuildRules.Banners(new[] { a }));
    }

    [Fact]
    public void Hearth_LitWhenEveryoneMoved_RestCounts()
    {
        var a = M("a", Mon, 3, new() { [Mon] = GuildDayKind.Trained, [Mon.AddDays(1)] = GuildDayKind.Trained });
        var b = M("b", Mon, 3, new() { [Mon] = GuildDayKind.ActiveRest });
        Assert.True(GuildRules.HearthLit(new[] { a, b }, Mon));
        Assert.False(GuildRules.HearthLit(new[] { a, b }, Mon.AddDays(1)));
    }

    [Fact]
    public void BeforeJoining_DaysDontCount()
    {
        var a = M("a", Mon, 3, Trained(0, 1, 2));
        var late = M("b", Mon.AddDays(2), 3, Trained(0, 1, 2)); // joined Wednesday
        var members = new[] { a, late };
        Assert.False(GuildRules.HearthLit(members, Mon.AddDays(1)) && late.On(Mon.AddDays(1)) != GuildDayKind.None);
        Assert.Equal(GuildDayKind.None, late.On(Mon));
        // Monday only had member a in the guild: one member can't light the hearth.
        Assert.False(GuildRules.HearthLit(members, Mon));
        Assert.True(GuildRules.HearthLit(members, Mon.AddDays(2)));
    }

    [Fact]
    public void Banners_CountQuestsEveryoneForgedSinceJoining()
    {
        var a = M("a", Mon, 3, new(), Mon.AddDays(-30), Mon.AddDays(1), Mon.AddDays(5));
        var b = M("b", Mon, 3, new(), Mon.AddDays(4));
        Assert.Equal(1, GuildRules.Banners(new[] { a, b })); // a's pre-guild forge doesn't count
        var rewards = GuildRules.RewardsFor(new[] { a, b }, "b", Mon.AddDays(6));
        Assert.Single(rewards, r => r.Key.StartsWith("banner:") && r.Earned);
    }

    [Fact]
    public void Rewards_StayInsideTheWindow_AndAfterJoining()
    {
        var a = M("a", Mon.AddDays(-60), 3, new());
        var b = M("b", Mon.AddDays(3), 3, new());
        var rewards = GuildRules.RewardsFor(new[] { a, b }, "b", Mon.AddDays(6));
        var hearths = rewards.Where(r => r.Key.StartsWith("hearth:")).Select(r => r.Key).ToList();
        Assert.Equal(4, hearths.Count); // Thu..Sun
        Assert.Equal("hearth:2026-11-12", hearths[0]);
        var longA = GuildRules.RewardsFor(new[] { a, b }, "a", Mon.AddDays(6), windowDays: 21);
        Assert.Equal(21, longA.Count(r => r.Key.StartsWith("hearth:")));
    }

    [Fact]
    public void Validate_RejectsOversizedOrOddInput()
    {
        var ok = new MemberStatus("Alex", "halfling", "ranger", 3, 4, "Copper Patched Cloak", 1, 2,
            [new DayMark("2026-11-11", GuildDayKind.Trained)], ["2026-10-01"], [new GearMark(0, "Copper")]);
        Assert.Null(GuildLimits.Validate(ok));
        Assert.Equal("name", GuildLimits.Validate(ok with { Name = new string('x', 40) }));
        Assert.Equal("days", GuildLimits.Validate(ok with { Days = [new DayMark("yesterday", GuildDayKind.Trained)] }));
        Assert.Equal("gear", GuildLimits.Validate(ok with { Gear = [new GearMark(7, "Copper")] }));
    }
}
