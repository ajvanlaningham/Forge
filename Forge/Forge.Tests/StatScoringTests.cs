using Forge.Training;

namespace Forge.Tests;

public class StatScoringTests
{
    private static Measure M(double cur, double baseVal, bool smaller = false) => new("x", cur, baseVal, smaller);

    [Fact]
    public void Baseline_ScoresTen()
        => Assert.Equal(10, StatScoring.Score([M(42, 42)]));

    [Fact]
    public void Doubling_IsPlusTwentyFive_AndHalvingClampsAtOne()
    {
        Assert.Equal(35, StatScoring.Score([M(84, 42)]));
        Assert.Equal(1, StatScoring.Score([M(21, 42)]));        // 10 − 25 → clamped
        Assert.Equal(60, StatScoring.Score([M(4, 1)]));          // two doublings
    }

    [Fact]
    public void ARetest_GainsAtMostTen()
    {
        Assert.Equal(20, StatScoring.Score([M(84, 42)], previousScore: 10));
        Assert.Equal(8, StatScoring.Score([M(40, 42)], previousScore: 10));   // drops aren't capped
    }

    [Fact]
    public void SmallerIsBetter_InvertsTheRatio()
    {
        // Recovery pulse 120 → 96: 25% lower is a ratio of 1.25.
        Assert.Equal(18, StatScoring.Score([M(96, 120, smaller: true)]));
    }

    [Fact]
    public void SeveralMeasures_AverageTheirLogRatios_AndSkipTheUncomparable()
    {
        // One doubled (+1), one unchanged (0): mean 0.5 → 10 + 12.5.
        Assert.Equal(23, StatScoring.Score([M(2, 1), M(5, 5), M(3, 0)]));
        Assert.Equal(17, StatScoring.Score([], previousScore: 17));
    }

    [Fact]
    public void Measures_RelativeStrength_ShoulderReach_BothSides()
    {
        Assert.Equal(StrengthMath.Epley(50, 8) / 250, StatMeasures.RelativeStrength(50, 8, 250), 9);
        Assert.Equal(0, StatMeasures.RelativeStrength(50, 0, 250));
        Assert.Equal(50, StatMeasures.ShoulderReach(10));        // a 10 cm gap
        Assert.Equal(65, StatMeasures.ShoulderReach(-5));        // fists overlap by 5 cm
        Assert.Equal(7.5, StatMeasures.BothSides(6, 9));
    }
}
