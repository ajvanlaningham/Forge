using Forge.Constants;

namespace Forge.Tests;

public class HealthMathTests
{
    private static readonly TimeSpan Off = TimeSpan.FromHours(-5);
    private static DateTimeOffset At(int day, int hour, int minute = 0) => new(2026, 11, day, hour, minute, 0, Off);
    private static SleepSession Night(DateTimeOffset start, DateTimeOffset end, params SleepStage[] stages) => new(start, end, stages);

    [Fact]
    public void SleepWindow_RunsSixPmToSixPm()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("t", Off, "t", "t");
        var (from, to) = HealthMath.SleepWindow(new DateOnly(2026, 11, 12), zone);
        Assert.Equal(At(11, 18), from);
        Assert.Equal(At(12, 18), to);
    }

    [Fact]
    public void Sleep_SubtractsAwakeStages()
    {
        var night = Night(At(11, 23), At(12, 7),
            new SleepStage(At(11, 23), At(11, 23, 20), HealthMath.StageAwake),
            new SleepStage(At(11, 23, 20), At(12, 3), 4),
            new SleepStage(At(12, 3), At(12, 3, 10), HealthMath.StageAwakeInBed),
            new SleepStage(At(12, 3, 10), At(12, 7), 6));
        Assert.Equal(8 * 60 - 30, HealthMath.SleepMinutes(new[] { night }));
    }

    [Fact]
    public void Sleep_AddsANap()
        => Assert.Equal(7 * 60 + 30, HealthMath.SleepMinutes(new[]
        {
            Night(At(11, 23), At(12, 6)),
            Night(At(12, 13), At(12, 13, 30)),
        }));

    [Fact]
    public void Sleep_OverlappingSourcesCountOnce()
        => Assert.Equal(8 * 60, HealthMath.SleepMinutes(new[]
        {
            Night(At(11, 23), At(12, 7)),       // watch
            Night(At(11, 23, 30), At(12, 6)),   // phone's guess at the same night
        }));

    [Fact]
    public void Sleep_NoneIsNull() => Assert.Null(HealthMath.SleepMinutes(Array.Empty<SleepSession>()));

    [Theory]
    [InlineData(DataSource.None, true)]
    [InlineData(DataSource.Synced, true)]
    [InlineData(DataSource.Manual, false)]
    public void Sync_NeverOverwritesHandEntries(DataSource current, bool mayWrite)
        => Assert.Equal(mayWrite, HealthMath.SyncMayWrite(current));

    [Theory]
    [InlineData("7:30", 450)]
    [InlineData(" 7h30 ", 450)]
    [InlineData("7.5", 450)]
    [InlineData("7,5", 450)]
    [InlineData("8", 480)]
    [InlineData("6:", 360)]
    [InlineData("7:75", null)]
    [InlineData("25", null)]
    [InlineData("0", null)]
    [InlineData("abc", null)]
    [InlineData("", null)]
    public void ParseSleep(string text, int? minutes) => Assert.Equal(minutes, HealthMath.ParseSleep(text));

    [Fact]
    public void FormatSleep_PadsMinutes() => Assert.Equal("7:05", HealthMath.FormatSleep(425));

    [Fact]
    public void RestingHr_Plausible()
    {
        Assert.True(HealthMath.PlausibleRestingHr(55));
        Assert.False(HealthMath.PlausibleRestingHr(0));
        Assert.False(HealthMath.PlausibleRestingHr(220));
    }
}
