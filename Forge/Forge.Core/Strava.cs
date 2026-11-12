using System.Globalization;
using System.Text;

namespace Forge.Constants
{
    /// <summary>One working set as Strava's description shows it. <c>Load</c> is already a label ("35 lb", "16 kg"), or empty for bodyweight.</summary>
    public sealed record StravaSet(string Load, int? Reps, int? Seconds);

    public sealed record StravaExercise(string Name, IReadOnlyList<StravaSet> Sets);

    /// <summary>A finished session as the export queue sees it.</summary>
    public sealed record StravaCandidate(int SessionId, DateTime EndedUtc, int WorkingSets);

    /// <summary>
    /// Turns a finished strength session into a Strava activity: a Weight Training entry
    /// whose description reads like a logbook page. Only working sets go out; warm-ups don't.
    /// </summary>
    public static class StravaActivity
    {
        public const string SportType = "WeightTraining";
        public const string Footer = "Logged with Forge";

        public static string Name(string? sessionTitle)
            => string.IsNullOrWhiteSpace(sessionTitle) ? "Strength session" : sessionTitle.Trim();

        /// <summary>Strava wants the local wall-clock start, ISO 8601 without an offset.</summary>
        public static string StartDateLocal(DateTime startUtc, TimeZoneInfo zone)
            => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(startUtc, DateTimeKind.Utc), zone)
                .ToString("yyyy-MM-dd'T'HH:mm:ss", CultureInfo.InvariantCulture);

        /// <summary>Whole seconds, at least one minute (Strava rejects a zero-length activity).</summary>
        public static int ElapsedSeconds(DateTime startUtc, DateTime endUtc)
            => Math.Max(60, (int)Math.Round((endUtc - startUtc).TotalSeconds));

        public static string Description(IEnumerable<StravaExercise> exercises, string? questLine = null)
        {
            var sb = new StringBuilder();
            foreach (var ex in exercises)
            {
                var line = ExerciseLine(ex);
                if (line.Length > 0) sb.AppendLine(line);
            }
            sb.AppendLine();
            if (!string.IsNullOrWhiteSpace(questLine)) sb.AppendLine(questLine.Trim());
            sb.Append(Footer);
            return sb.ToString().TrimStart();
        }

        /// <summary>
        /// "Floor Press: 3 × 8 @ 35 lb" when every set matches; "Row: 35 lb × 10, 10, 8" for one
        /// load; "Press: 35 lb × 10, 40 lb × 8" when the load moved; "Plank: 2 × 45s" for holds.
        /// Empty when nothing was logged.
        /// </summary>
        public static string ExerciseLine(StravaExercise ex)
        {
            var sets = ex.Sets.Where(s => s.Reps is not null || s.Seconds is not null).ToList();
            if (sets.Count == 0) return "";
            return $"{ex.Name}: {Sets(sets)}";
        }

        private static string Sets(IReadOnlyList<StravaSet> sets)
        {
            var effort = sets.Select(Effort).ToList();
            var loads = sets.Select(s => s.Load.Trim()).ToList();
            var oneLoad = loads.Distinct().Count() == 1;
            var load = loads[0];

            if (oneLoad && effort.Distinct().Count() == 1)
            {
                var same = sets.Count == 1 ? effort[0] : $"{sets.Count} × {effort[0]}";
                return load.Length == 0 ? same : $"{same} @ {load}";
            }
            if (oneLoad)
                return load.Length == 0 ? string.Join(", ", effort) : $"{load} × {string.Join(", ", effort)}";

            return string.Join(", ", sets.Select((s, i) =>
                loads[i].Length == 0 ? effort[i] : $"{loads[i]} × {effort[i]}"));
        }

        private static string Effort(StravaSet s)
            => s.Reps is int r ? r.ToString(CultureInfo.InvariantCulture) : $"{s.Seconds}s";

        /// <summary>
        /// Sessions still to post: finished after Strava was connected (history from before
        /// doesn't flood the feed), with at least one working set, and not posted yet.
        /// Oldest first, so the feed keeps its order.
        /// </summary>
        public static IReadOnlyList<int> Pending(IEnumerable<StravaCandidate> finished, IEnumerable<int> posted, DateTime connectedUtc)
        {
            var done = posted.ToHashSet();
            return finished
                .Where(c => c.EndedUtc >= connectedUtc && c.WorkingSets > 0 && !done.Contains(c.SessionId))
                .OrderBy(c => c.EndedUtc)
                .Select(c => c.SessionId)
                .ToList();
        }
    }

    /// <summary>OAuth details for Strava's mobile sign-in.</summary>
    public static class StravaAuth
    {
        /// <summary>
        /// The Forge API app on strava.com/settings/api. Not secret: it is in every sign-in URL.
        /// The client secret is never built in (the APKs are public downloads); each phone
        /// pastes it once and keeps it in secure storage. Strava has no PKCE, so a secret is needed.
        /// </summary>
        public const string ClientId = "";
        public const string AuthorizeEndpoint = "https://www.strava.com/oauth/mobile/authorize";
        public const string TokenEndpoint = "https://www.strava.com/oauth/token";
        public const string DeauthorizeEndpoint = "https://www.strava.com/oauth/deauthorize";
        public const string ActivitiesEndpoint = "https://www.strava.com/api/v3/activities";

        /// <summary>The host must match the API app's Authorization Callback Domain ("localhost").</summary>
        public const string RedirectUri = "forgestrava://localhost/strava";
        public const string Scope = "activity:write";

        public static string AuthorizeUrl(string clientId)
            => $"{AuthorizeEndpoint}?client_id={Uri.EscapeDataString(clientId)}"
             + $"&redirect_uri={Uri.EscapeDataString(RedirectUri)}"
             + "&response_type=code&approval_prompt=auto"
             + $"&scope={Uri.EscapeDataString(Scope)}";

        /// <summary>
        /// Strava's consent page lets the athlete untick permissions; the redirect reports what
        /// was granted. Without activity:write there is nothing for Forge to do.
        /// </summary>
        public static bool CanWrite(string? grantedScope)
            => (grantedScope ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(s => s is "activity:write");

        /// <summary>A Strava client secret is 40 hex characters. Trims what a paste drags along.</summary>
        public static string? CleanSecret(string? pasted)
        {
            var s = (pasted ?? "").Trim().ToLowerInvariant();
            return s.Length == 40 && s.All(Uri.IsHexDigit) ? s : null;
        }

        /// <summary>Access tokens last six hours; refresh with five minutes to spare.</summary>
        public static bool NeedsRefresh(long expiresAtUnix, DateTimeOffset now)
            => now.ToUnixTimeSeconds() >= expiresAtUnix - 300;
    }
}
