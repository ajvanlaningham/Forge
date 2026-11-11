using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Training
{
    /// <summary>
    /// A 4-week training block (Decisions 12–14), authored as JSON in
    /// <c>Resources/Raw/programs/*.json</c>.
    /// </summary>
    /// <remarks>
    /// A block has 3 or 4 <b>training days</b> a week; the other days are <b>active rest</b>
    /// (mobility, a walk, easy conditioning) — 7 days of movement. Each training day has
    /// <b>main lifts</b>, fixed for the whole block (they carry progression and records), and
    /// <b>accessories</b> that rotate week to week for variety. Weeks 1–3 build; week 4 keeps the
    /// same weights with fewer sets and ends with the stat test.
    ///
    /// Programs are content, not code, and versioned: editing one means shipping a new
    /// <see cref="Version"/>. Slots reference exercises by <b>name</b>, the library's stable key.
    /// </remarks>
    public sealed class ProgramDefinition
    {
        public const int WeeksPerBlock = 4;
        public const int LightWeek = 4;

        public string Id { get; init; } = "";
        public int Version { get; init; } = 1;
        public string Name { get; init; } = "";

        /// <summary>Programs sharing main lifts with different schedules (e.g. a 3-day and a 4-day).</summary>
        public string? Family { get; init; }

        public string? Description { get; init; }

        /// <summary>Equipment the program is written for. Missing pieces are handled by swaps.</summary>
        public IReadOnlyList<string> Equipment { get; init; } = Array.Empty<string>();

        /// <summary>Training days, in suggested order. Their count is the days per week (3 or 4).</summary>
        public IReadOnlyList<ProgramDay> Days { get; init; } = Array.Empty<ProgramDay>();

        /// <summary>What an active-rest day offers: a short mobility flow (a walk also counts).</summary>
        public ActiveRestDay ActiveRest { get; init; } = new();

        /// <summary>True for placeholder content shipped to prove the machinery.</summary>
        public bool Placeholder { get; init; }

        public int TrainingDaysPerWeek => Days.Count;
        public int ActiveRestDaysPerWeek => 7 - Days.Count;

        /// <summary>Stable key for a specific version, e.g. <c>block1-4day@1</c>.</summary>
        public string Key => $"{Id}@{Version}";

        /// <summary>
        /// The slots for a training day in a given week (1-based): mains plus that week's
        /// accessories. Week 4 keeps the weights and drops a set from everything (min 1).
        /// </summary>
        public ResolvedDay Resolve(int dayIndex, int week)
        {
            var day = Days[dayIndex];
            var light = week >= LightWeek;
            ProgramSlot Adjust(ProgramSlot s) => light ? s with { Sets = Math.Max(1, s.Sets - 1) } : s;

            var accessories = day.Accessories.Count == 0
                ? Array.Empty<ProgramSlot>()
                : (IReadOnlyList<ProgramSlot>)day.Accessories[(week - 1) % day.Accessories.Count];

            var slots = day.Main.Select(s => new ResolvedSlot(Adjust(s), IsMain: true))
                .Concat(accessories.Select(s => new ResolvedSlot(Adjust(s), IsMain: false)))
                .ToList();

            return new ResolvedDay(dayIndex, week, day.Name, slots, light, light ? TestFor(dayIndex) : null);
        }

        /// <summary>
        /// Week 4's stat tests are spread across its training days so they don't fatigue each other:
        /// strength on the first, mobility on the second, conditioning on the last.
        /// </summary>
        public StatTestKind? TestFor(int dayIndex)
            => dayIndex == 0 ? StatTestKind.Strength
             : dayIndex == Days.Count - 1 ? StatTestKind.Conditioning
             : dayIndex == 1 ? StatTestKind.Mobility
             : null;
    }

    public enum StatTestKind { Strength, Mobility, Conditioning }

    public sealed class ProgramDay
    {
        public string Name { get; init; } = "";
        /// <summary>Main lifts: the same every week of the block.</summary>
        public IReadOnlyList<ProgramSlot> Main { get; init; } = Array.Empty<ProgramSlot>();
        /// <summary>Accessory rotation: one list per week, cycling if shorter than the block.</summary>
        public IReadOnlyList<IReadOnlyList<ProgramSlot>> Accessories { get; init; } = Array.Empty<IReadOnlyList<ProgramSlot>>();
    }

    public sealed class ActiveRestDay
    {
        public string Name { get; init; } = "Active rest";
        /// <summary>A short mobility flow.</summary>
        public IReadOnlyList<ProgramSlot> Flow { get; init; } = Array.Empty<ProgramSlot>();
        /// <summary>Alternatives that also count, shown as text (e.g. "Walk 20+ minutes").</summary>
        public IReadOnlyList<string> Alternatives { get; init; } = new[] { "Walk or easy cardio, 20+ minutes" };
    }

    public sealed record ProgramSlot
    {
        /// <summary>Exercise name, matching the exercise library.</summary>
        public string Exercise { get; init; } = "";
        public int Sets { get; init; } = 3;
        public int? RepsMin { get; init; }
        public int? RepsMax { get; init; }
        /// <summary>Target seconds for timed/hold exercises.</summary>
        public int? Seconds { get; init; }
        public int? RestSeconds { get; init; }
        public string? Notes { get; init; }
        [JsonIgnore] public bool IsTimed => Seconds is not null;
    }

    public sealed record ResolvedSlot(ProgramSlot Slot, bool IsMain);

    /// <summary>A training day as it applies in a specific week.</summary>
    public sealed record ResolvedDay(int DayIndex, int Week, string Name, IReadOnlyList<ResolvedSlot> Slots, bool IsLight, StatTestKind? Test)
    {
        public int DayNumber => DayIndex + 1;
    }

    public static class ProgramParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true
        };

        /// <summary>Parses and validates one program file. Throws <see cref="FormatException"/> on bad content.</summary>
        public static ProgramDefinition Parse(string json, string sourceName = "program")
        {
            ProgramDefinition? program;
            try { program = JsonSerializer.Deserialize<ProgramDefinition>(json, Options); }
            catch (JsonException ex) { throw new FormatException($"{sourceName}: invalid JSON — {ex.Message}", ex); }

            if (program is null) throw new FormatException($"{sourceName}: empty program");
            var errors = Validate(program);
            if (errors.Count > 0) throw new FormatException($"{sourceName}: " + string.Join("; ", errors));
            return program;
        }

        public static IReadOnlyList<string> Validate(ProgramDefinition p)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Id)) errors.Add("missing id");
            if (string.IsNullOrWhiteSpace(p.Name)) errors.Add("missing name");
            if (p.Version < 1) errors.Add("version must be >= 1");
            if (p.Days.Count is < 3 or > 4) errors.Add("a block has 3 or 4 training days");

            for (var d = 0; d < p.Days.Count; d++)
            {
                var day = p.Days[d];
                var where = $"day {d + 1}";
                if (string.IsNullOrWhiteSpace(day.Name)) errors.Add($"{where}: missing name");
                if (day.Main.Count == 0) errors.Add($"{where}: needs at least one main lift");
                for (var s = 0; s < day.Main.Count; s++) CheckSlot(day.Main[s], $"{where} main {s + 1}", errors);
                if (day.Accessories.Count > ProgramDefinition.WeeksPerBlock)
                    errors.Add($"{where}: at most {ProgramDefinition.WeeksPerBlock} accessory weeks");
                for (var w = 0; w < day.Accessories.Count; w++)
                    for (var s = 0; s < day.Accessories[w].Count; s++)
                        CheckSlot(day.Accessories[w][s], $"{where} week {w + 1} accessory {s + 1}", errors);

                // A main lift is the block's constant — it can't also be someone's rotating accessory.
                var mains = day.Main.Select(m => m.Exercise).ToHashSet(StringComparer.Ordinal);
                foreach (var dup in day.Accessories.SelectMany(a => a).Where(a => mains.Contains(a.Exercise)).Select(a => a.Exercise).Distinct())
                    errors.Add($"{where}: '{dup}' is a main lift and an accessory");
            }

            for (var s = 0; s < p.ActiveRest.Flow.Count; s++)
                CheckSlot(p.ActiveRest.Flow[s], $"active rest {s + 1}", errors);
            return errors;
        }

        private static void CheckSlot(ProgramSlot slot, string at, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(slot.Exercise)) errors.Add($"{at}: missing exercise");
            if (slot.Sets < 1) errors.Add($"{at}: sets must be >= 1");
            var hasReps = slot.RepsMin is not null || slot.RepsMax is not null;
            if (hasReps == slot.IsTimed) errors.Add($"{at}: give either a rep range or seconds, not both or neither");
            if (hasReps && (slot.RepsMin is null || slot.RepsMax is null || slot.RepsMin < 1 || slot.RepsMax < slot.RepsMin))
                errors.Add($"{at}: rep range must be 1 <= repsMin <= repsMax");
            if (slot.IsTimed && slot.Seconds < 1) errors.Add($"{at}: seconds must be >= 1");
            if (slot.RestSeconds is < 0) errors.Add($"{at}: rest must be >= 0");
        }
    }
}
