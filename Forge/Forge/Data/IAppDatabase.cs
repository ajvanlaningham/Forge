using SQLite;

namespace Forge.Services.Interfaces
{
    public interface IAppDatabase
    {
        SQLiteAsyncConnection Connection { get; }
        Task EnsureTablesAsync(params Type[] tableTypes);

        /// <summary>Full path of the live database file.</summary>
        string Path { get; }

        /// <summary>Closes the connection so the file can be replaced (restore). The app must restart after.</summary>
        Task CloseAsync();
    }
}
