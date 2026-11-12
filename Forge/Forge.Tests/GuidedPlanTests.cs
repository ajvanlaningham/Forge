using Forge.Constants;
using Forge.Training;

namespace Forge.Tests;

public class GuidedPlanTests
{
    private static string Order(IReadOnlyList<GuidedStep> steps)
        => string.Join(" ", steps.Select(s => $"{(char)('a' + s.ExerciseIndex)}{(s.IsWarmup ? "w" + s.SetNumber : s.SetNumber.ToString())}"));

    [Fact]
    public void Superset_AlternatesPartners_WithWarmupsFirst()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 3, 120, Warmups: 1),
            new GuidedExercise(1, "A", 3, 90, Warmups: 1),
            new GuidedExercise(2, "B", 2, 60, Warmups: 0),
            new GuidedExercise(3, "B", 2, 60, Warmups: 0),
        }, defaultRestSeconds: 90);

        Assert.Equal("aw1 bw1 a1 b1 a2 b2 a3 b3 c1 d1 c2 d2", Order(steps));
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
            new GuidedExercise(0, null, 2, null, Warmups: 0),
            new GuidedExercise(1, null, 2, 60, Warmups: 0),
        }, defaultRestSeconds: 90);
        Assert.Equal("a1 a2 b1 b2", Order(steps));
        Assert.Equal(new[] { 90, 90, 60, 0 }, steps.Select(s => s.RestAfterSeconds));
    }

    [Fact]
    public void UnevenSuperset_FinishesTheLongerPartnerAlone()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 3, 90, 0),
            new GuidedExercise(1, "A", 2, 90, 0),
        }, 90);
        Assert.Equal("a1 b1 a2 b2 a3", Order(steps));
    }

    [Fact]
    public void Position_ResumesAtTheFirstSetNotLogged()
    {
        var steps = GuidedPlan.Build(new[]
        {
            new GuidedExercise(0, "A", 2, 90, 1),
            new GuidedExercise(1, "A", 2, 90, 0),
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

    [Fact]
    public void HardSets_AreTheLastSetOrTheTopOfTheRange()
    {
        var mid = new GuidedStep(0, false, 2, 3, 90);
        var last = new GuidedStep(0, false, 3, 3, 90);
        var warm = new GuidedStep(0, true, 1, 3, 45);
        Assert.False(GuidedPlan.IsHardSet(mid, 8, 10));
        Assert.True(GuidedPlan.IsHardSet(last, 8, 10));
        Assert.True(GuidedPlan.IsHardSet(mid, 10, 10));
        Assert.False(GuidedPlan.IsHardSet(warm, 8, 10));
        Assert.False(GuidedPlan.IsHardSet(last, null, null)); // holds aren't asked
    }

    [Fact]
    public void Warmups_RampHeavierLifts_AndShowTheirPercent()
    {
        var dumbbells = new LoadSet.Discrete(new double[] { 5, 10, 15, 20, 25, 30, 35, 40, 45, 50 });
        // 40 lb: 20 (50%) × 8, then 30 (75%) × 4.
        var heavy = GuidedPlan.Warmups(40, dumbbells);
        Assert.Equal(new[] { 20.0, 30.0 }, heavy.Select(w => w.LoadLb));
        Assert.Equal(new[] { 8, 4 }, heavy.Select(w => w.Reps));
        Assert.Equal(new[] { 50, 75 }, heavy.Select(w => w.Percent));
        // 10 lb: 5 (50%); 75% would be 7.5 → snaps to 5 or 10, neither in between: one warm-up.
        Assert.Single(GuidedPlan.Warmups(10, dumbbells));
        // Lightest owned: nothing to warm up with.
        Assert.Empty(GuidedPlan.Warmups(5, dumbbells));
        // A ramp in the plan: two warm-ups, then the work.
        var steps = GuidedPlan.Build(new[] { new GuidedExercise(0, null, 2, 90, Warmups: 2) }, 90);
        Assert.Equal("aw1 aw2 a1 a2", Order(steps));
    }

    [Fact]
    public void PerSide_RunsTheWorkTwice_EachWithItsOwnSetup()
    {
        Assert.Equal(2 * (10 * GuidedPlan.SecondsPerRep + GuidedPlan.SetupSeconds), GuidedPlan.SetSeconds(10, null, perSide: true));
        Assert.Equal(2 * (30 + GuidedPlan.SetupSeconds), GuidedPlan.SetSeconds(null, 30, perSide: true));
    }

    [Fact]
    public void PerSide_RightThenSwitchThenLeft()
    {
        var side = GuidedPlan.SetSeconds(10, null); // 35 s: one side, setup included
        Assert.Equal(SetSide.Right, GuidedPlan.SideAt(0, side));
        Assert.Equal(SetSide.Right, GuidedPlan.SideAt(side - 1, side));
        Assert.Equal(SetSide.Switch, GuidedPlan.SideAt(side, side));
        Assert.Equal(SetSide.Switch, GuidedPlan.SideAt(side + GuidedPlan.SetupSeconds - 1, side));
        Assert.Equal(SetSide.Left, GuidedPlan.SideAt(side + GuidedPlan.SetupSeconds, side));
        Assert.Equal(SetSide.Left, GuidedPlan.SideAt(2 * side, side)); // overtime (+10 s) stays on the left
    }
}
