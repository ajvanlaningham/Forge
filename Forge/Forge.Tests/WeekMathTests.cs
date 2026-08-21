using Forge.Common;

namespace Forge.Tests;

public class WeekMathTests
{
    // 2026-08-17 is a Monday; 2026-08-23 is the Sunday that closes the same week.
    private static readonly DateOnly Monday = new(2026, 8, 17);
    private static readonly DateOnly Sunday = new(2026, 8, 23);

    [Fact]
    public void MondayOf_IsIdempotentOnAMonday()
        => Assert.Equal(Monday, WeekMath.MondayOf(Monday));

    [Fact]
    public void MondayOf_TreatsSundayAsTheEndOfTheWeek()
    {
        // The trap: DayOfWeek puts Sunday at 0, so a naive shift rolls Sunday into next week.
        Assert.Equal(Monday, WeekMath.MondayOf(Sunday));
    }

    [Fact]
    public void MondayOf_ResolvesEveryDayOfTheWeekToTheSameMonday()
    {
        for (var offset = 0; offset < 7; offset++)
            Assert.Equal(Monday, WeekMath.MondayOf(Monday.AddDays(offset)));
    }

    [Fact]
    public void MondayOf_MovesToTheNextWeekOnTheFollowingMonday()
        => Assert.Equal(new DateOnly(2026, 8, 24), WeekMath.MondayOf(new DateOnly(2026, 8, 24)));

    [Fact]
    public void SundayOf_ClosesTheWeekSixDaysAfterItsMonday()
    {
        Assert.Equal(Sunday, WeekMath.SundayOf(Monday));
        Assert.Equal(Sunday, WeekMath.SundayOf(Sunday));
    }

    [Fact]
    public void WeekKey_IsStableForEveryDayInTheWeek()
    {
        var keys = Enumerable.Range(0, 7)
            .Select(offset => WeekMath.WeekKey(Monday.AddDays(offset)))
            .Distinct()
            .ToList();

        Assert.Equal(new[] { "2026-08-17" }, keys);
    }

    [Fact]
    public void DateKey_UsesTheSortableFormatStoredInSqlite()
        => Assert.Equal("2026-08-17", WeekMath.DateKey(Monday));

    [Fact]
    public void MondayOf_HandlesAYearBoundary()
    {
        // 2026-01-01 is a Thursday, so its week started in the previous year.
        Assert.Equal(new DateOnly(2025, 12, 29), WeekMath.MondayOf(new DateOnly(2026, 1, 1)));
    }

    [Fact]
    public void MondayOf_HandlesALeapDay()
    {
        // 2024-02-29 is a Thursday.
        Assert.Equal(new DateOnly(2024, 2, 26), WeekMath.MondayOf(new DateOnly(2024, 2, 29)));
    }
}
