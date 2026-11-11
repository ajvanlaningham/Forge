using Forge.Training;

namespace Forge.Tests;

public class StrengthMathTests
{
    private static WorkSet S(double load, int reps) => new(load, reps, null);

    [Theory]
    [InlineData(100, 1, 100)]       // a single is its own max
    [InlineData(100, 10, 133.333)]  // 100 × (1 + 10/30)
    [InlineData(135, 5, 157.5)]
    [InlineData(0, 10, 0)]          // bodyweight has no load-based e1RM
    [InlineData(100, 0, 0)]
    public void Epley(double load, int reps, double expected)
        => Assert.Equal(expected, StrengthMath.Epley(load, reps), 3);

    [Fact]
    public void FirstSession_IsABaseline_NotRecords()
        => Assert.Empty(StrengthMath.FindRecords([], [S(135, 10)]));

    [Fact]
    public void HeavierLoad_IsAHeaviestLoadRecord()
    {
        var prs = StrengthMath.FindRecords([S(135, 10)], [S(140, 6)]);
        Assert.Contains(prs, p => p.Kind == PrKind.HeaviestLoad && p.LoadLb == 140);
    }

    [Fact]
    public void HeavierButWorseE1rm_IsNotAnE1rmRecord()
    {
        // 135×10 ≈ 180 e1RM; 140×6 = 168.
        var prs = StrengthMath.FindRecords([S(135, 10)], [S(140, 6)]);
        Assert.DoesNotContain(prs, p => p.Kind == PrKind.BestE1rm);
    }

    [Fact]
    public void MoreRepsAtAKnownLoad_IsARepsRecord_AndUsuallyAnE1rmRecord()
    {
        var prs = StrengthMath.FindRecords([S(135, 8)], [S(135, 9)]);
        Assert.Contains(prs, p => p.Kind == PrKind.MostRepsAtLoad && p.Reps == 9);
        Assert.Contains(prs, p => p.Kind == PrKind.BestE1rm);
        Assert.DoesNotContain(prs, p => p.Kind == PrKind.HeaviestLoad);
    }

    [Fact]
    public void RepsAtANewLoad_IsNotARepsRecord()
        => Assert.DoesNotContain(StrengthMath.FindRecords([S(135, 8)], [S(145, 3)]), p => p.Kind == PrKind.MostRepsAtLoad);

    [Fact]
    public void MatchingThePast_IsNoRecord()
        => Assert.Empty(StrengthMath.FindRecords([S(135, 10)], [S(135, 10), S(135, 9)]));

    [Fact]
    public void Bodyweight_OnlyCountsReps()
    {
        var prs = StrengthMath.FindRecords([S(0, 12)], [S(0, 15)]);
        Assert.Single(prs);
        Assert.Equal(PrKind.MostRepsAtLoad, prs[0].Kind);
    }

    [Fact]
    public void AtMostOneRecordPerKind()
    {
        var prs = StrengthMath.FindRecords([S(100, 5)], [S(105, 5), S(110, 5), S(115, 4)]);
        Assert.Equal(prs.Count, prs.Select(p => p.Kind).Distinct().Count());
        Assert.Equal(115, prs.Single(p => p.Kind == PrKind.HeaviestLoad).LoadLb);
    }
}
