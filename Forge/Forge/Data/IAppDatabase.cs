using SQLite;

namespace Forge.Services.Interfaces
{
    public interface IAppDatabase
    {
        SQLiteAsyncConnection Connection { get; }
        Task EnsureTablesAsync(params Type[] tableTypes);
    }
}
