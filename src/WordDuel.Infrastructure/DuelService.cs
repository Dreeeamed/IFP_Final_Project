using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using WordDuel.Core.Analysis;
using WordDuel.Core.Common;
using WordDuel.Core.Contracts;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Views;
using WordDuel.Core.Words;
using WordDuel.Infrastructure.Persistence;
using WordDuel.Infrastructure.Rooms;

namespace WordDuel.Infrastructure;

/// <summary>Which seat a SignalR connection currently controls.</summary>
public sealed record SeatRef(string RoomCode, PlayerId Player);

/// <summary>
/// Server-side orchestration — the imperative shell around the functional core.
///
/// Responsibilities that are impure by nature live here: secure randomness, the clock, the
/// connection → seat map, per-room locking, persistence, logging and pushing views to clients.
/// Every game decision is delegated to pure functions (validator, MatchEngine, analyzer, projection).
///
/// Security model: a player's identity comes from the SignalR connection that created, joined or
/// resumed the seat. The client never sends a player id, score, timestamp or feedback. A room code
/// alone grants nothing: resuming a seat requires its 256-bit token.
/// </summary>
public sealed class DuelService
{
    private readonly RoomRegistry _rooms;
    private readonly ConcurrentDictionary<string, SeatRef> _connections = new(StringComparer.Ordinal);
    private readonly WordDictionary _dictionary;
    private readonly Func<string?, Result<ValidGuess>> _validateGuess;
    private readonly IClock _clock;
    private readonly ISecretWordPicker _secrets;
    private readonly IIdentityGenerator _ids;
    private readonly IRoomNotifier _notifier;
    private readonly IMatchHistory _history;
    private readonly DuelOptions _options;
    private readonly MatchRules _rules;
    private readonly ILogger<DuelService> _log;

    public DuelService(
        RoomRegistry rooms,
        WordDictionary dictionary,
        IClock clock,
        ISecretWordPicker secrets,
        IIdentityGenerator ids,
        IRoomNotifier notifier,
        IMatchHistory history,
        IOptions<DuelOptions> options,
        ILogger<DuelService> log)
    {
        _rooms = rooms;
        _dictionary = dictionary;
        _clock = clock;
        _secrets = secrets;
        _ids = ids;
        _notifier = notifier;
        _history = history;
        _options = options.Value;
        _rules = _options.ToRules();
        _log = log;
        // Partial application: the rule list is fixed once, leaving only the raw guess as input.
        _validateGuess = GuessValidator.For(dictionary);
    }

    public int ActiveRooms => _rooms.Count;

    // ── Player commands ─────────────────────────────────────────────────────

    public async Task<Result<SessionTicket>> CreateRoomAsync(string connectionId, string? nickname)
    {
        await DetachAsync(connectionId);
        return await Nickname.Validate(nickname)
            .BindAsync(name => CreateRoomForAsync(connectionId, name));
    }

    public async Task<Result<SessionTicket>> JoinRoomAsync(string connectionId, string? roomCode, string? nickname)
    {
        await DetachAsync(connectionId);
        return await RoomCode.Normalize(roomCode)
            .Bind(code => Nickname.Validate(nickname).Map(name => (code, name)))
            .BindAsync(input => WithRoomAsync(input.code, room => JoinAsOpponent(room, input.name, connectionId)));
    }

    /// <summary>Re-attaches a browser to its seat after a refresh or reconnect; requires the seat token.</summary>
    public async Task<Result<SessionTicket>> ResumeAsync(string connectionId, string? roomCode, string? token)
    {
        var target = RoomCode.Normalize(roomCode);
        if (target is Success<string> code && _connections.TryGetValue(connectionId, out var current) && current.RoomCode != code.Value)
            await DetachAsync(connectionId);

        return await target.BindAsync(code => WithRoomAsync(code, room =>
            room.SeatWithToken(token)
                .ToResult(GameErrors.InvalidToken)
                .Ensure(seat => seat.Presence != Presence.Left, _ => GameErrors.InvalidToken)
                .Map(seat => Rebind(room, seat, connectionId))));
    }

    /// <summary>
    /// The guess pipeline (railway-oriented, Lecture 6): identity → throttle → validation →
    /// pure state transition → personalised view. Any failure skips the remaining steps.
    /// </summary>
    public Task<Result<PlayerView>> SubmitGuessAsync(string connectionId, string? guess) =>
        SeatOf(connectionId).BindAsync(seat => WithRoomAsync(seat.RoomCode, room =>
            Throttle(room, seat.Player)
                .Bind(_ => _validateGuess(guess))
                .Bind(valid => Transition(room, new SubmitGuess(seat.Player, valid)))
                .Map(_ => ViewFor(room, seat.Player))));

    public Task<Result<PlayerView>> MarkReadyAsync(string connectionId) =>
        Act(connectionId, player => new MarkReady(player));

    public Task<Result<PlayerView>> VoteRematchAsync(string connectionId) =>
        SeatOf(connectionId).BindAsync(seat => WithRoomAsync(seat.RoomCode, room =>
            Throttle(room, seat.Player)
                .Ensure(_ => room.Seats.All(s => s.Presence == Presence.Connected), _ => GameErrors.InvalidAction("start a rematch while your opponent is away"))
                .Bind(_ => Transition(room, new VoteRematch(seat.Player)))
                .Map(_ => ViewFor(room, seat.Player))));

    /// <summary>Explicit "leave room": forfeits a running match.</summary>
    public async Task<Result<bool>> LeaveAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var seatRef)) return Result.Ok(true);
        return await WithRoomAsync(seatRef.RoomCode, room => Leave(room, seatRef.Player));
    }

    /// <summary>Called when a SignalR connection drops. The seat waits for the grace period.</summary>
    public async Task DisconnectedAsync(string connectionId)
    {
        if (!_connections.TryRemove(connectionId, out var seatRef)) return;
        await WithRoomAsync(seatRef.RoomCode, room => room.SeatOf(seatRef.Player)
            .ToResult(GameErrors.NotInRoom)
            .Map(seat =>
            {
                if (seat.ConnectionId != connectionId) return false; // already resumed elsewhere
                seat.ConnectionId = null;
                seat.Presence = Presence.Disconnected;
                seat.DisconnectedAt = _clock.UtcNow;
                room.Touch();
                return true;
            }));
    }

    /// <summary>
    /// Called once per second by the background clock: expires rounds, starts the next round after
    /// the break, forfeits players who did not come back, and removes abandoned or idle rooms.
    /// </summary>
    public async Task TickAsync()
    {
        foreach (var room in _rooms.Snapshot())
            await WithRoomAsync(room, Maintain);
    }

    // ── Room operations (always called while holding the room's gate) ──────────

    private async Task<Result<SessionTicket>> CreateRoomForAsync(string connectionId, string nickname)
    {
        var now = _clock.UtcNow;
        var host = new PlayerInfo(_ids.NewPlayerId(), nickname);
        return await _rooms.Create(_ids.NewRoomCode, code => new GameRoom(code, MatchEngine.Create(host, _rules), now), _options.MaxRooms)
            .OnSuccess(room => _log.LogInformation("Room {Code} created", room.Code))
            .BindAsync(room => WithRoomAsync(room, r => TakeSeat(r, host, connectionId)));
    }

    private Result<SessionTicket> JoinAsOpponent(GameRoom room, string nickname, string connectionId)
    {
        if (room.Seats.Count >= 2) return Result.Fail<SessionTicket>(GameErrors.RoomFull);
        var opponent = new PlayerInfo(_ids.NewPlayerId(), nickname);
        return Transition(room, new OpponentJoins(opponent))
            .Bind(_ => TakeSeat(room, opponent, connectionId));
    }

    private Result<SessionTicket> TakeSeat(GameRoom room, PlayerInfo player, string connectionId)
    {
        var seat = new Seat(player, _ids.NewToken()) { ConnectionId = connectionId };
        room.AddSeat(seat);
        _connections[connectionId] = new SeatRef(room.Code, player.Id);
        return Result.Ok(Ticket(room, seat));
    }

    private SessionTicket Rebind(GameRoom room, Seat seat, string connectionId)
    {
        if (seat.ConnectionId is { } previous && previous != connectionId)
            _connections.TryRemove(previous, out _);

        seat.ConnectionId = connectionId;
        seat.Presence = Presence.Connected;
        seat.DisconnectedAt = null;
        _connections[connectionId] = new SeatRef(room.Code, seat.Player.Id);
        room.Touch();
        return Ticket(room, seat);
    }

    private Result<bool> Leave(GameRoom room, PlayerId player)
    {
        room.SeatOf(player).OnSomething(seat =>
        {
            seat.Presence = Presence.Left;
            seat.ConnectionId = null;
            room.Touch();
        });

        if (room.State is RoundInProgress or BetweenRounds)
            Transition(room, new Forfeit(player));

        if (room.State is WaitingForOpponent || room.Seats.All(s => s.Presence == Presence.Left))
            Close(room, "everyone left");

        return Result.Ok(true);
    }

    private Result<bool> Maintain(GameRoom room)
    {
        var now = _clock.UtcNow;
        Transition(room, ClockTick.Instance);

        var grace = TimeSpan.FromSeconds(_options.ReconnectGraceSeconds);
        foreach (var seat in room.Seats.Where(s => s.Presence == Presence.Disconnected && now - s.DisconnectedAt >= grace))
        {
            seat.Presence = Presence.Left;
            room.Touch();
            if (room.State is RoundInProgress or BetweenRounds)
                Transition(room, new Forfeit(seat.Player.Id));
            _log.LogInformation("Room {Code}: {Player} did not reconnect in time", room.Code, seat.Player.Nickname);
        }

        if (room.Seats.All(s => s.Presence == Presence.Left))
            Close(room, "abandoned");
        else if (now - room.LastActivity > TimeSpan.FromMinutes(_options.IdleRoomMinutes))
            Close(room, "idle");

        return Result.Ok(true);
    }

    private void Close(GameRoom room, string reason)
    {
        foreach (var seat in room.Seats.Where(s => s.ConnectionId is not null))
            _connections.TryRemove(seat.ConnectionId!, out _);
        _rooms.Remove(room);
        _log.LogInformation("Room {Code} closed ({Reason})", room.Code, reason);
    }

    private Task<Result<PlayerView>> Act(string connectionId, Func<PlayerId, MatchCommand> command) =>
        SeatOf(connectionId).BindAsync(seat => WithRoomAsync(seat.RoomCode, room =>
            Throttle(room, seat.Player)
                .Bind(_ => Transition(room, command(seat.Player)))
                .Map(_ => ViewFor(room, seat.Player))));

    /// <summary>The single point where the shell applies the pure engine and stores the new state.</summary>
    private Result<MatchState> Transition(GameRoom room, MatchCommand command)
    {
        var now = _clock.UtcNow;
        var context = new TransitionContext(now, NextSecretFor(room.State));
        return MatchEngine.Apply(room.State, command, context)
            .OnSuccess(next => room.SetState(next, now))
            .OnFailure(error => _log.LogDebug("Room {Code}: {Command} refused ({Error})", room.Code, command.GetType().Name, error.Code));
    }

    /// <summary>A new round can only start outside an active round, so only then is a secret drawn.</summary>
    private string NextSecretFor(MatchState state) =>
        state is RoundInProgress playing
            ? playing.Round.Secret
            : _secrets.Pick(_dictionary, MatchEngine.UsedSecrets(state));

    private Result<bool> Throttle(GameRoom room, PlayerId player)
    {
        var now = _clock.UtcNow;
        return room.SeatOf(player)
            .ToResult(GameErrors.NotInRoom)
            .Ensure(seat => now - seat.LastActionAt >= TimeSpan.FromMilliseconds(_options.MinActionIntervalMs), _ => GameErrors.TooFast)
            .Map(seat =>
            {
                seat.LastActionAt = now;
                return true;
            });
    }

    private Result<SeatRef> SeatOf(string connectionId) =>
        ((IReadOnlyDictionary<string, SeatRef>)_connections).Find(connectionId).ToResult(GameErrors.NotInRoom);

    private async Task DetachAsync(string connectionId)
    {
        if (_connections.ContainsKey(connectionId))
            await DisconnectedAsync(connectionId);
    }

    private SessionTicket Ticket(GameRoom room, Seat seat) =>
        new(room.Code, seat.Player.Id.Value, seat.Token, ViewFor(room, seat.Player.Id));

    private PlayerView ViewFor(GameRoom room, PlayerId player) =>
        PlayerViewProjection.Project(room.State, player, new ProjectionContext(room.Code, _clock.UtcNow, room.Presence(), room.Analyses));

    // ── Locking, side effects and notifications ──────────────────────────────

    private Task<Result<T>> WithRoomAsync<T>(string code, Func<GameRoom, Result<T>> action) =>
        _rooms.Find(code).ToResult(GameErrors.RoomNotFound).BindAsync(room => WithRoomAsync(room, action));

    /// <summary>
    /// Runs <paramref name="action"/> under the room's lock. If the room changed, the shell then
    /// performs the side effects: analysis of finished rounds, history persistence and pushing a
    /// fresh personalised view to every connected player.
    /// </summary>
    private async Task<Result<T>> WithRoomAsync<T>(GameRoom room, Func<GameRoom, Result<T>> action)
    {
        await room.Gate.WaitAsync();
        try
        {
            if (room.Closed) return Result.Fail<T>(GameErrors.RoomNotFound);

            var version = room.Version;
            var before = room.State;
            var result = action(room);
            if (room.Version != version)
                await PublishAsync(room, before);
            return result;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "Unexpected error in room {Code}", room.Code);
            return Result.Fail<T>(GameErrors.Unexpected);
        }
        finally
        {
            room.Gate.Release();
        }
    }

    private async Task PublishAsync(GameRoom room, MatchState before)
    {
        UpdateAnalyses(room);

        if (room.State is MatchOver over && before is not MatchOver)
            await SaveHistoryAsync(over);

        if (room.Closed) return;
        foreach (var seat in room.Seats.Where(s => s is { ConnectionId: not null, Presence: Presence.Connected }))
        {
            try
            {
                await _notifier.SendAsync(seat.ConnectionId!, ViewFor(room, seat.Player.Id));
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Could not notify {Player} in room {Code}", seat.Player.Nickname, room.Code);
            }
        }
    }

    /// <summary>Analyses every finished round once and caches the result for the reveal screens.</summary>
    private void UpdateAnalyses(GameRoom room)
    {
        var duel = room.State switch
        {
            RoundInProgress playing => playing.Duel,
            BetweenRounds pause => pause.Duel,
            MatchOver over => over.Duel,
            _ => null
        };
        if (duel is null) return;
        if (duel.History.Count == 0)
        {
            room.Analyses = room.Analyses.Clear(); // a rematch started a new match
            return;
        }

        foreach (var round in duel.History.Where(r => !room.Analyses.ContainsKey(r.Number)))
            room.Analyses = room.Analyses.SetItem(round.Number, StrategyAnalyzer.AnalyzeRound(_dictionary, duel, round));
    }

    private async Task SaveHistoryAsync(MatchOver over)
    {
        var duel = over.Duel;
        var winner = over.Result is MatchWon won
            ? (won.Winner == duel.First.Id ? duel.First.Nickname : duel.Second.Nickname)
            : null;
        var reason = over.Result switch
        {
            MatchWon w => w.Reason.ToString(),
            _ => "Draw"
        };
        var record = new FinishedMatch(_clock.UtcNow.UtcDateTime, duel.First.Nickname, duel.Second.Nickname,
            duel.WinsOf(duel.First.Id), duel.WinsOf(duel.Second.Id), duel.Draws, winner, reason);

        try
        {
            await _history.SaveAsync(record);
        }
        catch (Exception ex)
        {
            // History is a nice-to-have: a database problem must never break a live match.
            _log.LogWarning(ex, "Could not save match history");
        }
    }
}
