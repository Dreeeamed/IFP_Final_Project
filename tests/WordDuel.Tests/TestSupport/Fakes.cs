using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;
using WordDuel.Core.Common;
using WordDuel.Core.Players;
using WordDuel.Core.Views;
using WordDuel.Core.Words;
using WordDuel.Infrastructure;
using WordDuel.Infrastructure.Persistence;
using WordDuel.Infrastructure.Rooms;

namespace WordDuel.Tests.TestSupport;

/// <summary>A clock the test moves by hand (Lecture 10: "inject the clock").</summary>
public sealed class FakeClock : IClock
{
    public DateTimeOffset UtcNow { get; private set; } = TestData.T0;

    public void Advance(TimeSpan by) => UtcNow += by;
}

/// <summary>Always returns the configured secret, so tests know the answer.</summary>
public sealed class FixedSecretPicker(string secret = "CRANE") : ISecretWordPicker
{
    public string Secret { get; set; } = secret;

    public string Pick(WordDictionary dictionary, IReadOnlySet<string> exclude) => Secret;
}

/// <summary>Predictable codes and tokens.</summary>
public sealed class SequentialIds : IIdentityGenerator
{
    private int _room;
    private int _player;
    private int _token;

    // Only characters from the real alphabet, so the codes pass RoomCode.Normalize.
    public string NewRoomCode()
    {
        var n = _room++;
        return $"RMKP{RoomCode.Alphabet[n / RoomCode.Alphabet.Length % RoomCode.Alphabet.Length]}{RoomCode.Alphabet[n % RoomCode.Alphabet.Length]}";
    }

    public PlayerId NewPlayerId() => new($"p{++_player}");

    public string NewToken() => $"token-{++_token}-secret";
}

/// <summary>Records every view pushed to every connection.</summary>
public sealed class RecordingNotifier : IRoomNotifier
{
    public ConcurrentQueue<(string Connection, PlayerView View)> Sent { get; } = new();

    public Task SendAsync(string connectionId, PlayerView view)
    {
        Sent.Enqueue((connectionId, view));
        return Task.CompletedTask;
    }

    public PlayerView? LastFor(string connectionId) =>
        Sent.Where(s => s.Connection == connectionId).Select(s => s.View).LastOrDefault();
}

/// <summary>A DuelService wired with deterministic fakes.</summary>
public sealed class ServiceHarness
{
    public FakeClock Clock { get; } = new();
    public FixedSecretPicker Secrets { get; } = new();
    public RecordingNotifier Notifier { get; } = new();
    public InMemoryMatchHistory History { get; } = new();
    public RoomRegistry Rooms { get; } = new();
    public DuelOptions Options { get; } = new() { MinActionIntervalMs = 200, ReconnectGraceSeconds = 60 };
    public DuelService Service { get; }

    public ServiceHarness(Action<DuelOptions>? configure = null)
    {
        configure?.Invoke(Options);
        Service = new DuelService(Rooms, TestData.Dictionary, Clock, Secrets, new SequentialIds(), Notifier, History,
            Microsoft.Extensions.Options.Options.Create(Options), NullLogger<DuelService>.Instance);
    }

    /// <summary>Moves time forward past the anti-spam interval.</summary>
    public void Wait(double seconds = 1) => Clock.Advance(TimeSpan.FromSeconds(seconds));
}
