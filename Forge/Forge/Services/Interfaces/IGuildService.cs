using Forge.Guild;

namespace Forge.Services.Interfaces
{
    /// <summary>
    /// Membership in a guild on nidavellir (Decision 24). Sends this player's game state only
    /// (never health or training details), fetches the guild, and pays guild bonuses through the
    /// XP ledger. Offline-first: everything here can fail quietly and retry on the next sync.
    /// </summary>
    public interface IGuildService
    {
        bool InGuild { get; }
        string? GuildName { get; }
        string? InviteCode { get; }
        string? MemberId { get; }

        /// <summary>What the guild calls this player.</summary>
        string DisplayName { get; set; }

        /// <summary>The guild as last fetched (shown when offline).</summary>
        GuildDto? Snapshot { get; }
        DateTime? LastSyncUtc { get; }

        /// <summary>Starts a guild. Returns an error message, or null on success.</summary>
        Task<string?> CreateAsync(string guildName);

        /// <summary>Joins with an invite code. Returns an error message, or null on success.</summary>
        Task<string?> JoinAsync(string inviteCode);

        Task<string?> LeaveAsync();

        /// <summary>Sends this player's status, fetches the guild, pays bonuses. Returns an error message, or null.</summary>
        Task<string?> SyncAsync();

        /// <summary>The members, as the rules see them (join dates in local time).</summary>
        IReadOnlyList<GuildMember> Members();
    }
}
