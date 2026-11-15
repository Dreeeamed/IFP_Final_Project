using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Views;

namespace WordDuel.Core.Contracts;

// Transport contract shared by the SignalR hub (server) and the Blazor client.
// Plain C# types only — no SignalR dependency in the core.

public static class HubRoutes
{
    public const string Duel = "/hubs/duel";
}

/// <summary>Client → server method names (must match the public methods of the server's GameHub).</summary>
public static class HubMethods
{
    public const string CreateRoom = "CreateRoom";
    public const string JoinRoom = "JoinRoom";
    public const string Resume = "Resume";
    public const string SubmitGuess = "SubmitGuess";
    public const string Ready = "Ready";
    public const string Rematch = "Rematch";
    public const string Leave = "Leave";

    /// <summary>Server → client push (method of <see cref="IGameClient"/>).</summary>
    public const string RoomUpdated = nameof(IGameClient.RoomUpdated);
}

/// <summary>Messages the server pushes to a client (strongly-typed hub client).</summary>
public interface IGameClient
{
    /// <summary>A fresh, personalised snapshot of the room for this player.</summary>
    Task RoomUpdated(PlayerView view);
}

/// <summary>
/// Serialisable mirror of <see cref="Result{T}"/> for the wire. Abstract unions do not
/// serialise to JSON without extra configuration, so the boundary converts Result ⇄ HubResponse.
/// </summary>
public sealed record HubResponse<T>(bool Ok, T? Value, GameError? Error)
{
    public static HubResponse<T> From(Result<T> result) =>
        result.Match(value => new HubResponse<T>(true, value, null), error => new HubResponse<T>(false, default, error));

    public Result<T> ToResult() => Ok && Value is not null
        ? new Success<T>(Value)
        : new Failure<T>(Error ?? GameErrors.Unexpected);
}

/// <summary>What a player receives after creating, joining or resuming a room.</summary>
public sealed record SessionTicket(string RoomCode, string PlayerId, string Token, PlayerView View);
