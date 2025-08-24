using System.Text.Json.Serialization;
using System.Text.Json;
using Forge.Models;
using Forge.Services.Interfaces;
using Forge.Data;
using Forge.Constants;

namespace Forge.Services.Implementations
{
    internal sealed class ExerciseLibraryImporter : IExerciseLibraryImporter
    {
        private const string PrefKey = GameConstants.Exercises.PrefixKey;
        private readonly IRepository<ExerciseRow> _repo;

        public ExerciseLibraryImporter(IRepository<ExerciseRow> repo) => _repo = repo;

        public async Task EnsureSeededAsync(
            string libraryFile = GameConstants.Exercises.LibraryFile, 
            string version = GameConstants.Exercises.LibraryVersion)
        {
            // skip if already imported this version
            var existingVersion = Preferences.Get(PrefKey, null);
            if (existingVersion == version) return;

            await _repo.EnsureTableAsync();

            using var stream = await FileSystem.OpenAppPackageFileAsync(libraryFile);
            using var reader = new StreamReader(stream);
            var json = await reader.ReadToEndAsync();

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };

            var items = JsonSerializer.Deserialize<List<ExerciseImportDto>>(json, options) ?? new();

            //fun fact, the original version of this started every single row with ID=0 so I only ever displayed the last exercise in the seed data. lol 
            foreach (var dto in items)
            {
                var exercise = dto.ToDomain();

                // 1) Try to find by unique Name
                var existing = await _repo.FirstOrDefaultAsync(r => r.Name == exercise.Name);

                if (existing == null)
                {
                    // New row => let SQLite auto-assign Id
                    var newRow = ExerciseRow.FromDomain(exercise);
                    newRow.Id = 0;                 // sentinel for sqlite-net INSERT (auto-increment)
                    await _repo.InsertAsync(newRow);
                }
                else
                {
                    // Existing row => keep the PK, update other fields
                    var updated = ExerciseRow.FromDomain(exercise);
                    updated.Id = existing.Id;      // preserve PK
                    await _repo.UpdateAsync(updated);
                }
            }

            Preferences.Set(PrefKey, version);
        }

        // DTO that matches the JSON (enums as strings, equipment as list)
        private sealed class ExerciseImportDto
        {
            public string Name { get; set; } = "";
            public string? Description { get; set; }

            public ExerciseCategory Category { get; set; } = ExerciseCategory.Strength;
            public MobilityTechnique? Technique { get; set; }

            public BodyZone BodyZone { get; set; }
            public MovementPattern Movement { get; set; }
            public Modality Modality { get; set; }

            public List<Equipment> Equipment { get; set; } = new();

            public ActionType Action { get; set; }
            public int? DefaultReps { get; set; }
            public int? DefaultSeconds { get; set; }
            public double? DefaultDistance { get; set; }
            public int? DefaultBreaths { get; set; }

            public bool Unilateral { get; set; }
            public SkillLevel Skill { get; set; } = SkillLevel.Beginner;
            public bool IsActive { get; set; } = true;

            public Exercise ToDomain()
            {
                var flags = Equipment.Aggregate(Forge.Models.Equipment.None, (acc, e) => acc | e);
                return new Exercise
                {
                    Name = Name,
                    Description = Description,
                    Category = Category,
                    Technique = Technique,
                    BodyZone = BodyZone,
                    Movement = Movement,
                    Modality = Modality,
                    Equipment = flags,
                    Action = Action,
                    DefaultReps = DefaultReps,
                    DefaultSeconds = DefaultSeconds,
                    DefaultDistance = DefaultDistance,
                    DefaultBreaths = DefaultBreaths,
                    Unilateral = Unilateral,
                    Skill = Skill,
                    IsActive = IsActive
                };
            }
        }
    }
}