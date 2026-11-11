using System.Text.Json;
using System.Text.Json.Serialization;

namespace Forge.Training
{
    /// <summary>
    /// A training program as authored in <c>Resources/Raw/programs/*.json</c>: an ordered list of
    /// days, each an ordered list of exercise slots.
    /// </summary>
    /// <remarks>
    /// Programs are content, not code, and are versioned: <see cref="Id"/> is stable across
    /// versions, and editing a program's prescription means shipping a new <see cref="Version"/>
    /// rather than changing an old one in place. Logged history records the id + version + day it
    /// was performed against, so it keeps meaning what was actually prescribed at the time.
    ///
    /// Slots reference exercises by <b>name</b> — the unique key of the exercise library — never by
    /// database id, which is autoincrement and not stable across reseeds.
    /// </remarks>
    public sealed class ProgramDefinition
    {
        public string Id { get; init; } = "";
        public int Version { get; init; } = 1;
        public string Name { get; init; } = "";
        public string? Description { get; init; }

        /// <summary>Equipment the program is written for. Missing pieces are handled by swaps.</summary>
        public IReadOnlyList<string> Equipment { get; init; } = Array.Empty<string>();

        public IReadOnlyList<ProgramDay> Days { get; init; } = Array.Empty<ProgramDay>();

        /// <summary>True for placeholder content shipped to prove the machinery.</summary>
        public bool Placeholder { get; init; }

        /// <summary>Stable key for a specific version, e.g. <c>ppl-full@1</c>.</summary>
        public string Key => $"{Id}@{Version}";
    }

    public sealed class ProgramDay
    {
        public string Name { get; init; } = "";
        public IReadOnlyList<ProgramSlot> Slots { get; init; } = Array.Empty<ProgramSlot>();
    }

    public sealed class ProgramSlot
    {
        /// <summary>Exercise name, matching the exercise library.</summary>
        public string Exercise { get; init; } = "";

        /// <summary>Working sets (warm-ups are logged ad hoc and never prescribed).</summary>
        public int Sets { get; init; } = 3;

        /// <summary>Rep range for rep-based exercises. Null for timed/hold exercises.</summary>
        public int? RepsMin { get; init; }
        public int? RepsMax { get; init; }

        /// <summary>Target seconds for timed/hold exercises. Null for rep-based ones.</summary>
        public int? Seconds { get; init; }

        /// <summary>Rest after each set. Null = the user's default rest time.</summary>
        public int? RestSeconds { get; init; }

        public string? Notes { get; init; }

        public bool IsTimed => Seconds is not null;
    }

    public static class ProgramParser
    {
        private static readonly JsonSerializerOptions Options = new()
        {
            PropertyNameCaseInsensitive = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            NumberHandling = JsonNumberHandling.Strict
        };

        /// <summary>Parses and validates one program file. Throws <see cref="FormatException"/> on bad content.</summary>
        public static ProgramDefinition Parse(string json, string sourceName = "program")
        {
            ProgramDefinition? program;
            try
            {
                program = JsonSerializer.Deserialize<ProgramDefinition>(json, Options);
            }
            catch (JsonException ex)
            {
                throw new FormatException($"{sourceName}: invalid JSON — {ex.Message}", ex);
            }

            if (program is null)
                throw new FormatException($"{sourceName}: empty program");

            var errors = Validate(program);
            if (errors.Count > 0)
                throw new FormatException($"{sourceName}: " + string.Join("; ", errors));

            return program;
        }

        /// <summary>Structural validation. Exercise-name checks against the library live in tests.</summary>
        public static IReadOnlyList<string> Validate(ProgramDefinition p)
        {
            var errors = new List<string>();
            if (string.IsNullOrWhiteSpace(p.Id)) errors.Add("missing id");
            if (string.IsNullOrWhiteSpace(p.Name)) errors.Add("missing name");
            if (p.Version < 1) errors.Add("version must be >= 1");
            if (p.Days.Count == 0) errors.Add("no days");

            for (var d = 0; d < p.Days.Count; d++)
            {
                var day = p.Days[d];
                var where = $"day {d + 1}";
                if (string.IsNullOrWhiteSpace(day.Name)) errors.Add($"{where}: missing name");
                if (day.Slots.Count == 0) errors.Add($"{where}: no slots");

                for (var s = 0; s < day.Slots.Count; s++)
                {
                    var slot = day.Slots[s];
                    var at = $"{where} slot {s + 1}";
                    if (string.IsNullOrWhiteSpace(slot.Exercise)) errors.Add($"{at}: missing exercise");
                    if (slot.Sets < 1) errors.Add($"{at}: sets must be >= 1");

                    var hasReps = slot.RepsMin is not null || slot.RepsMax is not null;
                    if (hasReps == slot.IsTimed)
                        errors.Add($"{at}: give either a rep range or seconds, not both or neither");
                    if (hasReps && (slot.RepsMin is null || slot.RepsMax is null || slot.RepsMin < 1 || slot.RepsMax < slot.RepsMin))
                        errors.Add($"{at}: rep range must be 1 <= repsMin <= repsMax");
                    if (slot.IsTimed && slot.Seconds < 1)
                        errors.Add($"{at}: seconds must be >= 1");
                    if (slot.RestSeconds is < 0)
                        errors.Add($"{at}: rest must be >= 0");
                }
            }
            return errors;
        }
    }
}
