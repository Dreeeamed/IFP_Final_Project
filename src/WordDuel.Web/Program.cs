using WordDuel.Core.Contracts;
using WordDuel.Core.Match;
using WordDuel.Infrastructure;
using WordDuel.Infrastructure.Persistence;
using WordDuel.Web;
using WordDuel.Web.Components;
using WordDuel.Web.Hubs;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 16 * 1024; // clients only ever send short strings
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

builder.Services.AddWordDuelServer(builder.Configuration, builder.Environment.ContentRootPath);

var app = builder.Build();

await app.EnsureHistoryDatabaseAsync();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Basic hardening headers for every response.
app.Use(async (context, next) =>
{
    context.Response.Headers.XContentTypeOptions = "nosniff";
    context.Response.Headers.XFrameOptions = "DENY";
    context.Response.Headers["Referrer-Policy"] = "no-referrer";
    await next();
});

app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();

app.MapHub<GameHub>(HubRoutes.Duel);

// Public, read-only endpoints. They expose nothing about live rooms, secrets or tokens.
app.MapGet("/health", (DuelService duel) => Results.Ok(new { status = "ok", activeRooms = duel.ActiveRooms }));

app.MapGet("/api/history", async (IMatchHistory history, CancellationToken cancellationToken) =>
{
    var recent = await history.RecentAsync(200, cancellationToken);
    return Results.Ok(new
    {
        recent = recent.Take(15),
        leaderboard = Leaderboard.Compute(recent, top: 10)
    });
});

app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(WordDuel.Web.Client._Imports).Assembly);

app.Run();

/// <summary>Exposed for WebApplicationFactory-based integration tests.</summary>
public partial class Program;
