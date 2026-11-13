using Forge.Constants;

namespace Forge.Tests;

public class StreakMathTests
{
    private static readonly DateOnly Today = new(2026, 11, 11);
    private static IEnumerable<DateOnly> Days(params int[] ago) => ago.Select(a => Today.AddDays(-a));

    [Fact] public void Nothing_IsZero() => Assert.Equal(0, StreakMath.Current([], Today));
    [Fact] public void TodayOnly_IsOne() => Assert.Equal(1, StreakMath.Current(Days(0), Today));
    [Fact] public void ThroughToday() => Assert.Equal(3, StreakMath.Current(Days(0, 1, 2), Today));

    [Fact]
    public void TodayNotDoneYet_DoesNotBreakIt()
        => Assert.Equal(2, StreakMath.Current(Days(1, 2), Today));

    [Fact]
    public void AMissedDay_Breaks()
        => Assert.Equal(1, StreakMath.Current(Days(0, 2, 3, 4), Today));

    [Fact]
    public void MissedYesterdayAndToday_IsZero()
        => Assert.Equal(0, StreakMath.Current(Days(2, 3), Today));

    [Fact]
    public void Duplicates_DontInflate()
        => Assert.Equal(2, StreakMath.Current(Days(0, 0, 1, 1), Today));

    [Fact]
    public void Longest_FindsTheBestRun()
        => Assert.Equal(4, StreakMath.Longest(Days(0, 1, 5, 6, 7, 8, 10)));

    // --- Decision 26: the streak multiplier ---

    [Fact]
    public void Multiplier_NeedsYesterday()
    {
        Assert.Equal(1.2, StreakMath.Multiplier(Days(1), Today));
        Assert.Equal(1.2, StreakMath.Multiplier(Days(1, 2, 3), Today));
    }

    [Fact]
    public void Multiplier_TodaysOwnWorkDoesntCount()
        => Assert.Equal(1.0, StreakMath.Multiplier(Days(0), Today));

    [Fact]
    public void Multiplier_AMissResetsIt_WithoutTakingAnything()
        => Assert.Equal(1.0, StreakMath.Multiplier(Days(0, 2, 3), Today));
}
