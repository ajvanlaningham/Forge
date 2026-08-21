using SQLite;

namespace Forge.Models
{
    [Table("UserStats")]
    public sealed class UserStats
    {
        [PrimaryKey]
        public string Id { get; set; } = Guid.NewGuid().ToString("N");

        /// <summary>
        /// Cached copy of the level implied by <see cref="Xp"/>. NOT the source of truth —
        /// always display <c>GameMath.LevelFromXp(Xp)</c>. Kept in sync on save so anyone
        /// inspecting the database is not misled.
        /// </summary>
        public int Level { get; set; } = 1;
        public int Xp { get; set; } = 0;

        public long UpdatedAtUnix { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        public bool IsDirty { get; set; } = true;
    }
}
