using Forge.Constants;

namespace Forge.Tests;

public class WeightMathTests
{
    private static readonly DateOnly Today = new(2026, 8, 24);

    [Fact]
    public void UnitLabel_ShowsLbOrKg()
    {
        Assert.Equal("lb", WeightMath.UnitLabel(WeightUnit.Pounds));
        Assert.Equal("kg", WeightMath.UnitLabel(WeightUnit.Kilograms));
    }

    [Fact]
    public void PoundsToKilograms_UsesTheNistFactor()
    {
        // 200 lb -> 90.7185 kg, checked against NIST 0.45359237 kg/lb.
        Assert.Equal(90.7185, WeightMath.PoundsToKilograms(200), 3);
    }

    [Fact]
    public void RoundTripping_PoundsThroughKilograms_IsExact()
    {
        // The conversion is used both ways at the display boundary; drift here would
        // silently corrupt a stored value on every save.
        Assert.Equal(185.5, WeightMath.KilogramsToPounds(WeightMath.PoundsToKilograms(185.5)), 6);
    }

    [Fact]
    public void ToDisplay_LeavesPoundsUntouched()
        => Assert.Equal(180.0, WeightMath.ToDisplay(180.0, WeightUnit.Pounds));

    [Fact]
    public void ToDisplay_ConvertsWhenUnitIsKilograms()
        => Assert.Equal(WeightMath.PoundsToKilograms(180.0),
                        WeightMath.ToDisplay(180.0, WeightUnit.Kilograms));

    [Fact]
    public void ToStoredPounds_FromKilograms_ConvertsBack()
        => Assert.Equal(180.0, WeightMath.ToStoredPounds(WeightMath.PoundsToKilograms(180.0),
                                                          WeightUnit.Kilograms),
                        6);

    [Fact]
    public void MovingAverage_ReturnsNullOnEmpty()
        => Assert.Null(WeightMath.MovingAverage(Array.Empty<(DateOnly, double)>(), Today));

    [Fact]
    public void MovingAverage_IsMeanOverOnlyTheDaysWithEntries()
    {
        // Three of the seven days have logs — average is over 3, not 7.
        var entries = new[]
        {
            (Today.AddDays(-6), 200.0),
            (Today.AddDays(-3), 210.0),
            (Today,             190.0),
        };
        Assert.Equal(200.0, WeightMath.MovingAverage(entries, Today)!.Value, 6);
    }

    [Fact]
    public void MovingAverage_IgnoresEntriesOutsideTheWindow()
    {
        // The 7-day window ends inclusive on today; anything older than today-6 is ignored.
        var entries = new[]
        {
            (Today.AddDays(-30), 100.0),
            (Today.AddDays(-7),  100.0),
            (Today.AddDays(-6),  200.0),
            (Today,              210.0),
        };
        Assert.Equal(205.0, WeightMath.MovingAverage(entries, Today)!.Value, 6);
    }

    [Fact]
    public void MovingAverage_IgnoresFutureDates()
    {
        // Backfill is allowed but the DatePicker clamps to today; a stray future row must
        // still not skew the average if one somehow shows up.
        var entries = new[]
        {
            (Today,             200.0),
            (Today.AddDays(1),  400.0),
        };
        Assert.Equal(200.0, WeightMath.MovingAverage(entries, Today)!.Value, 6);
    }

    [Fact]
    public void MovingAverage_WindowIsExactlySevenDaysInclusive()
    {
        // Six days back is in, seven days back is out — that is the whole "7-day" contract.
        Assert.Equal(WeightMath.MovingAverageWindowDays, 7);

        var inWindow = new[] { (Today.AddDays(-6), 100.0) };
        Assert.Equal(100.0, WeightMath.MovingAverage(inWindow, Today)!.Value, 6);

        var outOfWindow = new[] { (Today.AddDays(-7), 100.0) };
        Assert.Null(WeightMath.MovingAverage(outOfWindow, Today));
    }

    [Fact]
    public void XpPerWeightLog_IsTwentyFive()
        => Assert.Equal(25, WeightMath.XpPerWeightLog);

    [Fact]
    public void MovingAverageWithSample_ReturnsNullOnEmpty()
        => Assert.Null(WeightMath.MovingAverageWithSample(
            Array.Empty<(DateOnly, double)>(), Today));

    [Fact]
    public void MovingAverageWithSample_SingleEntryReportsOneDay()
    {
        // The card label reads "1-day avg" off SampleDays for a brand-new user.
        var entries = new[] { (Today, 195.0) };
        var result = WeightMath.MovingAverageWithSample(entries, Today);
        Assert.NotNull(result);
        Assert.Equal(195.0, result!.Value.Average, 6);
        Assert.Equal(1, result.Value.SampleDays);
    }

    [Fact]
    public void MovingAverageWithSample_CountsOnlyEntriesInsideWindow()
    {
        // The "days sampled" count uses the same window rules as MovingAverage — otherwise
        // the label would claim more history than the average actually saw.
        var entries = new[]
        {
            (Today.AddDays(-30), 100.0),
            (Today.AddDays(-6),  200.0),
            (Today.AddDays(-3),  210.0),
            (Today,              190.0),
        };
        var result = WeightMath.MovingAverageWithSample(entries, Today);
        Assert.NotNull(result);
        Assert.Equal(200.0, result!.Value.Average, 6);
        Assert.Equal(3, result.Value.SampleDays);
    }
}
