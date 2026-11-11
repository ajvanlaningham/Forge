using Forge.Constants;
using Forge.Training;

namespace Forge.Tests;

public class GuidedPlanTests
{
    private static string Order(IReadOnlyList<GuidedStep> steps)
        => string.Join(" ", steps.Select(s => $"{(char)('a' + s.ExerciseIndex)}{(s.IsWarmup ? "w" : s.SetNumber.ToString())}"));

    [Fact]
    public void Superset_AlternatesPartners_WithWarmupsFirst()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 3, 120, Warmup: true),
            new GuidedExercise(1, "A", 3, 90, Warmup: true),
            new GuidedExercise(2, "B", 2, 60, Warmup: false),
            new GuidedExercise(3, "B", 2, 60, Warmup: false),
        }, defaultRestSeconds: 90);

        Assert.Equal("aw bw a1 b1 a2 b2 a3 b3 c1 d1 c2 d2", Order(steps));
        Assert.Equal(GuidedPlan.WarmupRestSeconds, steps[0].RestAfterSeconds);
        Assert.Equal(GuidedPlan.TransitionSeconds, steps[2].RestAfterSeconds); // a1 → b1
        Assert.Equal(120, steps[3].RestAfterSeconds);                          // round rest: the longer of the pair
        Assert.Equal(0, steps[^1].RestAfterSeconds);
    }

    [Fact]
    public void LoneExercises_AreStraightSets_WithTheirOwnRest()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, null, 2, null, Warmup: false),
            new GuidedExercise(1, null, 2, 60, Warmup: false),
        }, defaultRestSeconds: 90);
        Assert.Equal("a1 a2 b1 b2", Order(steps));
        Assert.Equal(new[] { 90, 90, 60, 0 }, steps.Select(s => s.RestAfterSeconds));
    }

    [Fact]
    public void UnevenSuperset_FinishesTheLongerPartnerAlone()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 3, 90, false),
            new GuidedExercise(1, "A", 2, 90, false),
        }, 90);
        Assert.Equal("a1 b1 a2 b2 a3", Order(steps));
    }

    [Fact]
    public void Position_ResumesAtTheFirstSetNotLogged()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 2, 90, true),
            new GuidedExercise(1, "A", 2, 90, false),
        }, 90);
        // aw a1 b1 a2 b2: a has its warm-up and one working set, b has one.
        Assert.Equal(3, GuidedPlan.Position(steps, i => i == 0 ? (1, 1) : (0, 1)));
        Assert.Equal(0, GuidedPlan.Position(steps, _ => (0, 0)));
        Assert.Equal(steps.Count, GuidedPlan.Position(steps, i => i == 0 ? (1, 2) : (0, 2)));
    }

    [Fact]
    public void SetSeconds_RepsAtAPacePlusSetup_OrTheHold()
    {
        Assert.Equal(8 * GuidedPlan.SecondsPerRep + GuidedPlan.SetupSeconds, GuidedPlan.SetSeconds(8, null));
        Assert.Equal(45 + GuidedPlan.SetupSeconds, GuidedPlan.SetSeconds(null, 45));
    }

    [Fact]
    public void WarmupLoad_IsAboutHalf_AndAlwaysLighter()
    {
        var dumbbells = new LoadSet.Discrete(new double[] { 5, 10, 15, 20, 25, 30, 35, 40 });
        Assert.Equal(15, GuidedPlan.WarmupLoad(30, dumbbells));
        Assert.Equal(5, GuidedPlan.WarmupLoad(10, dumbbells));
        Assert.Equal(5, GuidedPlan.WarmupLoad(5, dumbbells)); // nothing lighter: same weight
        var bells = new LoadSet.Discrete(new[] { 16.0, 20.0 }.Select(k => WeightMath.ToStoredPounds(k, WeightUnit.Kilograms)));
        Assert.Equal(WeightMath.ToStoredPounds(16, WeightUnit.Kilograms), GuidedPlan.WarmupLoad(WeightMath.ToStoredPounds(20, WeightUnit.Kilograms), bells), 6);
        Assert.Equal(0, GuidedPlan.WarmupLoad(0, dumbbells));
    }
}
