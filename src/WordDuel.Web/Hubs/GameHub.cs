using Microsoft.AspNetCore.SignalR;
using WordDuel.Core.Common;
using WordDuel.Core.Contracts;
using WordDuel.Core.Functional;
using WordDuel.Core.Views;
using WordDuel.Infrastructure;

namespace WordDuel.Web.Hubs;

/// <summary>
/// The real-time boundary. Each method is a thin adapter: it passes the caller's connection id
/// (the server-side identity) and the raw input to <see cref="DuelService"/>, then converts the
/// returned Result into a serialisable <see cref="HubResponse{T}"/>. No game logic lives here.
/// </summary>
public sealed class GameHub(DuelService duel, ILogger<GameHub> log) : Hub<IGameClient>
{
    public Task<HubResponse<SessionTicket>> CreateRoom(string? nickname) =>
        Respond(() => duel.CreateRoomAsync(Context.ConnectionId, nickname));

    public Task<HubResponse<SessionTicket>> JoinRoom(string? roomCode, string? nickname) =>
        Respond(() => duel.JoinRoomAsync(Context.ConnectionId, roomCode, nickname));

    public Task<HubResponse<SessionTicket>> Resume(string? roomCode, string? token) =>
        Respond(() => duel.ResumeAsync(Context.ConnectionId, roomCode, token));

    public Task<HubResponse<PlayerView>> SubmitGuess(string? guess) =>
        Respond(() => duel.SubmitGuessAsync(Context.ConnectionId, guess));

    public Task<HubResponse<PlayerView>> Ready() =>
        Respond(() => duel.MarkReadyAsync(Context.ConnectionId));

    public Task<HubResponse<PlayerView>> Rematch() =>
        Respond(() => duel.VoteRematchAsync(Context.ConnectionId));

    public Task<HubResponse<bool>> Leave() =>
        Respond(() => duel.LeaveAsync(Context.ConnectionId));

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await duel.DisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private async Task<HubResponse<T>> Respond<T>(Func<Task<Result<T>>> operation)
    {
        try
        {
            return HubResponse<T>.From(await operation());
        }
        catch (Exception ex)
        {
            // Never leak exception details to the browser; log them on the server instead.
            log.LogError(ex, "Hub call failed for connection {Connection}", Context.ConnectionId);
            return new HubResponse<T>(false, default, GameErrors.Unexpected);
        }
    }
}

/// <summary>Adapter from the shell's IRoomNotifier contract to SignalR.</summary>
public sealed class SignalRRoomNotifier(IHubContext<GameHub, IGameClient> hub) : IRoomNotifier
{
    public Task SendAsync(string connectionId, PlayerView view) => hub.Clients.Client(connectionId).RoomUpdated(view);
}

/// <summary>
/// The server's game clock: one tick per second drives round deadlines, the break between rounds,
/// reconnection grace periods and room cleanup. The loop is imperative shell code; each tick is
/// turned into a pure <c>ClockTick</c> command for every room.
/// </summary>
public sealed class RoundClockService(DuelService duel, ILogger<RoundClockService> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await duel.TickAsync();
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Clock tick failed");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // normal shutdown
        }
    }
}
