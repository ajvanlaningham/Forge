namespace Forge.Training
{
    public enum MapNodeKind
    {
        /// <summary>A training day of the quest.</summary>
        Training,
        /// <summary>The end of a week: opens if the week was perfect (every day moved, all training done).</summary>
        Chest,
        /// <summary>The end of the quest: the forge.</summary>
        Anvil
    }

    public enum MapNodeState
    {
        /// <summary>Done (a training day), opened (a chest), or reached (the anvil).</summary>
        Done,
        /// <summary>The one node to do next: today's training day.</summary>
        Current,
        /// <summary>Still to come.</summary>
        Ahead,
        /// <summary>
        /// Its week is over and it wasn't done (or the chest stayed shut). Shown dimmed, never red or
        /// locked: missed days are simply missed (Decision 12).
        /// </summary>
        Passed
    }

    /// <summary>One stop on the quest map.</summary>
    /// <param name="DayIndex">The training day (0-based) for <see cref="MapNodeKind.Training"/>, else null.</param>
    /// <param name="DoneOn">When a training day was done.</param>
    /// <param name="Via">A training day filled by a side quest ("HEMA"): its stone wears that activity's icon.</param>
    public sealed record MapNode(MapNodeKind Kind, int Week, int? DayIndex, MapNodeState State, DateOnly? DoneOn = null, string? Via = null);

    /// <summary>
    /// The quest as a path (plan phase 4, 2026-11-13): for each of the four weeks its training days
    /// in order, then that week's chest; the anvil at the end. Pure, so the map's rules are tested.
    /// </summary>
    public static class QuestMap
    {
        public static IReadOnlyList<MapNode> Build(DateOnly blockStart, int trainingDays,
            IReadOnlyCollection<DayCompletion> training, IReadOnlySet<DateOnly> activeRest, DateOnly today)
        {
            var offer = BlockSchedule.OfferFor(blockStart, trainingDays, training, today);
            var complete = offer.BlockComplete;
            var currentWeek = complete ? ProgramDefinition.WeeksPerBlock + 1 : offer.Week;
            var nodes = new List<MapNode>();

            for (var week = 1; week <= ProgramDefinition.WeeksPerBlock; week++)
            {
                var (start, end) = BlockSchedule.WeekRange(blockStart, week);
                var doneThisWeek = training.Where(c => c.Date >= start && c.Date <= end && c.Date <= today)
                    .GroupBy(c => c.DayIndex).ToDictionary(g => g.Key, g => g.OrderBy(c => c.Date).First());

                for (var day = 0; day < trainingDays; day++)
                {
                    MapNodeState state;
                    if (doneThisWeek.TryGetValue(day, out var done)) state = MapNodeState.Done;
                    else if (week < currentWeek) state = MapNodeState.Passed;
                    else if (week == currentWeek && offer.TrainingDay == day && !offer.TrainingDoneToday) state = MapNodeState.Current;
                    else state = MapNodeState.Ahead;
                    nodes.Add(state == MapNodeState.Done
                        ? new MapNode(MapNodeKind.Training, week, day, state, done.Date, done.Via)
                        : new MapNode(MapNodeKind.Training, week, day, state));
                }

                // A chest opens once its week is over and was perfect; while the week runs it waits.
                var chest = week >= currentWeek ? MapNodeState.Ahead
                    : BlockSchedule.IsPerfectWeek(blockStart, week, trainingDays, training, activeRest) ? MapNodeState.Done
                    : MapNodeState.Passed;
                nodes.Add(new MapNode(MapNodeKind.Chest, week, null, chest));
            }

            nodes.Add(new MapNode(MapNodeKind.Anvil, ProgramDefinition.WeeksPerBlock, null,
                complete ? MapNodeState.Done : MapNodeState.Ahead));
            return nodes;
        }

        /// <summary>
        /// Horizontal offset of the n-th node along the winding path, from -1 (left) to 1 (right).
        /// A gentle S-curve like a trail, centred on the first node.
        /// </summary>
        public static double Wind(int index) => Math.Sin(index * Math.PI / 4);
    }
}
