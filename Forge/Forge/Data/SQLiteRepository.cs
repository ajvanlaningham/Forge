using System.Linq.Expressions;
using Forge.Services.Interfaces;
using SQLite;

namespace Forge.Data
{
    public sealed class SQLiteRepository<T> : IRepository<T> where T : new()
    {
        private readonly IAppDatabase _appDb;
        private readonly SQLiteAsyncConnection _db;
        private bool _tableEnsured;

        public SQLiteRepository(IAppDatabase appDb)
        {
            _appDb = appDb;
            _db = appDb.Connection;
        }

        public async Task EnsureTableAsync()
        {
            if (_tableEnsured) return;
            await _appDb.EnsureTablesAsync(typeof(T));
            _tableEnsured = true;
        }

        public async Task<T?> GetByIdAsync(object pk)
        {
            await EnsureTableAsync();
            // SQLite-net will infer primary key from object/model attributes
            try
            {
                return await _db.GetAsync<T>(pk);
            }
            catch (InvalidOperationException)
            {
                return default;
            }
        }

        public async Task<List<T>> GetAllAsync()
        {
            await EnsureTableAsync();
            return await _db.Table<T>().ToListAsync();
        }

        public async Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate)
        {
            await EnsureTableAsync();
            return await _db.Table<T>().Where(predicate).ToListAsync();
        }

        public async Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate)
        {
            await EnsureTableAsync();
            return await _db.Table<T>().Where(predicate).FirstOrDefaultAsync();
        }

        public async Task<int> InsertAsync(T entity)
        {
            await EnsureTableAsync();
            return await _db.InsertAsync(entity);
        }

        public async Task<int> InsertAllAsync(IEnumerable<T> entities)
        {
            await EnsureTableAsync();
            return await _db.InsertAllAsync(entities);
        }

        public async Task<int> UpdateAsync(T entity)
        {
            await EnsureTableAsync();
            return await _db.UpdateAsync(entity);
        }

        public async Task<int> InsertOrReplaceAsync(T entity)
        {
            await EnsureTableAsync();
            return await _db.InsertOrReplaceAsync(entity);
        }

        public async Task<int> DeleteAsync(T entity)
        {
            await EnsureTableAsync();
            return await _db.DeleteAsync(entity);
        }

        public async IAsyncEnumerable<T> QueryAsync(string query, params object[] args)
        {
            await EnsureTableAsync();
            var enumerable = await _db.QueryAsync<T>(query, args);
            foreach (var row in enumerable)
                yield return row;
        }
    }
}
