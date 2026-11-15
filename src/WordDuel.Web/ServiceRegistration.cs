using Microsoft.EntityFrameworkCore;
using WordDuel.Core.Words;
using WordDuel.Infrastructure;
using WordDuel.Infrastructure.Persistence;
using WordDuel.Infrastructure.Rooms;
using WordDuel.Web.Hubs;

namespace WordDuel.Web;

/// <summary>Composition root: wires the functional core and the imperative shell together.</summary>
public static class ServiceRegistration
{
    public static IServiceCollection AddWordDuelServer(this IServiceCollection services, IConfiguration configuration, string contentRoot)
    {
        services.Configure<DuelOptions>(configuration.GetSection(DuelOptions.Section));

        // Functional core data: immutable, loaded once, shared by every request.
        services.AddSingleton(_ => EmbeddedWordLists.Load());

        // Imperative shell services.
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<ISecretWordPicker, RandomSecretWordPicker>();
        services.AddSingleton<IIdentityGenerator, SecureIdentityGenerator>();
        services.AddSingleton<RoomRegistry>();
        services.AddSingleton<DuelService>();
        services.AddSingleton<IRoomNotifier, SignalRRoomNotifier>();
        services.AddHostedService<RoundClockService>();

        // Match history in a local SQLite file (created on first start).
        var dataDirectory = Path.Combine(contentRoot, "App_Data");
        Directory.CreateDirectory(dataDirectory);
        var connectionString = configuration.GetConnectionString("History") ?? $"Data Source={Path.Combine(dataDirectory, "wordduel.db")}";
        services.AddDbContextFactory<MatchHistoryDbContext>(options => options.UseSqlite(connectionString));
        services.AddSingleton<IMatchHistory, SqliteMatchHistory>();

        return services;
    }

    public static async Task EnsureHistoryDatabaseAsync(this WebApplication app)
    {
        try
        {
            var factory = app.Services.GetRequiredService<IDbContextFactory<MatchHistoryDbContext>>();
            await using var db = await factory.CreateDbContextAsync();
            await db.Database.EnsureCreatedAsync();
        }
        catch (Exception ex)
        {
            app.Logger.LogWarning(ex, "Match history database unavailable; the game still works without it");
        }
    }
}
