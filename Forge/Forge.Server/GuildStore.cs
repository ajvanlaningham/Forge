using System.Security.Cryptography;
using System.Text.Json;

using Forge.Guild;

using Microsoft.Data.Sqlite;

namespace Forge.Server
{
    /// <summary>
    /// SQLite storage for guilds, members and encrypted backups. Tokens are stored only as SHA-256
    /// hashes; member status is the game-state JSON a phone sent (validated, size-capped); backups
    /// are opaque ciphertext the server can't read.
    /// </summary>
    public sealed class GuildStore
    {
        private readonly string _connectionString;
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
        private const string InviteAlphabet = "23456789ABCDEFGHJKMNPQRSTVWXYZ"; // no 0/1/I/L/O/U

        public GuildStore(string dataDir)
        {
            Directory.CreateDirectory(dataDir);
            _connectionString = new SqliteConnectionStringBuilder { DataSource = Path.Combine(dataDir, "nidavellir.db") }.ToString();
            using var db = Open();
            Exec(db, """
                PRAGMA journal_mode = WAL;
                CREATE TABLE IF NOT EXISTS guilds (
                    id TEXT PRIMARY KEY, name TEXT NOT NULL, invite_code TEXT NOT NULL UNIQUE, created_utc TEXT NOT NULL);
                CREATE TABLE IF NOT EXISTS members (
                    id TEXT PRIMARY KEY, guild_id TEXT NOT NULL REFERENCES guilds(id) ON DELETE CASCADE,
                    token_hash TEXT NOT NULL UNIQUE, joined_utc TEXT NOT NULL, updated_utc TEXT NOT NULL, status TEXT NOT NULL);
                CREATE INDEX IF NOT EXISTS members_guild ON members(guild_id);
                CREATE TABLE IF NOT EXISTS backups (
                    lookup_id TEXT PRIMARY KEY, token_hash TEXT NOT NULL, blob BLOB NOT NULL, updated_utc TEXT NOT NULL);
                """);
        }

        private SqliteConnection Open()
        {
            var db = new SqliteConnection(_connectionString);
            db.Open();
            Exec(db, "PRAGMA foreign_keys = ON;");
            return db;
        }

        private static void Exec(SqliteConnection db, string sql, params (string, object?)[] args)
        {
            using var cmd = db.CreateCommand();
            cmd.CommandText = sql;
            foreach (var (k, v) in args) cmd.Parameters.AddWithValue(k, v ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        private static string NewId() => Convert.ToHexString(RandomNumberGenerator.GetBytes(12)).ToLowerInvariant();
        private static string NewToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

        private static string NewInviteCode()
        {
            var chars = Enumerable.Range(0, 8).Select(_ => InviteAlphabet[RandomNumberGenerator.GetInt32(InviteAlphabet.Length)]).ToArray();
            return $"{new string(chars, 0, 4)}-{new string(chars, 4, 4)}";
        }

        public static string NormalizeInvite(string code)
        {
            var s = new string(code.ToUpperInvariant().Where(char.IsLetterOrDigit).ToArray());
            return s.Length == 8 ? $"{s[..4]}-{s[4..]}" : s;
        }

        private static string Now() => DateTime.UtcNow.ToString("O");

        public JoinedResponse Create(string guildName, MemberStatus member)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            var guildId = NewId();
            string invite;
            while (true)
            {
                invite = NewInviteCode();
                using var check = db.CreateCommand();
                check.CommandText = "SELECT 1 FROM guilds WHERE invite_code = $c";
                check.Parameters.AddWithValue("$c", invite);
                if (check.ExecuteScalar() is null) break;
            }
            Exec(db, "INSERT INTO guilds VALUES ($id, $name, $code, $now)", ("$id", guildId), ("$name", guildName.Trim()), ("$code", invite), ("$now", Now()));
            var (memberId, token) = AddMember(db, guildId, member);
            tx.Commit();
            return new JoinedResponse(guildId, guildName.Trim(), invite, memberId, token);
        }

        /// <summary>Null when the code is unknown; throws <see cref="InvalidOperationException"/> when the guild is full.</summary>
        public JoinedResponse? Join(string inviteCode, MemberStatus member)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            using var find = db.CreateCommand();
            find.CommandText = "SELECT id, name, invite_code FROM guilds WHERE invite_code = $c";
            find.Parameters.AddWithValue("$c", NormalizeInvite(inviteCode));
            using var r = find.ExecuteReader();
            if (!r.Read()) return null;
            var (guildId, name, code) = (r.GetString(0), r.GetString(1), r.GetString(2));
            r.Close();

            using var count = db.CreateCommand();
            count.CommandText = "SELECT COUNT(*) FROM members WHERE guild_id = $g";
            count.Parameters.AddWithValue("$g", guildId);
            if ((long)count.ExecuteScalar()! >= GuildLimits.MaxMembers) throw new InvalidOperationException("full");

            var (memberId, token) = AddMember(db, guildId, member);
            tx.Commit();
            return new JoinedResponse(guildId, name, code, memberId, token);
        }

        private static (string MemberId, string Token) AddMember(SqliteConnection db, string guildId, MemberStatus member)
        {
            var id = NewId();
            var token = NewToken();
            var now = Now();
            Exec(db, "INSERT INTO members VALUES ($id, $g, $t, $now, $now, $s)",
                ("$id", id), ("$g", guildId), ("$t", BackupCrypto.HashToken(token)), ("$now", now), ("$s", JsonSerializer.Serialize(member, Json)));
            return (id, token);
        }

        /// <summary>The member a bearer token belongs to, or null.</summary>
        public (string MemberId, string GuildId)? Authenticate(string? token)
        {
            if (string.IsNullOrEmpty(token)) return null;
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT id, guild_id FROM members WHERE token_hash = $t";
            cmd.Parameters.AddWithValue("$t", BackupCrypto.HashToken(token));
            using var r = cmd.ExecuteReader();
            return r.Read() ? (r.GetString(0), r.GetString(1)) : null;
        }

        public void UpdateStatus(string memberId, MemberStatus status)
        {
            using var db = Open();
            Exec(db, "UPDATE members SET status = $s, updated_utc = $now WHERE id = $id",
                ("$s", JsonSerializer.Serialize(status, Json)), ("$now", Now()), ("$id", memberId));
        }

        public GuildDto? GetGuild(string guildId)
        {
            using var db = Open();
            using var g = db.CreateCommand();
            g.CommandText = "SELECT name, invite_code FROM guilds WHERE id = $g";
            g.Parameters.AddWithValue("$g", guildId);
            using var gr = g.ExecuteReader();
            if (!gr.Read()) return null;
            var (name, code) = (gr.GetString(0), gr.GetString(1));
            gr.Close();

            using var m = db.CreateCommand();
            m.CommandText = "SELECT id, joined_utc, updated_utc, status FROM members WHERE guild_id = $g ORDER BY joined_utc";
            m.Parameters.AddWithValue("$g", guildId);
            using var mr = m.ExecuteReader();
            var members = new List<GuildMemberDto>();
            while (mr.Read())
                members.Add(new GuildMemberDto(mr.GetString(0),
                    DateTime.Parse(mr.GetString(1), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    DateTime.Parse(mr.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind),
                    JsonSerializer.Deserialize<MemberStatus>(mr.GetString(3), Json)!));
            return new GuildDto(guildId, name, code, members);
        }

        /// <summary>Leaves the guild; the last one out closes it.</summary>
        public void Leave(string memberId, string guildId)
        {
            using var db = Open();
            using var tx = db.BeginTransaction();
            Exec(db, "DELETE FROM members WHERE id = $id", ("$id", memberId));
            Exec(db, "DELETE FROM guilds WHERE id = $g AND NOT EXISTS (SELECT 1 FROM members WHERE guild_id = $g)", ("$g", guildId));
            tx.Commit();
        }

        // ---- encrypted backups: keyed by a value derived from the recovery key ----

        public enum BackupWrite { Saved, Forbidden }

        public BackupWrite PutBackup(string lookupId, string writeToken, byte[] blob)
        {
            using var db = Open();
            var hash = BackupCrypto.HashToken(writeToken);
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT token_hash FROM backups WHERE lookup_id = $id";
            cmd.Parameters.AddWithValue("$id", lookupId);
            if (cmd.ExecuteScalar() is string existing && !CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(existing), System.Text.Encoding.ASCII.GetBytes(hash)))
                return BackupWrite.Forbidden;
            Exec(db, "INSERT INTO backups VALUES ($id, $t, $b, $now) ON CONFLICT(lookup_id) DO UPDATE SET blob = $b, updated_utc = $now",
                ("$id", lookupId), ("$t", hash), ("$b", blob), ("$now", Now()));
            return BackupWrite.Saved;
        }

        /// <summary>Null when there's no backup there or the token doesn't match (indistinguishable on purpose).</summary>
        public (byte[] Blob, DateTime UpdatedUtc)? GetBackup(string lookupId, string writeToken)
        {
            using var db = Open();
            using var cmd = db.CreateCommand();
            cmd.CommandText = "SELECT token_hash, blob, updated_utc FROM backups WHERE lookup_id = $id";
            cmd.Parameters.AddWithValue("$id", lookupId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            var hash = BackupCrypto.HashToken(writeToken);
            if (!CryptographicOperations.FixedTimeEquals(System.Text.Encoding.ASCII.GetBytes(r.GetString(0)), System.Text.Encoding.ASCII.GetBytes(hash)))
                return null;
            return ((byte[])r["blob"], DateTime.Parse(r.GetString(2), null, System.Globalization.DateTimeStyles.RoundtripKind));
        }

        public bool DeleteBackup(string lookupId, string writeToken)
        {
            if (GetBackup(lookupId, writeToken) is null) return false;
            using var db = Open();
            Exec(db, "DELETE FROM backups WHERE lookup_id = $id", ("$id", lookupId));
            return true;
        }
    }
}
