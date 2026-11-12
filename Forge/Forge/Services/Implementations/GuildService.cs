using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

using Forge.Common;
using Forge.Constants;
using Forge.Data;
using Forge.Guild;
using Forge.Models;
using Forge.Services.Interfaces;

using Microsoft.Extensions.Logging;

namespace Forge.Services.Implementations
{
    /// <inheritdoc />
    public sealed class GuildService : IGuildService
    {
        private const string TokenKey = "guild.token";
        private const string IdPref = "Guild.Id", MemberPref = "Guild.MemberId", NamePref = "Guild.Name",
            InvitePref = "Guild.Invite", DisplayPref = "Guild.DisplayName", SnapshotPref = "Guild.Snapshot", SyncedPref = "Guild.SyncedUtc";
        private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

        private readonly IRepository<QuestCompletionRow> _rest;
        private readonly IRepository<ProgramDayCompletionRow> _days;
        private readonly IRepository<ForgeResultRow> _forged;
        private readonly IRepository<GuildRewardRow> _rewards;
        private readonly IProgramService _programs;
        private readonly IForgeService _forge;
        private readonly IStatsStore _stats;
        private readonly IXpService _xp;
        private readonly ProgressService _progress;
        private readonly ILogger<GuildService> _log;
        private readonly HttpClient _http = new() { BaseAddress = new Uri(GameConstants.Nidavellir.BaseUrl), Timeout = TimeSpan.FromSeconds(20) };
        private readonly SemaphoreSlim _gate = new(1, 1);

        public GuildService(IRepository<QuestCompletionRow> rest, IRepository<ProgramDayCompletionRow> days,
            IRepository<ForgeResultRow> forged, IRepository<GuildRewardRow> rewards, IProgramService programs,
            IForgeService forge, IStatsStore stats, IXpService xp, ProgressService progress, ILogger<GuildService> log)
        {
            _rest = rest;
            _days = days;
            _forged = forged;
            _rewards = rewards;
            _programs = programs;
            _forge = forge;
            _stats = stats;
            _xp = xp;
            _progress = progress;
            _log = log;
        }

        public bool InGuild => Preferences.Get(MemberPref, null as string) is { Length: > 0 };
        public string? GuildName => InGuild ? Preferences.Get(NamePref, null as string) : null;
        public string? InviteCode => InGuild ? Preferences.Get(InvitePref, null as string) : null;
        public string? MemberId => InGuild ? Preferences.Get(MemberPref, null as string) : null;

        public string DisplayName
        {
            get => Preferences.Get(DisplayPref, "");
            set => Preferences.Set(DisplayPref, (value ?? "").Trim()[..Math.Min((value ?? "").Trim().Length, GuildLimits.MaxNameLength)]);
        }

        public GuildDto? Snapshot
        {
            get
            {
                var json = Preferences.Get(SnapshotPref, null as string);
                if (string.IsNullOrEmpty(json)) return null;
                try { return JsonSerializer.Deserialize<GuildDto>(json, Json); } catch { return null; }
            }
        }

        public DateTime? LastSyncUtc => Preferences.Get(SyncedPref, 0L) is var t and > 0 ? new DateTime(t, DateTimeKind.Utc) : null;

        public IReadOnlyList<GuildMember> Members()
            => Snapshot?.Members.Select(m => new GuildMember(m.Id, DateOnly.FromDateTime(m.JoinedUtc.ToLocalTime()), m.Status)).ToList()
               ?? (IReadOnlyList<GuildMember>)[];

        // ------------------------------------------------------------ what we share

        /// <summary>This player's game state. Deliberately nothing about weights, sets or health.</summary>
        private async Task<MemberStatus> BuildStatusAsync()
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var from = today.AddDays(-(GuildLimits.DaysSent - 1));
            var fromKey = WeekMath.DateKey(from);

            var marks = new Dictionary<string, GuildDayKind>();
            foreach (var r in await _rest.WhereAsync(r => r.Completed && r.Kind == (int)QuestKind.Mobility))
                if (string.CompareOrdinal(r.DateKey, fromKey) >= 0) marks[r.DateKey] = GuildDayKind.ActiveRest;
            foreach (var r in await _days.WhereAsync(r => r.Completed))
                if (string.CompareOrdinal(r.DateKey, fromKey) >= 0) marks[r.DateKey] = GuildDayKind.Trained;

            await _stats.InitAsync();
            var level = GameMath.LevelFromXp((await _stats.GetUserStatsAsync()).Xp);
            var view = await _programs.GetViewAsync(today);
            var forgedDates = (await _forged.WhereAsync(r => r.Forged)).Select(r => r.DateKey).Where(k => k.Length == 10).ToList();
            var gear = (await _forge.GetForgedAsync()).Select(f => new GearMark((int)f.Slot, f.Metal)).Take(GuildLimits.MaxGear).ToList();
            var (streak, _) = await _progress.GetStreakAsync();

            var name = DisplayName.Length > 0 ? DisplayName : UserSettings.HeroTitle;
            return new MemberStatus(
                name[..Math.Min(name.Length, GuildLimits.MaxNameLength)],
                UserSettings.HeroRace, UserSettings.HeroClass, level,
                view?.DayCount ?? 0,
                view is null ? null : HeroQuest.Name(view.Program.Definition),
                view?.Week ?? 0,
                streak,
                marks.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => new DayMark(kv.Key, kv.Value)).ToList(),
                forgedDates.TakeLast(GuildLimits.MaxForged).ToList(),
                gear);
        }

        // ------------------------------------------------------------ joining

        public async Task<string?> CreateAsync(string guildName)
        {
            var name = guildName.Trim();
            if (name.Length is 0 or > GuildLimits.MaxGuildNameLength) return $"A guild name is 1 to {GuildLimits.MaxGuildNameLength} characters.";
            return await JoinWithAsync(async status => await _http.PostAsJsonAsync("v1/guilds", new CreateGuildRequest(name, status), Json));
        }

        public async Task<string?> JoinAsync(string inviteCode)
        {
            var code = inviteCode.Trim();
            if (code.Length < 8) return "An invite code looks like K7QF-9M2D.";
            return await JoinWithAsync(async status => await _http.PostAsJsonAsync("v1/guilds/join", new JoinGuildRequest(code, status), Json));
        }

        private async Task<string?> JoinWithAsync(Func<MemberStatus, Task<HttpResponseMessage>> send)
        {
            if (InGuild) return "Leave your current guild first.";
            try
            {
                using var resp = await send(await BuildStatusAsync());
                if (resp.StatusCode == HttpStatusCode.NotFound) return "No guild has that invite code.";
                if (resp.StatusCode == HttpStatusCode.Conflict) return $"That guild is full ({GuildLimits.MaxMembers} members).";
                if (!resp.IsSuccessStatusCode) return $"The guild hall refused ({(int)resp.StatusCode}).";
                var joined = (await resp.Content.ReadFromJsonAsync<JoinedResponse>(Json))!;
                await SecureStorage.Default.SetAsync(TokenKey, joined.Token);
                Preferences.Set(IdPref, joined.GuildId);
                Preferences.Set(MemberPref, joined.MemberId);
                Preferences.Set(NamePref, joined.GuildName);
                Preferences.Set(InvitePref, joined.InviteCode);
                return await SyncAsync();
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Guild join failed");
                return Unreachable;
            }
        }

        private const string Unreachable = "Couldn't reach the guild hall. Check the connection and try again.";

        public async Task<string?> LeaveAsync()
        {
            var token = await SecureStorage.Default.GetAsync(TokenKey);
            if (token is not null)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Delete, "v1/me");
                    req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var resp = await _http.SendAsync(req);
                    if (!resp.IsSuccessStatusCode && resp.StatusCode != HttpStatusCode.Unauthorized)
                        return $"The guild hall refused ({(int)resp.StatusCode}).";
                }
                catch (Exception ex)
                {
                    _log.LogWarning(ex, "Guild leave failed");
                    return Unreachable;
                }
            }
            Forget();
            return null;
        }

        /// <summary>Bonuses already paid are kept: leaving doesn't take XP back.</summary>
        private static void Forget()
        {
            SecureStorage.Default.Remove(TokenKey);
            foreach (var k in new[] { IdPref, MemberPref, NamePref, InvitePref, SnapshotPref, SyncedPref }) Preferences.Remove(k);
        }

        // ------------------------------------------------------------ sync

        public async Task<string?> SyncAsync()
        {
            if (!InGuild) return null;
            if (!await _gate.WaitAsync(0)) return null; // one at a time; the running one will finish
            try
            {
                var token = await SecureStorage.Default.GetAsync(TokenKey);
                if (token is null) { Forget(); return "This phone lost its guild key. Join again with the invite code."; }

                using (var put = new HttpRequestMessage(HttpMethod.Put, "v1/me") { Content = JsonContent.Create(await BuildStatusAsync(), options: Json) })
                {
                    put.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var resp = await _http.SendAsync(put);
                    if (resp.StatusCode == HttpStatusCode.Unauthorized) { Forget(); return "You're no longer in that guild."; }
                    if (!resp.IsSuccessStatusCode) return $"The guild hall refused ({(int)resp.StatusCode}).";
                }

                GuildDto guild;
                using (var get = new HttpRequestMessage(HttpMethod.Get, "v1/guild"))
                {
                    get.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
                    using var resp = await _http.SendAsync(get);
                    if (resp.StatusCode == HttpStatusCode.Unauthorized) { Forget(); return "You're no longer in that guild."; }
                    if (!resp.IsSuccessStatusCode) return $"The guild hall refused ({(int)resp.StatusCode}).";
                    guild = (await resp.Content.ReadFromJsonAsync<GuildDto>(Json))!;
                }

                Preferences.Set(SnapshotPref, JsonSerializer.Serialize(guild, Json));
                Preferences.Set(NamePref, guild.Name);
                Preferences.Set(InvitePref, guild.InviteCode);
                Preferences.Set(SyncedPref, DateTime.UtcNow.Ticks);
                await PayRewardsAsync();
                return null;
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Guild sync failed");
                return Unreachable;
            }
            finally { _gate.Release(); }
        }

        private async Task PayRewardsAsync()
        {
            if (MemberId is not { } me) return;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var delta = 0;
            foreach (var reward in GuildRules.RewardsFor(Members().ToList(), me, today))
            {
                var row = await _rewards.GetByIdAsync(reward.Key) ?? new GuildRewardRow { Key = reward.Key };
                var (entry, change) = XpLedger.Reconcile(reward.Earned, new XpEntry(row.Granted, row.Awarded), reward.Xp);
                if (change == 0) continue;
                row.Granted = entry.Granted;
                row.Awarded = entry.Awarded;
                await _rewards.InsertOrReplaceAsync(row);
                delta += change;
            }
            if (delta != 0) await _xp.ApplyAsync(delta);
        }
    }
}
