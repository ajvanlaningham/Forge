using SQLite;

namespace Forge.Models
{
    [Table("UserStats")]
    public sealed class UserStats
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        public int Level { get; set; } = 1;
        public int Xp { get; set; } = 0;

        public long UpdatedAtUnix { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool IsDirty { get; set; } = true;
    }
}
