
using System.Linq.Expressions;

namespace Forge.Data
{
    public interface IRepository<T> where T : new()
    {
        Task EnsureTableAsync();

        //Reads
        Task<List<T>> GetAllAsync();
        Task<T?> GetByIdAsync(object pk);
        Task<List<T>> WhereAsync(Expression<Func<T, bool>> predicate);
        Task<T?> FirstOrDefaultAsync(Expression<Func<T, bool>> predicate);

        Task<int> InsertAsync(T entity);
        Task<int> InsertAllAsync(IEnumerable<T> entities);
        Task<int> UpdateAsync(T entity);
        Task<int> InsertOrReplaceAsync(T entity);
        Task<int> DeleteAsync(T entity);

        // Raw query escape hatch (use sparingly)
        IAsyncEnumerable<T> QueryAsync(string query, params object[] args);
    }
}
