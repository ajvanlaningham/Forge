using Forge.Training;

namespace Forge.Tests;

public class SessionProgressTests
{
    [Fact]
    public void FreshDay_IsFullHealth()
    {
        var day = new[] { new ExerciseProgress(3, 0, false), new ExerciseProgress(2, 0, false) };
        Assert.Equal(1.0, SessionProgress.Remaining(day));
        Assert.All(SessionProgress.Segments(day), s => Assert.Equal(1.0, s.Remaining));
    }

    [Fact]
    public void OneSection_PerSet_EmptiedInOrderFromTheRight()
    {
        // A superset: one set of the first exercise done, then one of the second.
        var day = new[] { new ExerciseProgress(3, 1, false), new ExerciseProgress(2, 1, false) };
        var segs = SessionProgress.Segments(day);
        Assert.Equal(5, segs.Count);
        Assert.Equal(0.2, segs[0].Width, 6);
        Assert.Equal(new[] { 1.0, 1, 1, 0, 0 }, segs.Select(s => s.Remaining));
        Assert.Equal(0.6, SessionProgress.Remaining(day), 6);
    }

    [Fact]
    public void Skipped_GreysOut_AtTheFarEnd_AndDoesntHoldTheFoeUp()
    {
        var day = new[] { new ExerciseProgress(2, 0, true), new ExerciseProgress(3, 3, false) };
        var segs = SessionProgress.Segments(day);
        Assert.Equal(new[] { false, false, false, true, true }, segs.Select(s => s.Skipped));
        Assert.All(segs, s => Assert.Equal(0, s.Remaining));
        Assert.True(SessionProgress.Vanquished(day));
    }

    [Fact]
    public void ExtraSets_DontGoNegative()
    {
        var day = new[] { new ExerciseProgress(2, 4, false), new ExerciseProgress(2, 0, false) };
        Assert.Equal(0.5, SessionProgress.Remaining(day), 6);
        Assert.Equal(2, SessionProgress.Segments(day).Count(s => s.Remaining > 0));
    }

    [Fact]
    public void Ring_DrainsWithTheClock()
    {
        Assert.Equal(1.0, SessionProgress.Ring(0, 60).Remaining);
        Assert.Equal(0.5, SessionProgress.Ring(30, 60).Remaining, 6);
        Assert.Equal(0, SessionProgress.Ring(90, 60).Remaining);
        Assert.Null(SessionProgress.Ring(10, 60).Side);
    }

    [Fact]
    public void Ring_PerSide_RightThenSwitchThenLeft()
    {
        var side = 30;
        var total = side * 2 + GuidedPlan.SetupSeconds;
        Assert.Equal(SetSide.Right, SessionProgress.Ring(5, total, side).Side);
        Assert.Equal(SetSide.Switch, SessionProgress.Ring(side + 1, total, side).Side);
        Assert.Equal(SetSide.Left, SessionProgress.Ring(total - 1, total, side).Side);
    }
}
