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
    public void EverySet_KnocksOffItsShare()
    {
        var day = new[] { new ExerciseProgress(3, 1, false), new ExerciseProgress(2, 0, false) };
        var segs = SessionProgress.Segments(day);
        Assert.Equal(2.0 / 3, segs[0].Remaining, 6);
        Assert.Equal(0.5, segs[1].Width, 6);
        Assert.Equal((2.0 / 3 + 1) / 2, SessionProgress.Remaining(day), 6);
    }

    [Fact]
    public void Skipped_GreysOut_AndDoesntHoldTheFoeUp()
    {
        var day = new[] { new ExerciseProgress(3, 3, false), new ExerciseProgress(2, 0, true) };
        var segs = SessionProgress.Segments(day);
        Assert.True(segs[1].Skipped);
        Assert.Equal(0, segs[1].Remaining);
        Assert.True(SessionProgress.Vanquished(day));
    }

    [Fact]
    public void ExtraSets_DontGoNegative()
        => Assert.Equal(0, SessionProgress.Segments([new ExerciseProgress(2, 4, false)])[0].Remaining);

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
