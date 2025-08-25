using Forge.Constants;

namespace Forge.Services.Interfaces
{
    public interface IExerciseLibraryImporter
    {
        Task EnsureSeededAsync(
            IEnumerable<string>? libraryFiles = null,
            string version = GameConstants.Exercises.LibraryVersion);
    }
}
