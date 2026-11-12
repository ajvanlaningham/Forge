using Forge.Constants;

namespace Forge.Tests;

public class StravaTests
{
    private static StravaExercise Ex(string name, params StravaSet[] sets) => new(name, sets);
    private static StravaSet S(string load, int reps) => new(load, reps, null);
    private static StravaSet T(int seconds) => new("", null, seconds);

    [Fact]
    public void IdenticalSets_Collapse()
        => Assert.Equal("Floor Press: 3 × 8 @ 35 lb",
            StravaActivity.ExerciseLine(Ex("Floor Press", S("35 lb", 8), S("35 lb", 8), S("35 lb", 8))));

    [Fact]
    public void OneLoad_ListsReps()
        => Assert.Equal("Row: 16 kg × 10, 10, 8",
            StravaActivity.ExerciseLine(Ex("Row", S("16 kg", 10), S("16 kg", 10), S("16 kg", 8))));

    [Fact]
    public void ChangingLoad_PairsEachSet()
        => Assert.Equal("Press: 35 lb × 10, 40 lb × 8",
            StravaActivity.ExerciseLine(Ex("Press", S("35 lb", 10), S("40 lb", 8))));

    [Fact]
    public void Bodyweight_HasNoLoad()
    {
        Assert.Equal("Push-up: 15, 12", StravaActivity.ExerciseLine(Ex("Push-up", S("", 15), S("", 12))));
        Assert.Equal("Push-up: 2 × 12", StravaActivity.ExerciseLine(Ex("Push-up", S("", 12), S("", 12))));
    }

    [Fact]
    public void Holds_UseSeconds()
    {
        Assert.Equal("Plank: 2 × 45s", StravaActivity.ExerciseLine(Ex("Plank", T(45), T(45))));
        Assert.Equal("Dead Hang: 20s", StravaActivity.ExerciseLine(Ex("Dead Hang", T(20))));
    }

    [Fact]
    public void Description_SkipsEmptyExercises_AndEndsWithFooter()
    {
        var text = StravaActivity.Description(new[]
        {
            Ex("Floor Press", S("35 lb", 8), S("35 lb", 8)),
            Ex("Skipped"),
            Ex("Curl", S("20 lb", 12)),
        }, "Copper Patched Cloak · week 1");

        Assert.Equal(
            "Floor Press: 2 × 8 @ 35 lb\nCurl: 12 @ 20 lb\n\nCopper Patched Cloak · week 1\nLogged with Forge",
            text.Replace("\r\n", "\n"));
    }

    [Fact]
    public void Name_FallsBack()
    {
        Assert.Equal("Kindling · Upper A", StravaActivity.Name(" Kindling · Upper A "));
        Assert.Equal("Strength session", StravaActivity.Name(null));
    }

    [Fact]
    public void Elapsed_IsAtLeastAMinute()
    {
        var start = new DateTime(2026, 11, 11, 18, 0, 0, DateTimeKind.Utc);
        Assert.Equal(60, StravaActivity.ElapsedSeconds(start, start.AddSeconds(5)));
        Assert.Equal(2700, StravaActivity.ElapsedSeconds(start, start.AddMinutes(45)));
    }

    [Fact]
    public void StartDateLocal_IsWallClockWithoutOffset()
    {
        var zone = TimeZoneInfo.CreateCustomTimeZone("t", TimeSpan.FromHours(-6), "t", "t");
        Assert.Equal("2026-11-11T12:30:00",
            StravaActivity.StartDateLocal(new DateTime(2026, 11, 11, 18, 30, 0, DateTimeKind.Utc), zone));
    }

    [Fact]
    public void Pending_IsNewUnpostedNonEmpty_OldestFirst()
    {
        var connected = new DateTime(2026, 11, 12, 0, 0, 0, DateTimeKind.Utc);
        var pending = StravaActivity.Pending(new[]
        {
            new StravaCandidate(1, connected.AddDays(-1), 10),  // before connecting
            new StravaCandidate(2, connected.AddHours(5), 10),
            new StravaCandidate(3, connected.AddHours(1), 10),
            new StravaCandidate(4, connected.AddHours(2), 0),   // nothing logged
            new StravaCandidate(5, connected.AddHours(3), 8),   // already posted
        }, posted: new[] { 5 }, connected);

        Assert.Equal(new[] { 3, 2 }, pending);
    }

    [Fact]
    public void Scope_NeedsActivityWrite()
    {
        Assert.True(StravaAuth.CanWrite("read,activity:write"));
        Assert.False(StravaAuth.CanWrite("read"));
        Assert.False(StravaAuth.CanWrite(null));
    }

    [Fact]
    public void Refresh_FiveMinutesEarly()
    {
        var now = DateTimeOffset.FromUnixTimeSeconds(1_000_000);
        Assert.False(StravaAuth.NeedsRefresh(1_000_000 + 301, now));
        Assert.True(StravaAuth.NeedsRefresh(1_000_000 + 300, now));
    }

    [Fact]
    public void AuthorizeUrl_AsksForWriteAndUsesTheCallback()
    {
        var url = StravaAuth.AuthorizeUrl("12345");
        Assert.Contains("client_id=12345", url);
        Assert.Contains("scope=activity%3Awrite", url);
        Assert.Contains("redirect_uri=forgestrava%3A%2F%2Flocalhost%2Fstrava", url);
    }

    [Fact]
    public void Secret_Is40Hex_TrimmedAndLowered()
    {
        var hex = new string('a', 20) + "0123456789ABCDEF0123";
        Assert.Equal(hex.ToLowerInvariant(), StravaAuth.CleanSecret("  " + hex + "\n"));
        Assert.Null(StravaAuth.CleanSecret(hex[..39]));
        Assert.Null(StravaAuth.CleanSecret(new string('g', 40)));
        Assert.Null(StravaAuth.CleanSecret(null));
    }
}
