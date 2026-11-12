using System.Text.RegularExpressions;
using System.Threading.RateLimiting;

using Forge.Guild;
using Forge.Server;

using Microsoft.AspNetCore.Http.Json;

var builder = WebApplication.CreateBuilder(args);

var dataDir = builder.Configuration["Forge:DataDir"] ?? Path.Combine(AppContext.BaseDirectory, "data");
builder.Services.AddSingleton(new GuildStore(dataDir));
builder.Services.Configure<JsonOptions>(o => o.SerializerOptions.PropertyNameCaseInsensitive = true);
builder.WebHost.ConfigureKestrel(k => k.Limits.MaxRequestBodySize = GuildLimits.MaxBackupBytes + 4096);

// Funnel is a reverse proxy, so every request arrives from localhost; partition by token (or
// backup id) instead. Generous for two to six phones syncing on foreground, tight for a scraper.
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(ctx =>
        RateLimitPartition.GetFixedWindowLimiter(
            ctx.Request.Headers.Authorization.ToString() is { Length: > 0 } auth ? auth : ctx.Request.Path.ToString(),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 60, Window = TimeSpan.FromMinutes(1) }));
});

var app = builder.Build();
app.UseRateLimiter();

var store = app.Services.GetRequiredService<GuildStore>();

static string? Bearer(HttpRequest r)
    => r.Headers.Authorization.ToString() is var h && h.StartsWith("Bearer ", StringComparison.Ordinal) ? h[7..].Trim() : null;

var lookupPattern = new Regex("^[0-9a-f]{64}$", RegexOptions.Compiled);

app.MapGet("/health", () => Results.Text("ok"));

var v1 = app.MapGroup("/v1");

v1.MapPost("/guilds", (CreateGuildRequest req) =>
{
    var name = req.GuildName?.Trim() ?? "";
    if (name.Length is 0 or > GuildLimits.MaxGuildNameLength) return Results.BadRequest(new { error = "guild name" });
    if (GuildLimits.Validate(req.Member) is { } bad) return Results.BadRequest(new { error = bad });
    return Results.Ok(store.Create(name, req.Member));
});

v1.MapPost("/guilds/join", (JoinGuildRequest req) =>
{
    if (GuildLimits.Validate(req.Member) is { } bad) return Results.BadRequest(new { error = bad });
    try
    {
        return store.Join(req.InviteCode ?? "", req.Member) is { } joined
            ? Results.Ok(joined)
            : Results.NotFound(new { error = "no guild with that code" });
    }
    catch (InvalidOperationException) { return Results.Conflict(new { error = "guild is full" }); }
});

v1.MapPut("/me", (HttpRequest http, MemberStatus status) =>
{
    if (store.Authenticate(Bearer(http)) is not { } who) return Results.Unauthorized();
    if (GuildLimits.Validate(status) is { } bad) return Results.BadRequest(new { error = bad });
    store.UpdateStatus(who.MemberId, status);
    return Results.NoContent();
});

v1.MapGet("/guild", (HttpRequest http) =>
    store.Authenticate(Bearer(http)) is { } who && store.GetGuild(who.GuildId) is { } guild
        ? Results.Ok(guild)
        : Results.Unauthorized());

v1.MapDelete("/me", (HttpRequest http) =>
{
    if (store.Authenticate(Bearer(http)) is not { } who) return Results.Unauthorized();
    store.Leave(who.MemberId, who.GuildId);
    return Results.NoContent();
});

// Encrypted backups. The id and token both derive from a recovery key the server never sees.
v1.MapPut("/backups/{id}", async (string id, HttpRequest http) =>
{
    if (!lookupPattern.IsMatch(id) || Bearer(http) is not { Length: >= 20 } token) return Results.BadRequest();
    using var ms = new MemoryStream();
    await http.Body.CopyToAsync(ms);
    if (ms.Length is 0 or > GuildLimits.MaxBackupBytes) return Results.BadRequest(new { error = "size" });
    return store.PutBackup(id, token, ms.ToArray()) == GuildStore.BackupWrite.Saved ? Results.NoContent() : Results.StatusCode(403);
});

v1.MapGet("/backups/{id}", (string id, HttpRequest http) =>
{
    if (!lookupPattern.IsMatch(id) || Bearer(http) is not { } token) return Results.BadRequest();
    if (store.GetBackup(id, token) is not { } backup) return Results.NotFound();
    http.HttpContext.Response.Headers["X-Backup-Updated"] = backup.UpdatedUtc.ToString("O");
    return Results.Bytes(backup.Blob, "application/octet-stream");
});

v1.MapDelete("/backups/{id}", (string id, HttpRequest http) =>
    lookupPattern.IsMatch(id) && Bearer(http) is { } token && store.DeleteBackup(id, token)
        ? Results.NoContent()
        : Results.NotFound());

app.Run();

/// <summary>Visible to the integration tests (WebApplicationFactory).</summary>
public partial class Program;
