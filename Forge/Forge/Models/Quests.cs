namespace Forge.Models
{
    public enum QuestKind
    {
        Strength,
        Mobility,
        Conditioning
    }

    public sealed class QuestExercise
    {
        public int ExerciseId { get; init; }
        public string Name { get; init; } = string.Empty;

        // What to do with it:
        public ActionType Action { get; init; }   // Reps / Time / Distance / Hold / Calories

        // Defaults (only one will typically be used depending on Action)
        public int? Reps { get; init; }
        public int? Seconds { get; init; }
        public double? Distance { get; init; }
        public int? Breaths { get; init; }
    }

    /// <summary>
    /// A quest is a themed set of exercises of a single kind (STR/DEX/CON) with a body focus.
    /// </summary>
    public sealed class Quest
    {
        public QuestKind Kind { get; init; }
        public BodyZone BodyFocus { get; init; }           // Lower / Upper / Core / FullBody
        public IReadOnlyList<QuestExercise> Exercises { get; init; } = Array.Empty<QuestExercise>();

        // Optional presentation
        public string? Title { get; init; }
        public string? Notes { get; init; }
    }

    public sealed class DailyQuests
    {
        public DateOnly Date { get; init; }

        /// <summary>
        /// The day’s overall theme. Individual quests should match this.
        /// </summary>
        public BodyZone BodyFocus { get; init; }

        public Quest Strength { get; init; } = new() { Kind = QuestKind.Strength, BodyFocus = BodyZone.FullBody };
        public Quest Mobility { get; init; } = new() { Kind = QuestKind.Mobility, BodyFocus = BodyZone.FullBody };
        public Quest Conditioning { get; init; } = new() { Kind = QuestKind.Conditioning, BodyFocus = BodyZone.FullBody };
    }
}
