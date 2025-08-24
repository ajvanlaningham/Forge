

using Forge.Contstants;

namespace Forge.Services.Interfaces
{
    public interface IExerciseLibraryImporter
    {
        Task EnsureSeededAsync(string libraryFile = GameConstants.Exercises.LibraryFile, string version = GameConstants.Exercises.LibraryVersion);
    }
}
