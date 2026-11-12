using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Forge.Guild;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Forge.Tests;

/// <summary>The nidavellir API end to end, in-process, on a throwaway database.</summary>
public sealed class GuildServerTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "forge-server-" + Guid.NewGuid().ToString("N"));
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _http;

    public GuildServerTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.UseSetting("Forge:DataDir", _dir));
        _http = _factory.CreateClient();
    }

    public void Dispose()
    {
        _http.Dispose();
        _factory.Dispose();
        try { Directory.Delete(_dir, true); } catch { /* best effort */ }
    }

    private static MemberStatus Status(string name, int level = 1) => new(name, "halfling", "ranger", level, 4,
        "Copper Patched Cloak", 1, 2, [new DayMark("2026-11-11", GuildDayKind.Trained)], [], [new GearMark(2, "Copper")]);

    private HttpRequestMessage Authed(HttpMethod method, string url, string token, HttpContent? content = null)
    {
        var req = new HttpRequestMessage(method, url) { Content = content };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return req;
    }

    private async Task<JoinedResponse> CreateAsync(string guild = "The Anvil", string name = "Alex")
    {
        var resp = await _http.PostAsJsonAsync("/v1/guilds", new CreateGuildRequest(guild, Status(name)));
        resp.EnsureSuccessStatusCode();
        return (await resp.Content.ReadFromJsonAsync<JoinedResponse>())!;
    }

    [Fact]
    public async Task CreateJoinSync_RoundTrip()
    {
        var alex = await CreateAsync();
        Assert.Matches("^[2-9A-Z]{4}-[2-9A-Z]{4}$", alex.InviteCode);

        // Codes are forgiving: lower case, no dash.
        var join = await _http.PostAsJsonAsync("/v1/guilds/join", new JoinGuildRequest(alex.InviteCode.Replace("-", "").ToLowerInvariant(), Status("Miche")));
        join.EnsureSuccessStatusCode();
        var miche = (await join.Content.ReadFromJsonAsync<JoinedResponse>())!;
        Assert.Equal(alex.GuildId, miche.GuildId);

        var put = await _http.SendAsync(Authed(HttpMethod.Put, "/v1/me", miche.Token, JsonContent.Create(Status("Miche", level: 5))));
        Assert.Equal(HttpStatusCode.NoContent, put.StatusCode);

        var get = await _http.SendAsync(Authed(HttpMethod.Get, "/v1/guild", alex.Token));
        var guild = (await get.Content.ReadFromJsonAsync<GuildDto>())!;
        Assert.Equal("The Anvil", guild.Name);
        Assert.Equal(new[] { "Alex", "Miche" }, guild.Members.Select(m => m.Status.Name));
        Assert.Equal(5, guild.Members[1].Status.Level);
    }

    [Fact]
    public async Task NoToken_OrAWrongOne_IsRefused()
    {
        await CreateAsync();
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.GetAsync("/v1/guild")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Authed(HttpMethod.Get, "/v1/guild", "not-a-token"))).StatusCode);
    }

    [Fact]
    public async Task UnknownCode_IsNotFound_FullGuild_IsConflict()
    {
        Assert.Equal(HttpStatusCode.NotFound, (await _http.PostAsJsonAsync("/v1/guilds/join", new JoinGuildRequest("ZZZZ-ZZZZ", Status("X")))).StatusCode);
        var g = await CreateAsync();
        for (var i = 1; i < GuildLimits.MaxMembers; i++)
            (await _http.PostAsJsonAsync("/v1/guilds/join", new JoinGuildRequest(g.InviteCode, Status($"M{i}")))).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await _http.PostAsJsonAsync("/v1/guilds/join", new JoinGuildRequest(g.InviteCode, Status("Late")))).StatusCode);
    }

    [Fact]
    public async Task BadStatus_IsRejected()
    {
        var resp = await _http.PostAsJsonAsync("/v1/guilds", new CreateGuildRequest("G", Status(new string('x', 50))));
        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
    }

    [Fact]
    public async Task LastOneOut_ClosesTheGuild()
    {
        var g = await CreateAsync();
        Assert.Equal(HttpStatusCode.NoContent, (await _http.SendAsync(Authed(HttpMethod.Delete, "/v1/me", g.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await _http.SendAsync(Authed(HttpMethod.Get, "/v1/guild", g.Token))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.PostAsJsonAsync("/v1/guilds/join", new JoinGuildRequest(g.InviteCode, Status("X")))).StatusCode);
    }

    [Fact]
    public async Task Backup_StoresCiphertext_OnlyTheKeyHolderCanReplaceOrRead()
    {
        var keys = BackupCrypto.Derive(BackupCrypto.NewRecoveryKey());
        var blob = BackupCrypto.Seal("SQLite format 3"u8.ToArray(), keys.EncryptionKey);
        var url = $"/v1/backups/{keys.LookupId}";

        Assert.Equal(HttpStatusCode.NoContent, (await _http.SendAsync(Authed(HttpMethod.Put, url, keys.WriteToken, new ByteArrayContent(blob)))).StatusCode);

        var got = await _http.SendAsync(Authed(HttpMethod.Get, url, keys.WriteToken));
        Assert.Equal(HttpStatusCode.OK, got.StatusCode);
        Assert.Equal("SQLite format 3"u8.ToArray(), BackupCrypto.Open(await got.Content.ReadAsByteArrayAsync(), keys.EncryptionKey));

        var stranger = "x" + keys.WriteToken;
        Assert.Equal(HttpStatusCode.Forbidden, (await _http.SendAsync(Authed(HttpMethod.Put, url, stranger, new ByteArrayContent(new byte[] { 9 })))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await _http.SendAsync(Authed(HttpMethod.Get, url, stranger))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.SendAsync(Authed(HttpMethod.Get, "/v1/backups/not-hex", keys.WriteToken))).StatusCode);
    }
}
