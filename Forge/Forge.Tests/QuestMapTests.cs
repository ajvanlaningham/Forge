using Forge.Training;

namespace Forge.Tests;

public class QuestMapTests
{
    private static readonly DateOnly Start = new(2026, 11, 11); // a Wednesday; weeks run from the block start
    private static readonly HashSet<DateOnly> NoRest = [];

    private static List<MapNode> Training(IReadOnlyList<MapNode> nodes, int week)
        => nodes.Where(n => n.Kind == MapNodeKind.Training && n.Week == week).ToList();

    [Fact]
    public void Shape_FourWeeksOfDays_ChestEachWeek_AnvilLast()
    {
        var nodes = QuestMap.Build(Start, 4, [], NoRest, Start);
        Assert.Equal(4 * 4 + 4 + 1, nodes.Count);
        Assert.Equal(MapNodeKind.Anvil, nodes[^1].Kind);
        Assert.Equal(MapNodeKind.Chest, nodes[4].Kind);
        Assert.Equal(1, nodes[4].Week);
    }

    [Fact]
    public void FirstDay_IsCurrent_RestAhead()
    {
        var nodes = QuestMap.Build(Start, 4, [], NoRest, Start);
        Assert.Equal(MapNodeState.Current, nodes[0].State);
        Assert.All(nodes.Skip(1), n => Assert.Equal(MapNodeState.Ahead, n.State));
    }

    [Fact]
    public void DoneToday_NothingIsCurrent()
    {
        var nodes = QuestMap.Build(Start, 4, [new DayCompletion(0, Start)], NoRest, Start);
        Assert.Equal(MapNodeState.Done, nodes[0].State);
        Assert.Equal(Start, nodes[0].DoneOn);
        Assert.DoesNotContain(nodes, n => n.State == MapNodeState.Current);
    }

    [Fact]
    public void NextDay_TheFollowingNodeIsCurrent()
    {
        var nodes = QuestMap.Build(Start, 4, [new DayCompletion(0, Start)], NoRest, Start.AddDays(1));
        Assert.Equal(MapNodeState.Current, Training(nodes, 1)[1].State);
    }

    [Fact]
    public void AMissedWeek_StaysDimmed_NotLocked_AndTheNextWeekStartsFresh()
    {
        var training = new List<DayCompletion> { new(0, Start), new(1, Start.AddDays(2)) };
        var nodes = QuestMap.Build(Start, 4, training, NoRest, Start.AddDays(8)); // week 2
        var week1 = Training(nodes, 1);
        Assert.Equal(new[] { MapNodeState.Done, MapNodeState.Done, MapNodeState.Passed, MapNodeState.Passed }, week1.Select(n => n.State));
        Assert.Equal(MapNodeState.Passed, nodes.Single(n => n.Kind == MapNodeKind.Chest && n.Week == 1).State);
        Assert.Equal(MapNodeState.Current, Training(nodes, 2)[0].State);
    }

    [Fact]
    public void APerfectWeek_OpensItsChest_OnceTheWeekIsOver()
    {
        var training = Enumerable.Range(0, 3).Select(i => new DayCompletion(i, Start.AddDays(i * 2))).ToList();
        var rest = Enumerable.Range(0, 7).Select(d => Start.AddDays(d)).Where(d => training.All(t => t.Date != d)).ToHashSet();
        Assert.Equal(MapNodeState.Ahead, QuestMap.Build(Start, 3, training, rest, Start.AddDays(6))
            .Single(n => n.Kind == MapNodeKind.Chest && n.Week == 1).State);   // still week 1
        Assert.Equal(MapNodeState.Done, QuestMap.Build(Start, 3, training, rest, Start.AddDays(7))
            .Single(n => n.Kind == MapNodeKind.Chest && n.Week == 1).State);   // week 2: opened
    }

    [Fact]
    public void AfterTheBlock_TheAnvilIsReached()
    {
        var nodes = QuestMap.Build(Start, 3, [], NoRest, Start.AddDays(28));
        Assert.Equal(MapNodeState.Done, nodes[^1].State);
        Assert.DoesNotContain(nodes, n => n.State is MapNodeState.Current or MapNodeState.Ahead && n.Kind == MapNodeKind.Training);
    }

    [Fact]
    public void Wind_StaysWithinTheTrail()
        => Assert.All(Enumerable.Range(0, 40), i => Assert.InRange(QuestMap.Wind(i), -1.0, 1.0));
}
