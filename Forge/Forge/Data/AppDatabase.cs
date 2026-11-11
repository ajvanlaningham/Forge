using Forge.Constants;
using Forge.Services.Interfaces;
using SQLite;

namespace Forge.Data
{
    public sealed class AppDatabase : IAppDatabase
    {
        private readonly SQLiteAsyncConnection _connection;

        public AppDatabase()
        {
            SQLitePCL.Batteries_V2.Init();
            Path = System.IO.Path.Combine(FileSystem.AppDataDirectory, GameConstants.Db.FileName);
            _connection = new SQLiteAsyncConnection(Path);
        }

        public SQLiteAsyncConnection Connection => _connection;

        public string Path { get; }

        public Task CloseAsync() => _connection.CloseAsync();

        public async Task EnsureTablesAsync(params Type[] tableTypes)
        {
            foreach (var t in tableTypes)
            {
                await _connection.CreateTableAsync(t);
            }
        }
    }
}
