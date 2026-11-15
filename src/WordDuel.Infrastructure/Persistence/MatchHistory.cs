using Microsoft.EntityFrameworkCore;
using WordDuel.Core.Match;

namespace WordDuel.Infrastructure.Persistence;

/// <summary>Stores finished matches. The game itself never depends on this succeeding.</summary>
public interface IMatchHistory
{
    Task SaveAsync(FinishedMatch match, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FinishedMatch>> RecentAsync(int count, CancellationToken cancellationToken = default);
}

/// <summary>EF Core row. Only public information is stored: nicknames and scores.</summary>
public sealed class MatchRecord
{
    public int Id { get; set; }
    public DateTime FinishedAtUtc { get; set; }
    public string FirstNickname { get; set; } = "";
    public string SecondNickname { get; set; } = "";
    public int FirstWins { get; set; }
    public int SecondWins { get; set; }
    public int Draws { get; set; }
    public string? WinnerNickname { get; set; }
    public string EndReason { get; set; } = "";

    public static MatchRecord From(FinishedMatch match) => new()
    {
        FinishedAtUtc = match.FinishedAtUtc,
        FirstNickname = match.FirstNickname,
        SecondNickname = match.SecondNickname,
        FirstWins = match.FirstWins,
        SecondWins = match.SecondWins,
        Draws = match.Draws,
        WinnerNickname = match.WinnerNickname,
        EndReason = match.EndReason
    };

    public FinishedMatch ToDomain() =>
        new(FinishedAtUtc, FirstNickname, SecondNickname, FirstWins, SecondWins, Draws, WinnerNickname, EndReason);
}

public sealed class MatchHistoryDbContext(DbContextOptions<MatchHistoryDbContext> options) : DbContext(options)
{
    public DbSet<MatchRecord> Matches => Set<MatchRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var match = modelBuilder.Entity<MatchRecord>();
        match.ToTable("Matches");
        match.Property(m => m.FirstNickname).HasMaxLength(32);
        match.Property(m => m.SecondNickname).HasMaxLength(32);
        match.Property(m => m.WinnerNickname).HasMaxLength(32);
        match.Property(m => m.EndReason).HasMaxLength(32);
        match.HasIndex(m => m.FinishedAtUtc);
    }
}

/// <summary>SQLite implementation. A short-lived DbContext per operation keeps it thread-safe.</summary>
public sealed class SqliteMatchHistory(IDbContextFactory<MatchHistoryDbContext> factory) : IMatchHistory
{
    public async Task SaveAsync(FinishedMatch match, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        db.Matches.Add(MatchRecord.From(match));
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FinishedMatch>> RecentAsync(int count, CancellationToken cancellationToken = default)
    {
        await using var db = await factory.CreateDbContextAsync(cancellationToken);
        var rows = await db.Matches.AsNoTracking()
            .OrderByDescending(m => m.FinishedAtUtc)
            .Take(count)
            .ToListAsync(cancellationToken);
        return rows.Select(row => row.ToDomain()).ToList();
    }
}

/// <summary>In-memory implementation used by tests (and usable when no database is wanted).</summary>
public sealed class InMemoryMatchHistory : IMatchHistory
{
    private readonly List<FinishedMatch> _matches = [];
    private readonly Lock _lock = new();

    public Task SaveAsync(FinishedMatch match, CancellationToken cancellationToken = default)
    {
        lock (_lock) _matches.Add(match);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<FinishedMatch>> RecentAsync(int count, CancellationToken cancellationToken = default)
    {
        lock (_lock)
            return Task.FromResult<IReadOnlyList<FinishedMatch>>(_matches.OrderByDescending(m => m.FinishedAtUtc).Take(count).ToList());
    }
}
