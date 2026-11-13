using System.Text.Json;
using Forge.Constants;
using Forge.Models;
using Forge.Training;

namespace Forge.Tests;

public class BlockScheduleTests
{
    private static readonly DateOnly Start = new(2026, 11, 9); // a Monday, though blocks can start any day

    [Theory]
    [InlineData(0, 1)]
    [InlineData(6, 1)]
    [InlineData(7, 2)]
    [InlineData(27, 4)]
    [InlineData(40, 4)] // past the end: still reported as the last week
    public void WeeksFollowTheCalendar(int daysIn, int week)
        => Assert.Equal(week, BlockSchedule.WeekOf(Start, Start.AddDays(daysIn)));

    [Fact]
    public void BlockCompletesAfterFourWeeks()
    {
        Assert.False(BlockSchedule.IsComplete(Start, Start.AddDays(27)));
        Assert.True(BlockSchedule.IsComplete(Start, Start.AddDays(28)));
    }

    [Fact]
    public void NoHistory_OffersDayOne()
        => Assert.Equal(new BlockOffer(1, 0, false, false), BlockSchedule.OfferFor(Start, 4, [], Start));

    [Fact]
    public void WithinAWeek_TheNextUndoneDayIsOffered()
    {
        var done = new[] { new DayCompletion(0, Start), new DayCompletion(1, Start.AddDays(2)) };
        Assert.Equal(2, BlockSchedule.OfferFor(Start, 4, done, Start.AddDays(3)).TrainingDay);
    }

    [Fact]
    public void DaysCanBeDoneOutOfOrder()
    {
        // Did day 3 first; day 1 is still owed this week.
        var done = new[] { new DayCompletion(2, Start) };
        Assert.Equal(0, BlockSchedule.OfferFor(Start, 4, done, Start.AddDays(1)).TrainingDay);
    }

    [Fact]
    public void CompletedToday_ShowsTodaysDayAsDone()
    {
        var done = new[] { new DayCompletion(0, Start.AddDays(1)) };
        Assert.Equal(new BlockOffer(1, 0, true, false), BlockSchedule.OfferFor(Start, 4, done, Start.AddDays(1)));
    }

    [Fact]
    public void AllTrainingDone_TheRestOfTheWeekIsActiveRest()
    {
        var done = Enumerable.Range(0, 3).Select(i => new DayCompletion(i, Start.AddDays(i))).ToList();
        Assert.Null(BlockSchedule.OfferFor(Start, 3, done, Start.AddDays(4)).TrainingDay);
    }

    [Fact]
    public void MissedDays_AreSimplyMissed_TheNextWeekStartsFresh()
    {
        var done = new[] { new DayCompletion(0, Start) }; // only day 1 in week 1
        var offer = BlockSchedule.OfferFor(Start, 4, done, Start.AddDays(7));
        Assert.Equal(2, offer.Week);
        Assert.Equal(0, offer.TrainingDay);
    }

    // --- perfect weeks ---

    private static (List<DayCompletion> Training, HashSet<DateOnly> RestDays) PerfectWeek(int week, int trainingDays)
    {
        var (start, _) = BlockSchedule.WeekRange(Start, week);
        var training = Enumerable.Range(0, trainingDays).Select(i => new DayCompletion(i, start.AddDays(i * 2))).ToList();
        var rest = Enumerable.Range(0, 7).Select(d => start.AddDays(d)).Where(d => training.All(t => t.Date != d)).ToHashSet();
        return (training, rest);
    }

    [Fact]
    public void PerfectWeek_EveryDayMoved_AllTrainingDone()
    {
        var (t, r) = PerfectWeek(1, 4);
        Assert.True(BlockSchedule.IsPerfectWeek(Start, 1, 4, t, r));
    }

    [Fact]
    public void OneDayWithoutMovement_IsNotPerfect()
    {
        var (t, r) = PerfectWeek(1, 4);
        r.Remove(r.First());
        Assert.False(BlockSchedule.IsPerfectWeek(Start, 1, 4, t, r));
    }

    [Fact]
    public void MovingEveryDay_ButSkippingATrainingDay_IsNotPerfect()
    {
        var (t, r) = PerfectWeek(1, 4);
        var dropped = t[^1];
        t.Remove(dropped);
        r.Add(dropped.Date);
        Assert.False(BlockSchedule.IsPerfectWeek(Start, 1, 4, t, r));
    }
}

public class ProgramDefinitionTests
{
    private const string Minimal = """
        { "id": "x", "name": "X", "quest": "copper-helm", "item": "Helm",
          "days": [
            { "name": "A", "main": [ { "exercise": "Goblet Squat", "sets": 3, "repsMin": 8, "repsMax": 12 } ],
              "accessories": [ [ { "exercise": "Plank", "sets": 2, "seconds": 30 } ],
                               [ { "exercise": "Dead Bug", "sets": 2, "repsMin": 8, "repsMax": 12 } ] ] },
            { "name": "B", "main": [ { "exercise": "Push-up", "sets": 3, "repsMin": 8, "repsMax": 15 } ] },
            { "name": "C", "main": [ { "exercise": "Glute Bridge", "sets": 3, "repsMin": 10, "repsMax": 15 } ] } ] }
        """;

    [Fact]
    public void MainsAreFixed_AccessoriesRotateWeekly()
    {
        var p = ProgramParser.Parse(Minimal);
        Assert.Equal(new[] { "Goblet Squat", "Plank" }, p.Resolve(0, 1).Slots.Select(s => s.Slot.Exercise));
        Assert.Equal(new[] { "Goblet Squat", "Dead Bug" }, p.Resolve(0, 2).Slots.Select(s => s.Slot.Exercise));
        Assert.Equal(new[] { "Goblet Squat", "Plank" }, p.Resolve(0, 3).Slots.Select(s => s.Slot.Exercise)); // cycles
        Assert.True(p.Resolve(0, 1).Slots[0].IsMain);
        Assert.False(p.Resolve(0, 1).Slots[1].IsMain);
    }

    [Fact]
    public void WeekFour_IsLight_SameWeightsFewerSets()
    {
        var p = ProgramParser.Parse(Minimal);
        var w4 = p.Resolve(0, 4);
        Assert.True(w4.IsLight);
        Assert.Equal(2, w4.Slots[0].Slot.Sets);   // 3 -> 2
        Assert.Equal(1, w4.Slots[1].Slot.Sets);   // 2 -> 1 (never 0)
        Assert.False(p.Resolve(0, 3).IsLight);
    }

    [Fact]
    public void WeekFour_SpreadsTheStatTests()
    {
        var p = ProgramParser.Parse(Minimal);
        Assert.Equal(StatTestKind.Strength, p.Resolve(0, 4).Test);
        Assert.Equal(StatTestKind.Mobility, p.Resolve(1, 4).Test);
        Assert.Equal(StatTestKind.Conditioning, p.Resolve(2, 4).Test);
        Assert.Null(p.Resolve(0, 2).Test);
    }

    [Fact]
    public void AFourDayBlock_HasOneDayWithoutATest()
    {
        var four = new ProgramDefinition { Days = Enumerable.Range(0, 4).Select(_ => new ProgramDay()).ToList() };
        Assert.Null(four.TestFor(2));
        Assert.Equal(StatTestKind.Conditioning, four.TestFor(3));
    }

    [Theory]
    [InlineData("""{ "id": "x", "name": "X", "quest": "q", "item": "Helm", "days": [] }""")]          // no days
    [InlineData("""{ "id": "x", "name": "X", "days": [ {"name":"A","main":[]}, {"name":"B","main":[]}, {"name":"C","main":[]} ] }""")] // no mains
    [InlineData("{ not json")]
    public void RejectsBadPrograms(string json)
        => Assert.Throws<FormatException>(() => ProgramParser.Parse(json));

    [Fact]
    public void NamesComeFromTheQuest()
    {
        var p = ProgramParser.Parse(Minimal);
        Assert.Equal("Copper Helm", p.QuestName);
        Assert.Equal("Dumbbell · 3-day", p.VersionName);
        Assert.Equal(new[] { "Kindling", "Tempering", "Quenching", "Honing" }, Enumerable.Range(1, 4).Select(ProgramDefinition.WeekName));
    }

    [Theory]
    [InlineData("\"discipline\": \"Fire\"")]
    [InlineData("\"implement\": \"Sandbag\"")]
    public void RejectsUnknownDisciplinesAndImplements(string field)
        => Assert.Throws<FormatException>(() => ProgramParser.Parse(Minimal.Replace("\"quest\": \"copper-helm\"", $"\"quest\": \"copper-helm\", {field}")));

    [Fact]
    public void AMainLift_CannotAlsoRotateAsAnAccessory()
    {
        var bad = Minimal.Replace("\"exercise\": \"Plank\", \"sets\": 2, \"seconds\": 30", "\"exercise\": \"Goblet Squat\", \"sets\": 2, \"repsMin\": 8, \"repsMax\": 12");
        Assert.Throws<FormatException>(() => ProgramParser.Parse(bad));
    }
}

/// <summary>
/// Validates the programs that actually ship against the exercise library that actually ships.
/// A typo in an exercise name would otherwise surface only on the phone, mid-workout.
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

    private static ProgramDefinition Load(string file) => ProgramParser.Parse(File.ReadAllText(Raw(file)), file);

    private static IEnumerable<ProgramSlot> AllSlots(ProgramDefinition p)
        => p.Days.SelectMany(d => d.Main.Concat(d.Accessories.SelectMany(a => a))).Concat(p.ActiveRest.Flow);

    [Theory, MemberData(nameof(ProgramFiles))]
    public void ProgramParses(string file) => Load(file);

    [Fact]
    public void Superset_PartnersMustBeNeighbours()
    {
        var p = Load("programs/copper-helm-db-3day.v3.json");
        var day = p.Days[0];
        var split = new List<ProgramSlot> { day.Main[1], day.Main[0], day.Main[2] }; // A, -, A
        var broken = new ProgramDefinition
        {
            Id = p.Id, Name = p.Name, Version = p.Version, Quest = p.Quest, Item = p.Item,
            Discipline = p.Discipline, Implement = p.Implement,
            Days = p.Days.Select((d, i) => i == 0 ? new ProgramDay { Name = d.Name, Main = split, Accessories = d.Accessories } : d).ToList()
        };
        Assert.Contains(ProgramParser.Validate(broken), e => e.Contains("superset 'A' is split up"));
        Assert.Empty(ProgramParser.Validate(p));
    }

    [Theory, MemberData(nameof(ProgramFiles))]
    public void EverySlotNamesALibraryExercise(string file)
    {
        var missing = AllSlots(Load(file)).Select(s => s.Exercise).Where(n => !Library.Value.ContainsKey(n)).Distinct().ToList();
        Assert.True(missing.Count == 0, $"{file} names exercises not in the library: {string.Join(", ", missing)}");
    }

    [Theory, MemberData(nameof(ProgramFiles))]
    public void SlotPrescriptionMatchesTheExerciseAction(string file)
    {
        foreach (var slot in AllSlots(Load(file)))
        {
            var action = Library.Value[slot.Exercise].GetProperty("action").GetString();
            Assert.True(slot.IsTimed == action is "hold" or "time",
                $"{file}: '{slot.Exercise}' is a '{action}' exercise but the slot is {(slot.IsTimed ? "timed" : "rep-based")}");
        }
    }

    [Theory, MemberData(nameof(ProgramFiles))]
    public void DeclaredEquipmentNamesAreRealEquipment(string file)
    {
        foreach (var name in Load(file).Equipment)
            Assert.True(Enum.TryParse<Equipment>(name, ignoreCase: true, out _), $"{file}: unknown equipment '{name}'");
    }

    [Fact]
    public void QuestsForgingTheSamePiece_AreToldApartOnTheBoard()
    {
        // The board titles a quest by the hero's item for its metal × discipline, so two quests
        // sharing both would show as twins. All but one of them must carry an epithet.
        var latest = GameConstants.Programs.Files.Select(Load)
            .GroupBy(p => p.Id).Select(g => g.OrderByDescending(p => p.Version).First());
        foreach (var piece in latest.GroupBy(p => (p.Metal, p.Discipline)))
        {
            var quests = piece.GroupBy(p => p.Quest).ToList();
            var bare = quests.Count(q => q.Any(p => p.Epithet is null));
            Assert.True(bare <= 1, $"{piece.Key.Metal} {piece.Key.Discipline}: {bare} quests without an epithet");
            foreach (var q in quests.Where(q => q.Any(p => p.Epithet is not null)))
                Assert.True(q.All(p => p.Epithet is not null && p.Description is not null),
                    $"{q.Key}: every version needs the epithet and its own description");
        }
    }

    [Theory, MemberData(nameof(ProgramFiles))]
    public void EveryWeekResolves(string file)
    {
        var p = Load(file);
        for (var day = 0; day < p.Days.Count; day++)
            for (var week = 1; week <= ProgramDefinition.WeeksPerBlock; week++)
                Assert.NotEmpty(p.Resolve(day, week).Slots);
    }

    [Theory, MemberData(nameof(ProgramFiles))]
    public void KettlebellVersions_AreSingleBell(string file)
    {
        // Decision 19: home kettlebell work uses one bell at a time (two unmatched bells is common).
        var p = Load(file);
        if (p.Implement != "Kettlebell") return;
        var doubles = AllSlots(p).Select(sl => sl.Exercise).Distinct()
            .Where(n => Library.Value[n].GetProperty("modality").GetString() == "kettlebell"
                        && Library.Value[n].TryGetProperty("pair", out var d) && d.GetBoolean()).ToList();
        Assert.True(doubles.Count == 0, $"{file} uses double-bell exercises: {string.Join(", ", doubles)}");
    }

    [Fact]
    public void VersionsOfAQuest_AgreeOnItsIdentity()
    {
        // Only the latest version of each program counts (older ones stay loadable for history).
        var latest = GameConstants.Programs.Files.Select(Load)
            .GroupBy(p => p.Id).Select(g => g.OrderByDescending(p => p.Version).First());
        foreach (var quest in latest.GroupBy(p => p.Quest))
        {
            var ids = quest.Select(p => (p.Metal, p.Item, p.Discipline)).Distinct().ToList();
            Assert.True(ids.Count == 1, $"quest '{quest.Key}' versions disagree on metal/item/discipline");
            var versions = quest.Select(p => p.VersionName).ToList();
            Assert.True(versions.Count == versions.Distinct().Count(), $"quest '{quest.Key}' has duplicate versions");
        }
    }

    [Fact]
    public void ProgramIdsAndVersionsAreUnique()
    {
        var keys = GameConstants.Programs.Files.Select(f => Load(f).Key).ToList();
        Assert.Equal(keys.Count, keys.Distinct().Count());
    }

    [Fact]
    public void AFamilyShares_ItsMainLifts()
    {
        foreach (var family in GameConstants.Programs.Files.Select(Load).Where(p => p.Family is not null).GroupBy(p => p.Family))
        {
            var sets = family.Select(p => p.Days.SelectMany(d => d.Main).Select(m => m.Exercise).ToHashSet()).ToList();
            Assert.True(sets.All(s => s.SetEquals(sets[0])), $"family '{family.Key}' programs don't share main lifts");
        }
    }

    [Fact]
    public void DefaultProgramExists()
        => Assert.Contains(GameConstants.Programs.Files, f => Load(f).Id == GameConstants.Programs.DefaultProgramId);

    [Fact]
    public void LibraryNamesAreUnique_AcrossFiles()
    {
        var names = GameConstants.Exercises.LibraryFiles
            .SelectMany(f => JsonDocument.Parse(File.ReadAllText(Raw(f))).RootElement.EnumerateArray())
            .Select(e => e.GetProperty("name").GetString()).ToList();
        var dupes = names.GroupBy(n => n).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        Assert.True(dupes.Count == 0, "duplicate exercise names: " + string.Join(", ", dupes));
    }
}
