using System.Collections.Concurrent;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;

namespace WordDuel.Infrastructure.Rooms;

/// <summary>All live rooms, indexed by code. Thread-safe.</summary>
public sealed class RoomRegistry
{
    private const int MaxCodeAttempts = 25;

    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new(StringComparer.Ordinal);

    public int Count => _rooms.Count;

    public IReadOnlyCollection<GameRoom> Snapshot() => _rooms.Values.ToArray();

    public Maybe<GameRoom> Find(string code) =>
        _rooms.TryGetValue(code, out var room) && !room.Closed ? new Something<GameRoom>(room) : new Nothing<GameRoom>();

    /// <summary>
    /// Lecture 7 — an indefinite loop expressed as a lazy sequence: generate random codes,
    /// stop at the first one we manage to claim, and give up after a fixed number of attempts
    /// instead of looping forever. TryAdd is the atomic "claim" step of the imperative shell.
    /// </summary>
    public Result<GameRoom> Create(Func<string> nextCode, Func<string, GameRoom> createRoom, int maxRooms) =>
        _rooms.Count >= maxRooms
            ? Result.Fail<GameRoom>(GameErrors.ServerFull)
            : Iteration.Generate(nextCode)
                .Take(MaxCodeAttempts)
                .Select(createRoom)
                .FirstOrNothing(room => _rooms.TryAdd(room.Code, room))
                .ToResult(GameErrors.ServerFull);

    public void Remove(GameRoom room)
    {
        room.Closed = true;
        _rooms.TryRemove(room.Code, out _);
    }
}
