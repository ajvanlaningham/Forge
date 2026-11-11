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

        // Program prescription (set when the quest is a program day; null for generated quests).
        public int? Sets { get; init; }
        public int? RepsMin { get; init; }
        public int? RepsMax { get; init; }
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

        /// <summary>Short badge text. Program days show e.g. "Day 2/4"; otherwise the body focus.</summary>
        public string? Tag { get; init; }
        public string BadgeText => Tag ?? BodyFocus.ToString();

        /// <summary>True when this quest is a day of the active training program.</summary>
        public bool IsProgramDay { get; init; }
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

    public sealed class WeeklyConditioningProgress
    {
        public DateOnly WeekStart { get; init; }
        public DateOnly WeekEnd { get; init; }

        public int Minutes { get; init; }

        public int GoalMinutes { get; init; }

        public double Ratio => GoalMinutes <= 0 ? 0 : Math.Clamp((double)Minutes / GoalMinutes, 0.0, 1.0);

        public bool Completed => Minutes >= GoalMinutes;
    }
}