using System;
using System.Collections.Generic;
using System.Linq;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

namespace Forge.Services.Implementations
{
    /// <summary>
    /// Read Focused -- exercise Library already imported into SQLite in the home-vm during init. 
    /// </summary>
    public sealed class ExerciseLibraryService : IExerciseLibraryService
    {
        private readonly IRepository<ExerciseRow> _repo;

        public ExerciseLibraryService(IRepository<ExerciseRow> repo)
        {
            _repo = repo;
        }

        public async Task InitializeAsync() => await _repo.EnsureTableAsync();

        public async Task<IReadOnlyList<Exercise>> GetAllAsync()
        {
            var rows = await _repo.GetAllAsync();
            return rows.Select(r => r.ToDomain()).ToList();
        }

        public async Task<Exercise?> GetByIdAsync(int id)
        {
            var row = await _repo.GetByIdAsync(id);
            return row?.ToDomain();
        }

        public async Task<Exercise?> GetByNameAsync(string name)
        {
            var row = await _repo.FirstOrDefaultAsync(r => r.Name == name);
            return row?.ToDomain();
        }

        public async Task<IReadOnlyList<Exercise>> FilterAsync(
            ExerciseCategory? category = null,
            BodyZone? bodyZone = null,
            Modality? modality = null,
            Equipment? requiresAllEquipmentFlags = null,
            SkillLevel? maxSkill = null,
            bool? onlyActive = true)
        {
            // Pull and filter in-memory for now; we can push this to SQL later if needed.
            var rows = await _repo.GetAllAsync();

            IEnumerable<ExerciseRow> q = rows;

            if (onlyActive == true) q = q.Where(r => r.IsActive);
            if (category.HasValue) q = q.Where(r => r.Category == (int)category.Value);
            if (bodyZone.HasValue) q = q.Where(r => r.BodyZone == (int)bodyZone.Value);
            if (modality.HasValue) q = q.Where(r => r.Modality == (int)modality.Value);
            if (maxSkill.HasValue) q = q.Where(r => r.Skill <= (int)maxSkill.Value);

            if (requiresAllEquipmentFlags.HasValue && requiresAllEquipmentFlags.Value != Equipment.None)
            {
                var flags = (int)requiresAllEquipmentFlags.Value;
                q = q.Where(r => (r.Equipment & flags) == flags);
            }

            return q.Select(r => r.ToDomain()).ToList();
        }

        public async Task<IReadOnlyList<Exercise>> SearchAsync(string text, bool onlyActive = true)
        {
            text = text?.Trim() ?? string.Empty;
            if (text.Length == 0) return Array.Empty<Exercise>();

            var rows = await _repo.GetAllAsync();
            var q = rows.AsEnumerable();

            if (onlyActive) q = q.Where(r => r.IsActive);

            // Case-insensitive 
            var results = q.Where(r =>
                    r.Name.Contains(text, StringComparison.OrdinalIgnoreCase) ||
                    (!string.IsNullOrEmpty(r.Description) &&
                     r.Description.Contains(text, StringComparison.OrdinalIgnoreCase)))
                .Select(r => r.ToDomain())
                .ToList();

            return results;
        }
    }
}
