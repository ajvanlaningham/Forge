using System.Globalization;
using System.Text.RegularExpressions;

namespace Forge.Constants
{
    /// <summary>
    /// When to back up, what to call the file, and which old copies to prune (PBI 10.1).
    /// Pure, so the rules are testable; the I/O lives in the app's BackupService.
    /// </summary>
    public static partial class BackupPolicy
    {
        /// <summary>Copies kept. Older ones are pruned after each successful backup.</summary>
        public const int Retain = 14;

        /// <summary>
        /// Bumped whenever the database schema changes incompatibly, and stamped into every backup's
        /// file name so a restore (PBI 10.2) can refuse — or migrate — an older shape.
        /// 2 = training programs + sessions (Epic 8).
        /// </summary>
        public const int SchemaVersion = 2;

        private const string Prefix = "forge-backup-";

        /// <summary>
        /// A backup is due when none has been made on the current local calendar day. Daily by
        /// date rather than every 24 h, so opening the app each morning reliably produces one.
        /// </summary>
        public static bool IsDue(DateTime? lastBackupUtc, DateTime nowUtc)
            => lastBackupUtc is not DateTime last
               || last.ToLocalTime().Date != nowUtc.ToLocalTime().Date;

        /// <summary>e.g. <c>forge-backup-20261111-0715-s2.db3</c> — sorts chronologically as text.</summary>
        public static string FileName(DateTime nowLocal, int schemaVersion = SchemaVersion)
            => $"{Prefix}{nowLocal.ToString("yyyyMMdd-HHmm", CultureInfo.InvariantCulture)}-s{schemaVersion}.db3";

        public static bool IsBackupFile(string name) => BackupName().IsMatch(name);

        /// <summary>Schema version stamped in a backup's name, or null if it isn't one of ours.</summary>
        public static int? SchemaOf(string name)
            => BackupName().Match(name) is { Success: true } m ? int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture) : null;

        /// <summary>The backups to delete so that only the newest <paramref name="retain"/> remain.</summary>
        public static IReadOnlyList<string> ToPrune(IEnumerable<string> existing, int retain = Retain)
            => existing.Where(IsBackupFile)
                .OrderByDescending(n => n, StringComparer.Ordinal)
                .Skip(Math.Max(0, retain))
                .ToList();

        [GeneratedRegex(@"^forge-backup-\d{8}-\d{4}-s(\d+)\.db3$")]
        private static partial Regex BackupName();
    }
}
