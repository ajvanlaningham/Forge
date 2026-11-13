using Forge.Constants;
using Forge.Training;

namespace Forge.Tests;

/// <summary>Decision 28: timed moves with suggested reps, progressed by how the workout felt.</summary>
public class PacedProgressionTests
{
    private static readonly Prescription Rx = new(3, 8, 12, 60);
    private static readonly LoadSet Bells = new LoadSet.Discrete([20, 25, 35]);
    private static Suggestion? Next(double load, int reps, Feel? feel, LoadSet? loads = null)
        => Progression.Paced(Rx, new PacedLast(load, reps, feel), loads ?? Bells, WeightUnit.Pounds);

    [Fact]
    public void FirstTime_HasNoSuggestion_TheBriefingAsks()
        => Assert.Null(Progression.Paced(Rx, null, Bells, WeightUnit.Pounds));

    [Fact]
    public void Easy_AddsTwoReps()
    {
        var s = Next(25, 8, Feel.Easy)!;
        Assert.Equal(25, s.LoadLb);
        Assert.Equal(10, s.Reps);
        Assert.Equal(60, s.Seconds);
    }

    [Fact]
    public void Easy_PastTheTopOfTheRange_GoesUpAWeight_AndBackToTheBottom()
    {
        var s = Next(25, 12, Feel.Easy)!;
        Assert.Equal(SuggestionKind.IncreaseLoad, s.Kind);
        Assert.Equal(35, s.LoadLb);
        Assert.Equal(8, s.Reps);
    }

    [Fact]
    public void Easy_AtTheHeaviestWeight_KeepsAddingReps_UpToWhatTheClockFits()
    {
        Assert.Equal(14, Next(35, 12, Feel.Easy)!.Reps);
        Assert.Equal(30, Next(35, 30, Feel.Easy)!.Reps); // 60 s holds about 30 steady reps
        Assert.Equal(30, Progression.MaxPacedReps(60));
    }

    [Fact]
    public void Easy_OnABodyweightMove_AddsReps()
        => Assert.Equal(12, Next(0, 10, Feel.Easy, new LoadSet.Discrete([0]))!.Reps);

    [Fact]
    public void Hard_TakesTwoRepsOff_ThenAWeight()
    {
        Assert.Equal(9, Next(25, 11, Feel.Hard)!.Reps);
        var down = Next(25, 9, Feel.Hard)!;
        Assert.Equal(20, down.LoadLb);
        Assert.Equal(8, down.Reps);
    }

    [Theory]
    [InlineData(Feel.Right)]
    [InlineData(null)]
    public void Right_OrNoAnswer_HoldsEverything(Feel? feel)
    {
        var s = Next(25, 10, feel)!;
        Assert.Equal(25, s.LoadLb);
        Assert.Equal(10, s.Reps);
    }
}

public class TimedCircuitTests
{
    [Fact]
    public void ATimedCircuit_RestsAfterEveryMove_NotAShortTransition()
    {
        var steps = GuidedPlan.Build(
        [
            new GuidedExercise(0, "A", 2, 45, 0, Timed: true),
            new GuidedExercise(1, "A", 2, 60, 0, Timed: true),
        ], 90);
        Assert.Equal(new[] { 45, 60, 45, 0 }, steps.Select(s => s.RestAfterSeconds));
    }

    [Fact]
    public void ACountedSuperset_StillUsesTheTransition()
    {
        var steps = GuidedPlan.Build([new GuidedExercise(0, "A", 1, 90, 0), new GuidedExercise(1, "A", 2, 90, 0)], 60);
        Assert.Equal(GuidedPlan.TransitionSeconds, steps[0].RestAfterSeconds);
    }

    [Fact]
    public void RoundsCanRiseByWeek()
    {
        var p = new ProgramDefinition
        {
            Days = [new ProgramDay { Name = "A", Main = [new ProgramSlot { Exercise = "X", Seconds = 60, RepsMin = 8, RepsMax = 12, SetsByWeek = [3, 3, 4] }] }]
        };
        Assert.Equal(new[] { 3, 3, 4, 4 }, Enumerable.Range(1, 4).Select(w => p.Resolve(0, w).Slots[0].Slot.Sets));
    }

    [Fact]
    public void APacedSlot_IsValid_ButASlotNeedsRepsOrSeconds()
    {
        var paced = new ProgramSlot { Exercise = "X", Seconds = 60, RepsMin = 8, RepsMax = 12 };
        Assert.True(paced.IsPaced);
        var json = """{ "id": "x", "name": "X", "subtitle": "S", "quest": "q", "days": [ {"name":"A","main":[{"exercise":"X"}]}, {"name":"B","main":[{"exercise":"X","seconds":30}]}, {"name":"C","main":[{"exercise":"X","seconds":30,"repsMin":5,"repsMax":8}]} ] }""";
        Assert.Contains("give a rep range, seconds, or both", Assert.Throws<FormatException>(() => ProgramParser.Parse(json)).Message);
    }

    [Fact]
    public void DayTiming_IsTheTimeline()
    {
        // Two timed moves, 60 s on / 45 s off, twice; one is per side (60 s each side).
        var day = new ResolvedDay(0, 1, "A",
        [
            new ResolvedSlot(new ProgramSlot { Exercise = "Both", Seconds = 60, RepsMin = 8, RepsMax = 12, Sets = 2, RestSeconds = 45, Superset = "A" }, true),
            new ResolvedSlot(new ProgramSlot { Exercise = "Side", Seconds = 30, RepsMin = 6, RepsMax = 8, Sets = 2, RestSeconds = 45, Superset = "A" }, true),
        ], false, null);
        var secs = DayTiming.Seconds(day, n => new MoveTraits(n == "Side", true));
        var both = 60 + GuidedPlan.SetupSeconds;
        var side = 2 * (30 + GuidedPlan.SetupSeconds);
        Assert.Equal(2 * both + 2 * side + 3 * 45, secs); // no rest after the last set
    }
}

/// <summary>Side quests (user, 2026-11-13): your own training counts.</summary>
public class SideQuestTests
{
    [Theory]
    [InlineData(180, Effort.Hard, SideQuestCounts.Training)]   // three hours of HEMA
    [InlineData(45, Effort.Moderate, SideQuestCounts.Training)] // a gym class
    [InlineData(60, Effort.Easy, SideQuestCounts.Rest)]         // a yoga practice
    [InlineData(20, Effort.Hard, SideQuestCounts.Rest)]         // too short to replace a workout
    public void Suggests_TrainingOrRest(int minutes, Effort effort, SideQuestCounts expected)
        => Assert.Equal(expected, SideQuests.Suggest(minutes, effort));

    [Fact]
    public void StandsIn_AtMostTwiceAWeek_AndOnlyWhenADayIsOnOffer()
    {
        Assert.True(SideQuests.CanStandIn(true, 1));
        Assert.False(SideQuests.CanStandIn(true, SideQuests.MaxTrainingPerWeek));
        Assert.False(SideQuests.CanStandIn(false, 0));
    }

    [Theory]
    [InlineData("HEMA", "sword")]
    [InlineData("Longsword sparring", "sword")]
    [InlineData("Yoga", "yoga")]
    [InlineData("Gym class", "class")]
    [InlineData("Boxing class", "fist")]
    [InlineData("Ultimate frisbee", "sport")]
    [InlineData("Knitting", "star")]
    public void Icons(string activity, string icon) => Assert.Equal(icon, SideQuests.Icon(activity));

    [Theory]
    [InlineData(45, "45 min")]
    [InlineData(60, "1 h")]
    [InlineData(90, "1 h 30")]
    [InlineData(180, "3 h")]
    public void Durations(int minutes, string text) => Assert.Equal(text, SideQuests.Duration(minutes));

    [Fact]
    public void ADayFilledByASideQuest_CarriesItOntoTheMap()
    {
        var start = new DateOnly(2026, 11, 9);
        var nodes = QuestMap.Build(start, 4, [new DayCompletion(0, start, "HEMA")], new HashSet<DateOnly>(), start);
        Assert.Equal("HEMA", nodes[0].Via);
        Assert.Equal(MapNodeState.Done, nodes[0].State);
        Assert.Null(nodes[1].Via);
    }

    [Fact]
    public void ABigHardDay_SuggestsRestTomorrow()
    {
        Assert.True(SideQuests.BigDay(180, Effort.Hard));
        Assert.False(SideQuests.BigDay(180, Effort.Moderate));
        Assert.False(SideQuests.BigDay(45, Effort.Hard));
    }
}
