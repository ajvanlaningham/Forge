using Forge.Constants;

namespace Forge.Services.Interfaces
{
    public interface IExerciseLibraryImporter
    {
        /// <summary>
        /// Seeds the exercise library if the stored version does not match
        /// <paramref name="version"/>. A matching version short-circuits the whole import, so
        /// editing a library file without bumping the version is a no-op on an existing install.
        /// </summary>
        Task EnsureSeededAsync(
            IEnumerable<string>? libraryFiles = null,
            string version = GameConstants.Exercises.LibraryVersion);

        /// <summary>
        /// Clears the stored version and re-runs the import, so edits to the JSON take effect
        /// without bumping <see cref="GameConstants.Exercises.LibraryVersion"/> or reinstalling.
        /// </summary>
        /// <remarks>
        /// Development convenience. Rows are upserted by name, so this refreshes and adds but
        /// never deletes — an exercise removed from the JSON survives in the database.
        /// </remarks>
        Task ForceReseedAsync(
            IEnumerable<string>? libraryFiles = null,
            string version = GameConstants.Exercises.LibraryVersion);
    }
}
