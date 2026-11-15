using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.SignalR.Client;
using WordDuel.Core.Common;
using WordDuel.Core.Contracts;
using WordDuel.Core.Functional;
using WordDuel.Core.Views;

namespace WordDuel.Web.Client.Services;

public enum LinkStatus
{
    Offline,
    Connecting,
    Online,
    Reconnecting
}

/// <summary>What a tab remembers about its seat (kept in sessionStorage, so each tab is its own player).</summary>
public sealed record StoredSession(string RoomCode, string PlayerId, string Token);

/// <summary>
/// Client-side state container for the duel. It owns the SignalR connection, keeps the latest
/// server snapshot (<see cref="View"/>) and notifies components through <see cref="Changed"/>.
///
/// The client is deliberately "dumb": it never computes feedback, scores or winners. It renders
/// the latest PlayerView the server sent and forwards key presses.
/// </summary>
public sealed class DuelConnection(NavigationManager navigation, BrowserInterop browser) : IAsyncDisposable
{
    private const string SessionKey = "wordduel.session";
    private readonly SemaphoreSlim _connectGate = new(1, 1);
    private HubConnection? _hub;

    public PlayerView? View { get; private set; }

    public StoredSession? Session { get; private set; }

    public LinkStatus Status { get; private set; } = LinkStatus.Offline;

    /// <summary>Set when an automatic resume after a reconnect failed (e.g. the room expired).</summary>
    public GameError? SessionError { get; private set; }

    /// <summary>Server clock minus local clock, refreshed with every snapshot; used by countdowns.</summary>
    public TimeSpan ServerOffset { get; private set; }

    public DateTimeOffset ServerNow => DateTimeOffset.UtcNow + ServerOffset;

    public event Action? Changed;

    // ── Session commands ────────────────────────────────────────────────────

    public Task<Result<SessionTicket>> CreateRoomAsync(string nickname) =>
        StartSessionAsync(HubMethods.CreateRoom, nickname);

    public Task<Result<SessionTicket>> JoinRoomAsync(string roomCode, string nickname) =>
        StartSessionAsync(HubMethods.JoinRoom, roomCode, nickname);

    /// <summary>Re-attaches this tab to its seat using the token remembered in sessionStorage.</summary>
    public async Task<Result<SessionTicket>> ResumeAsync(string roomCode)
    {
        var stored = Session ?? await LoadSessionAsync();
        return stored is not null && string.Equals(stored.RoomCode, roomCode, StringComparison.OrdinalIgnoreCase)
            ? await StartSessionAsync(HubMethods.Resume, stored.RoomCode, stored.Token)
            : Result.Fail<SessionTicket>(GameErrors.InvalidToken);
    }

    public async Task LeaveAsync()
    {
        if (_hub?.State == HubConnectionState.Connected)
            await InvokeAsync<bool>(HubMethods.Leave);
        await ForgetSessionAsync();
    }

    // ── Game commands: the server answers with a fresh view ──────────────────

    public async Task<Result<PlayerView>> SubmitGuessAsync(string guess) =>
        (await InvokeAsync<PlayerView>(HubMethods.SubmitGuess, guess)).OnSuccess(Apply);

    public async Task<Result<PlayerView>> ReadyAsync() =>
        (await InvokeAsync<PlayerView>(HubMethods.Ready)).OnSuccess(Apply);

    public async Task<Result<PlayerView>> RematchAsync() =>
        (await InvokeAsync<PlayerView>(HubMethods.Rematch)).OnSuccess(Apply);

    // ── Connection management ────────────────────────────────────────────────

    public async Task<bool> EnsureConnectedAsync()
    {
        if (_hub?.State == HubConnectionState.Connected) return true;

        await _connectGate.WaitAsync();
        try
        {
            _hub ??= BuildConnection();
            if (_hub.State == HubConnectionState.Disconnected)
            {
                SetStatus(LinkStatus.Connecting);
                await _hub.StartAsync();
                SetStatus(LinkStatus.Online);
            }
            return _hub.State == HubConnectionState.Connected;
        }
        catch (Exception)
        {
            SetStatus(LinkStatus.Offline);
            return false;
        }
        finally
        {
            _connectGate.Release();
        }
    }

    private HubConnection BuildConnection()
    {
        var hub = new HubConnectionBuilder()
            .WithUrl(navigation.ToAbsoluteUri(HubRoutes.Duel))
            .WithAutomaticReconnect([TimeSpan.Zero, TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromSeconds(20), TimeSpan.FromSeconds(30)])
            .Build();

        hub.On<PlayerView>(HubMethods.RoomUpdated, view =>
        {
            Apply(view);
            return Task.CompletedTask;
        });
        hub.Reconnecting += _ =>
        {
            SetStatus(LinkStatus.Reconnecting);
            return Task.CompletedTask;
        };
        hub.Reconnected += async _ =>
        {
            SetStatus(LinkStatus.Online);
            // A reconnect creates a new connection id, so the seat must be re-claimed with the token.
            if (Session is not null)
            {
                var resumed = await StartSessionAsync(HubMethods.Resume, Session.RoomCode, Session.Token);
                SessionError = resumed is Failure<SessionTicket> failed ? failed.Error : null;
                Changed?.Invoke();
            }
        };
        hub.Closed += _ =>
        {
            SetStatus(LinkStatus.Offline);
            return Task.CompletedTask;
        };
        return hub;
    }

    private async Task<Result<SessionTicket>> StartSessionAsync(string method, params object?[] args)
    {
        var result = await InvokeAsync<SessionTicket>(method, args);
        if (result is Success<SessionTicket> ok)
        {
            Session = new StoredSession(ok.Value.RoomCode, ok.Value.PlayerId, ok.Value.Token);
            SessionError = null;
            await browser.SetAsync(StorageKind.Session, SessionKey, JsonSerializer.Serialize(Session));
            View = null;
            Apply(ok.Value.View);
        }
        return result;
    }

    private async Task<Result<T>> InvokeAsync<T>(string method, params object?[] args)
    {
        if (!await EnsureConnectedAsync())
            return Result.Fail<T>(GameErrors.ConnectionLost);
        try
        {
            var response = await _hub!.InvokeCoreAsync<HubResponse<T>>(method, args);
            return response.ToResult();
        }
        catch (Exception)
        {
            return Result.Fail<T>(GameErrors.ConnectionLost);
        }
    }

    /// <summary>Accepts a snapshot unless it belongs to another room or is older than the current one.</summary>
    private void Apply(PlayerView view)
    {
        if (Session is not null && !string.Equals(view.RoomCode, Session.RoomCode, StringComparison.OrdinalIgnoreCase)) return;
        if (View is not null && view.ServerTime < View.ServerTime) return;

        View = view;
        ServerOffset = view.ServerTime - DateTimeOffset.UtcNow;
        Changed?.Invoke();
    }

    private async Task<StoredSession?> LoadSessionAsync()
    {
        var json = await browser.GetAsync(StorageKind.Session, SessionKey);
        if (string.IsNullOrEmpty(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<StoredSession>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task ForgetSessionAsync()
    {
        Session = null;
        View = null;
        SessionError = null;
        await browser.RemoveAsync(StorageKind.Session, SessionKey);
        Changed?.Invoke();
    }

    private void SetStatus(LinkStatus status)
    {
        if (Status == status) return;
        Status = status;
        Changed?.Invoke();
    }

    public async ValueTask DisposeAsync()
    {
        if (_hub is not null) await _hub.DisposeAsync();
    }
}
