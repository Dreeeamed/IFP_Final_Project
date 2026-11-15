using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using WordDuel.Core.Analysis;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Views;

namespace WordDuel.Infrastructure.Rooms;

/// <summary>A player's seat: identity, secret token and connection status (shell-only data).</summary>
public sealed class Seat(PlayerInfo player, string token)
{
    public PlayerInfo Player { get; } = player;
    public string Token { get; } = token;
    public string? ConnectionId { get; set; }
    public Presence Presence { get; set; } = Presence.Connected;
    public DateTimeOffset? DisconnectedAt { get; set; }
    public DateTimeOffset LastActionAt { get; set; } = DateTimeOffset.MinValue;

    /// <summary>Constant-time comparison so response timing does not leak how much of a token matched.</summary>
    public bool HasToken(string? candidate) =>
        candidate is not null &&
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(Token), Encoding.UTF8.GetBytes(candidate));
}

/// <summary>
/// The imperative shell around one match. It holds the ONLY mutable reference to the room's
/// immutable <see cref="MatchState"/>; every change goes through <see cref="SetState"/> while the
/// room's <see cref="Gate"/> is held, so commands for one room are processed one at a time.
/// </summary>
public sealed class GameRoom(string code, MatchState initial, DateTimeOffset createdAt)
{
    private readonly List<Seat> _seats = [];

    public string Code { get; } = code;

    /// <summary>Serialises all access to this room (async-friendly lock).</summary>
    public SemaphoreSlim Gate { get; } = new(1, 1);

    public MatchState State { get; private set; } = initial;

    public ImmutableDictionary<int, RoundAnalysis> Analyses { get; set; } = ImmutableDictionary<int, RoundAnalysis>.Empty;

    public DateTimeOffset CreatedAt { get; } = createdAt;

    public DateTimeOffset LastActivity { get; set; } = createdAt;

    public bool Closed { get; set; }

    /// <summary>Incremented on every observable change; used to decide whether to notify clients.</summary>
    public int Version { get; private set; }

    public IReadOnlyList<Seat> Seats => _seats;

    public void SetState(MatchState next, DateTimeOffset now)
    {
        if (ReferenceEquals(next, State)) return;
        State = next;
        LastActivity = now;
        Version++;
    }

    public void Touch() => Version++;

    public void AddSeat(Seat seat)
    {
        _seats.Add(seat);
        Version++;
    }

    public Maybe<Seat> SeatOf(PlayerId player) => _seats.FirstOrNothing(seat => seat.Player.Id == player);

    public Maybe<Seat> SeatWithToken(string? token) => _seats.FirstOrNothing(seat => seat.HasToken(token));

    public IReadOnlyDictionary<PlayerId, Presence> Presence() =>
        _seats.ToDictionary(seat => seat.Player.Id, seat => seat.Presence);
}
