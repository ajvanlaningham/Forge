using System.Net;
using System.Text.Json;

using Forge.Constants;
using Forge.Data;
using Forge.Models;
using Forge.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Forge.Services.Implementations
{
    /// <inheritdoc />
    public sealed class StravaService : IStravaService
    {
        // The client secret and tokens live in SecureStorage (Android Keystore), so they never
        // reach a backup file or the public APK. A restored install pastes the secret and connects again.
        private const string SecretKey = "strava.secret";
        private const string AccessKey = "strava.access";
        private const string RefreshKey = "strava.refresh";
        private const string ExpiresKey = "strava.expires";
        private const string ConnectedPref = "Strava.ConnectedUtc";
        private const string AthletePref = "Strava.Athlete";
        private const string ResultPref = "Strava.LastResult";

        private readonly ITrainingService _training;
        private readonly IProgramService _programs;
        private readonly IRepository<TrainingSessionRow> _sessions;
        private readonly IRepository<SessionSetRow> _sets;
        private readonly IRepository<StravaPostRow> _posts;
        private readonly ILogger<StravaService> _log;
        private readonly HttpClient _http = new() { Timeout = TimeSpan.FromSeconds(30) };
        private readonly SemaphoreSlim _gate = new(1, 1);

        public StravaService(ITrainingService training, IProgramService programs,
            IRepository<TrainingSessionRow> sessions, IRepository<SessionSetRow> sets,
            IRepository<StravaPostRow> posts, ILogger<StravaService> log)
        {
            _training = training;
            _programs = programs;
            _sessions = sessions;
            _sets = sets;
            _posts = posts;
            _log = log;
        }

        public bool IsAvailable => StravaAuth.ClientId.Length > 0;

        public async Task<bool> HasSecretAsync() => await SecureStorage.Default.GetAsync(SecretKey) is { Length: > 0 };

        public async Task<bool> SaveSecretAsync(string? pasted)
        {
            if (StravaAuth.CleanSecret(pasted) is not { } secret) return false;
            await SecureStorage.Default.SetAsync(SecretKey, secret);
            return true;
        }

        public bool IsConnected => IsAvailable && Preferences.Get(ConnectedPref, 0L) > 0;

        public string? AthleteName => IsConnected ? Preferences.Get(AthletePref, null as string) : null;

        public string LastResult
        {
            get => Preferences.Get(ResultPref, "");
            private set => Preferences.Set(ResultPref, value);
        }

        // ---------------------------------------------------------------- sign-in

        public async Task<string> ConnectAsync()
        {
            if (!IsAvailable) return "Strava isn't set up in this build.";
            if (!await HasSecretAsync()) return "Paste the client secret first.";
            WebAuthenticatorResult result;
            try
            {
                result = await WebAuthenticator.Default.AuthenticateAsync(
                    new Uri(StravaAuth.AuthorizeUrl(StravaAuth.ClientId)),
                    new Uri(StravaAuth.RedirectUri));
            }
            catch (TaskCanceledException) { return "Cancelled."; }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Strava sign-in failed");
                return "Couldn't open Strava's sign-in.";
            }

            if (result.Properties.TryGetValue("error", out var error))
                return error == "access_denied" ? "Not connected: Strava access was declined." : $"Strava said: {error}";
            if (!result.Properties.TryGetValue("code", out var code))
                return "Strava didn't send a sign-in code.";
            result.Properties.TryGetValue("scope", out var scope);
            if (!StravaAuth.CanWrite(scope))
                return "Not connected: Forge needs permission to upload activities. Connect again and leave that box ticked.";

            try
            {
                var (json, status) = await TokenRequestAsync(new()
                {
                    ["code"] = code,
                    ["grant_type"] = "authorization_code",
                });
                if (json is null)
                {
                    if (status is 400 or 401)
                    {
                        // Most likely a mistyped or reset secret: forget it so Connect asks again.
                        SecureStorage.Default.Remove(SecretKey);
                        return "Strava didn't accept the client secret. Connect again and paste it fresh.";
                    }
                    return "Strava refused the sign-in. Try again.";
                }

                await StoreTokensAsync(json.Value);
                var athlete = json.Value.TryGetProperty("athlete", out var a)
                    ? $"{Str(a, "firstname")} {Str(a, "lastname")}".Trim()
                    : "";
                Preferences.Set(AthletePref, athlete);
                // Only sessions finished from now on go out: connecting doesn't flood the feed with history.
                Preferences.Set(ConnectedPref, DateTime.UtcNow.Ticks);
                LastResult = "";
                return athlete.Length > 0 ? $"Connected as {athlete}." : "Connected.";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Strava token exchange failed");
                return "Couldn't reach Strava. Check the connection and try again.";
            }
        }

        public async Task DisconnectAsync()
        {
            var token = await SecureStorage.Default.GetAsync(AccessKey);
            if (token is not null)
            {
                // Revoke on Strava's side too, best effort: offline, we still forget locally.
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Post, StravaAuth.DeauthorizeEndpoint)
                    {
                        Content = new FormUrlEncodedContent(new Dictionary<string, string> { ["access_token"] = token })
                    };
                    using var _ = await _http.SendAsync(req);
                }
                catch (Exception ex) { _log.LogInformation(ex, "Strava deauthorize failed"); }
            }
            Forget();
        }

        private static void Forget()
        {
            SecureStorage.Default.Remove(AccessKey);
            SecureStorage.Default.Remove(RefreshKey);
            SecureStorage.Default.Remove(ExpiresKey);
            Preferences.Remove(ConnectedPref);
            Preferences.Remove(AthletePref);
        }

        private async Task<(JsonElement? Json, int Status)> TokenRequestAsync(Dictionary<string, string> form)
        {
            var secret = await SecureStorage.Default.GetAsync(SecretKey);
            if (string.IsNullOrEmpty(secret)) return (null, 0);
            form["client_id"] = StravaAuth.ClientId;
            form["client_secret"] = secret;
            using var resp = await _http.PostAsync(StravaAuth.TokenEndpoint, new FormUrlEncodedContent(form));
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("Strava token endpoint returned {Status}", (int)resp.StatusCode);
                return (null, (int)resp.StatusCode);
            }
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
            return (doc.RootElement.Clone(), (int)resp.StatusCode);
        }

        private static async Task StoreTokensAsync(JsonElement json)
        {
            await SecureStorage.Default.SetAsync(AccessKey, Str(json, "access_token"));
            await SecureStorage.Default.SetAsync(RefreshKey, Str(json, "refresh_token"));
            await SecureStorage.Default.SetAsync(ExpiresKey, json.GetProperty("expires_at").GetInt64().ToString());
        }

        private static string Str(JsonElement e, string name)
            => e.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() ?? "" : "";

        /// <summary>A live access token, refreshed if it's about to expire. Null when the athlete must reconnect.</summary>
        private async Task<string?> AccessTokenAsync()
        {
            var token = await SecureStorage.Default.GetAsync(AccessKey);
            var refresh = await SecureStorage.Default.GetAsync(RefreshKey);
            if (token is null || refresh is null) return null;

            long.TryParse(await SecureStorage.Default.GetAsync(ExpiresKey), out var expires);
            if (!StravaAuth.NeedsRefresh(expires, DateTimeOffset.UtcNow)) return token;

            var (json, _) = await TokenRequestAsync(new()
            {
                ["grant_type"] = "refresh_token",
                ["refresh_token"] = refresh,
            });
            if (json is null) return null;
            await StoreTokensAsync(json.Value);
            return Str(json.Value, "access_token");
        }

        // ---------------------------------------------------------------- the queue

        private async Task<IReadOnlyList<int>> PendingAsync()
        {
            if (!IsConnected) return [];
            var connected = new DateTime(Preferences.Get(ConnectedPref, 0L), DateTimeKind.Utc);
            var finished = await _sessions.WhereAsync(s => s.Status == (int)SessionStatus.Finished && s.EndedAtUtc != null);
            var recent = finished.Where(s => s.EndedAtUtc >= connected.Ticks).ToList();
            if (recent.Count == 0) return [];

            var ids = recent.Select(s => s.Id).ToList();
            var working = (await _sets.WhereAsync(s => ids.Contains(s.SessionId) && !s.IsWarmup))
                .GroupBy(s => s.SessionId).ToDictionary(g => g.Key, g => g.Count());
            var posted = (await _posts.GetAllAsync()).Select(p => p.SessionId);

            return StravaActivity.Pending(
                recent.Select(s => new StravaCandidate(s.Id, new DateTime(s.EndedAtUtc!.Value, DateTimeKind.Utc), working.GetValueOrDefault(s.Id))),
                posted, connected);
        }

        public async Task<int> PendingCountAsync() => (await PendingAsync()).Count;

        public async Task SyncAsync()
        {
            if (!IsConnected) return;
            if (!await _gate.WaitAsync(0)) return; // a sync is already running
            try
            {
                var pending = await PendingAsync();
                if (pending.Count == 0) return;
                if (Connectivity.Current.NetworkAccess != NetworkAccess.Internet)
                {
                    LastResult = $"{Plural(pending.Count)} waiting for a connection.";
                    return;
                }

                var token = await AccessTokenAsync();
                if (token is null)
                {
                    LastResult = "Strava sign-in expired. Disconnect and connect again.";
                    return;
                }

                var posted = 0;
                foreach (var id in pending)
                {
                    var outcome = await PostAsync(id, token);
                    if (outcome == PostOutcome.Posted) posted++;
                    else if (outcome == PostOutcome.StopForNow) break;
                }
                var left = (await PendingAsync()).Count;
                if (posted > 0 || left == 0)
                    LastResult = left == 0 ? $"Posted {Plural(posted)}." : $"Posted {Plural(posted)}; {left} still waiting.";
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Strava sync failed");
                LastResult = "Couldn't reach Strava; will try again later.";
            }
            finally { _gate.Release(); }
        }

        private static string Plural(int n) => n == 1 ? "1 session" : $"{n} sessions";

        private enum PostOutcome { Posted, Refused, StopForNow }

        private async Task<PostOutcome> PostAsync(int sessionId, string token)
        {
            var snap = await _training.GetSessionAsync(sessionId);
            var row = snap.Session;
            var start = new DateTime(row.StartedAtUtc, DateTimeKind.Utc);
            var end = new DateTime(row.EndedAtUtc ?? row.StartedAtUtc, DateTimeKind.Utc);

            var exercises = snap.Exercises
                .Where(e => !e.Row.Skipped)
                .Select(e => new StravaExercise(e.Row.ExerciseName, e.Sets
                    .Where(s => !s.IsWarmup)
                    .OrderBy(s => s.Position)
                    .Select(s => new StravaSet(s.LoadLb > 0 ? LoadDisplay.Label(s.LoadLb) : "", s.Reps, s.Seconds))
                    .ToList()));

            string? quest = null;
            if (row.ProgramId is not null && row.ProgramVersion is int v
                && await _programs.GetProgramAsync(row.ProgramId, v) is { } def)
                quest = $"Quest: {HeroQuest.Name(def)}";

            var form = new Dictionary<string, string>
            {
                ["name"] = StravaActivity.Name(row.Title),
                ["sport_type"] = StravaActivity.SportType,
                ["type"] = StravaActivity.SportType,
                ["start_date_local"] = StravaActivity.StartDateLocal(start, TimeZoneInfo.Local),
                ["elapsed_time"] = StravaActivity.ElapsedSeconds(start, end).ToString(),
                ["description"] = StravaActivity.Description(exercises, quest),
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, StravaAuth.ActivitiesEndpoint)
            {
                Content = new FormUrlEncodedContent(form)
            };
            req.Headers.Authorization = new("Bearer", token);
            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(body);
                await _posts.InsertOrReplaceAsync(new StravaPostRow
                {
                    SessionId = sessionId,
                    ActivityId = doc.RootElement.GetProperty("id").GetInt64(),
                    PostedAtUtc = DateTime.UtcNow.Ticks,
                });
                return PostOutcome.Posted;
            }

            _log.LogWarning("Strava refused session {Id}: {Status} {Body}", sessionId, (int)resp.StatusCode, body);
            switch (resp.StatusCode)
            {
                case HttpStatusCode.Unauthorized:
                    LastResult = "Strava sign-in expired. Disconnect and connect again.";
                    return PostOutcome.StopForNow;
                case HttpStatusCode.TooManyRequests:
                    LastResult = "Strava is busy; will try again later.";
                    return PostOutcome.StopForNow;
                case >= HttpStatusCode.InternalServerError:
                    LastResult = "Strava is having trouble; will try again later.";
                    return PostOutcome.StopForNow;
                default:
                    // Something about this session Strava won't take: record it so it isn't retried forever.
                    await _posts.InsertOrReplaceAsync(new StravaPostRow
                    {
                        SessionId = sessionId,
                        PostedAtUtc = DateTime.UtcNow.Ticks,
                        Error = $"{(int)resp.StatusCode}: {body[..Math.Min(body.Length, 300)]}",
                    });
                    LastResult = $"Strava refused one session ({(int)resp.StatusCode}).";
                    return PostOutcome.Refused;
            }
        }
    }
}
