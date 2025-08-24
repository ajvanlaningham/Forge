
using Forge.Models;

namespace Forge.Services.Interfaces
{
    public interface IExerciseLibraryService
    {
        Task InitializeAsync(); // ensure table exists (no seeding here)
        Task<IReadOnlyList<Exercise>> GetAllAsync();

        // Convenience getters
        Task<Exercise?> GetByIdAsync(int id);
        Task<Exercise?> GetByNameAsync(string name);

        // Filters
        Task<IReadOnlyList<Exercise>> FilterAsync(
            ExerciseCategory? category = null,
            BodyZone? bodyZone = null,
            Modality? modality = null,
            Equipment? requiresAllEquipmentFlags = null,  // Equipment flags must all be present
            SkillLevel? maxSkill = null,
            bool? onlyActive = true);

        // Search (name/description contains)
        Task<IReadOnlyList<Exercise>> SearchAsync(string text, bool onlyActive = true);
    }
}
