using System.Text.Json;
using Forge.Constants;
using Forge.Models;
using Forge.Training;

namespace Forge.Tests;

public class ProgramRotationTests
{
    private static readonly DateOnly Mon = new(2026, 11, 9);

    [Fact]
    public void NoHistory_OffersDayOne()
        => Assert.Equal(new ProgramDayOffer(0, false), ProgramRotation.OfferFor(4, [], Mon));

    [Fact]
    public void AdvancesByCompletionOrder_NotWeekday()
    {
        // Day 1 Monday, then nothing until Thursday: Thursday is Day 2, not Day 4.
        var done = new[] { new DayCompletion(0, Mon) };
        Assert.Equal(new ProgramDayOffer(1, false), ProgramRotation.OfferFor(4, done, Mon.AddDays(3)));
    }

    [Fact]
    public void CompletedToday_OffersTheSameDayAsDone_NotTheNext()
    {
        var done = new[] { new DayCompletion(0, Mon), new DayCompletion(1, Mon.AddDays(1)) };
        Assert.Equal(new ProgramDayOffer(1, true), ProgramRotation.OfferFor(4, done, Mon.AddDays(1)));
    }

    [Fact]
    public void WrapsAfterTheLastDay()
    {
        var done = Enumerable.Range(0, 4).Select(i => new DayCompletion(i, Mon.AddDays(i))).ToList();
        Assert.Equal(new ProgramDayOffer(0, false), ProgramRotation.OfferFor(4, done, Mon.AddDays(5)));
    }

    [Fact]
    public void LookingAtAnEarlierDate_IgnoresLaterSessions()
    {
        var done = new[] { new DayCompletion(0, Mon), new DayCompletion(1, Mon.AddDays(2)) };
        Assert.Equal(new ProgramDayOffer(1, false), ProgramRotation.OfferFor(4, done, Mon.AddDays(1)));
    }

    [Fact]
    public void OrderOfInputDoesNotMatter()
    {
        var done = new[] { new DayCompletion(2, Mon.AddDays(4)), new DayCompletion(0, Mon), new DayCompletion(1, Mon.AddDays(2)) };
        Assert.Equal(new ProgramDayOffer(3, false), ProgramRotation.OfferFor(4, done, Mon.AddDays(6)));
    }

    [Fact]
    public void HistoryFromALongerProgram_WrapsIntoRange()
    {
        var done = new[] { new DayCompletion(5, Mon) };
        Assert.Equal(new ProgramDayOffer(2, false), ProgramRotation.OfferFor(4, done, Mon.AddDays(1)));
    }
}

public class ProgramParserTests
{
    private const string Minimal = """
        { "id": "x", "version": 1, "name": "X",
          "days": [ { "name": "A", "slots": [ { "exercise": "Push-up", "sets": 3, "repsMin": 8, "repsMax": 12 } ] } ] }
        """;

    [Fact]
    public void ParsesMinimalProgram()
    {
        var p = ProgramParser.Parse(Minimal);
        Assert.Equal("x@1", p.Key);
        Assert.Equal(8, p.Days[0].Slots[0].RepsMin);
        Assert.False(p.Days[0].Slots[0].IsTimed);
    }

    [Theory]
    [InlineData("""{ "exercise": "Plank", "sets": 3, "repsMin": 8, "repsMax": 12, "seconds": 30 }""")] // both
    [InlineData("""{ "exercise": "Plank", "sets": 3 }""")]                                            // neither
    [InlineData("""{ "exercise": "Push-up", "sets": 3, "repsMin": 12, "repsMax": 8 }""")]             // inverted range
    [InlineData("""{ "exercise": "Push-up", "sets": 0, "repsMin": 8, "repsMax": 12 }""")]             // no sets
    [InlineData("""{ "exercise": "", "sets": 3, "repsMin": 8, "repsMax": 12 }""")]                    // no exercise
    public void RejectsBadSlots(string slot)
    {
        var json = $$"""{ "id": "x", "name": "X", "days": [ { "name": "A", "slots": [ {{slot}} ] } ] }""";
        Assert.Throws<FormatException>(() => ProgramParser.Parse(json));
    }

    [Fact]
    public void RejectsProgramWithNoDays()
        => Assert.Throws<FormatException>(() => ProgramParser.Parse("""{ "id": "x", "name": "X", "days": [] }"""));

    [Fact]
    public void RejectsInvalidJson()
        => Assert.Throws<FormatException>(() => ProgramParser.Parse("{ not json"));
}

/// <summary>
/// Validates the program files that actually ship, against the exercise library that actually
/// ships. A typo in an exercise name would otherwise surface only on the phone, mid-workout.
/// </summary>
public class ShippedProgramContentTests
{
    private static string Raw(string relative) => Path.Combine(AppContext.BaseDirectory, "Raw", relative);

    private static readonly Lazy<Dictionary<string, JsonElement>> Library = new(() =>
    {
        var all = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var file in GameConstants.Exercises.LibraryFiles)
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Raw(file)));
            foreach (var e in doc.RootElement.EnumerateArray())
                all[e.GetProperty("name").GetString()!] = e.Clone();
        }
        return all;
    });

    public static TheoryData<string> ProgramFiles()
    {
        var data = new TheoryData<string>();
        foreach (var f in GameConstants.Programs.Files) data.Add(f);
        return data;
    }

    [Theory]
    [MemberData(nameof(ProgramFiles))]
    public void ProgramParses(string file)
        => ProgramParser.Parse(File.ReadAllText(Raw(file)), file);

    [Theory]
    [MemberData(nameof(ProgramFiles))]
    public void EverySlotNamesALibraryExercise(string file)
    {
        var p = ProgramParser.Parse(File.ReadAllText(Raw(file)), file);
        var missing = p.Days.SelectMany(d => d.Slots).Select(s => s.Exercise)
            .Where(n => !Library.Value.ContainsKey(n)).Distinct().ToList();
        Assert.True(missing.Count == 0, $"{file} names exercises not in the library: {string.Join(", ", missing)}");
    }

    [Theory]
    [MemberData(nameof(ProgramFiles))]
    public void SlotPrescriptionMatchesTheExerciseAction(string file)
    {
        var p = ProgramParser.Parse(File.ReadAllText(Raw(file)), file);
        foreach (var slot in p.Days.SelectMany(d => d.Slots))
        {
            var action = Library.Value[slot.Exercise].GetProperty("action").GetString();
            var timedAction = action is "hold" or "time";
            Assert.True(slot.IsTimed == timedAction,
                $"{file}: '{slot.Exercise}' is a '{action}' exercise but the slot is {(slot.IsTimed ? "timed" : "rep-based")}");
        }
    }

    [Theory]
    [MemberData(nameof(ProgramFiles))]
    public void DeclaredEquipmentNamesAreRealEquipment(string file)
    {
        var p = ProgramParser.Parse(File.ReadAllText(Raw(file)), file);
        foreach (var name in p.Equipment)
            Assert.True(Enum.TryParse<Equipment>(name, ignoreCase: true, out _), $"{file}: unknown equipment '{name}'");
    }

    [Fact]
    public void ProgramIdsAndVersionsAreUnique()
    {
        var keys = GameConstants.Programs.Files.Select(f => ProgramParser.Parse(File.ReadAllText(Raw(f)), f).Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void DefaultProgramExists()
        => Assert.Contains(GameConstants.Programs.Files,
            f => ProgramParser.Parse(File.ReadAllText(Raw(f)), f).Id == GameConstants.Programs.DefaultProgramId);

    [Fact]
    public void LibraryNamesAreUnique_AcrossFiles()
    {
        var names = GameConstants.Exercises.LibraryFiles
            .SelectMany(f => JsonDocument.Parse(File.ReadAllText(Raw(f))).RootElement.EnumerateArray())
            .Select(e => e.GetProperty("name").GetString())
            .ToList();
        var dupes = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, "duplicate exercise names: " + string.Join(", ", dupes));
    }
}
