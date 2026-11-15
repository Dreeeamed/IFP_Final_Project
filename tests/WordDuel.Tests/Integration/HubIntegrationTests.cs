using System.Collections.Concurrent;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using WordDuel.Core.Common;
using WordDuel.Core.Contracts;
using WordDuel.Core.Views;
using WordDuel.Infrastructure;
using WordDuel.Infrastructure.Persistence;
using WordDuel.Tests.TestSupport;

namespace WordDuel.Tests.Integration;

/// <summary>
/// Runs the real ASP.NET Core host in memory and connects genuine SignalR clients to the hub.
/// Only the secret picker (so the test knows the word) and the history store are replaced.
/// </summary>
public sealed class WordDuelFactory : WebApplicationFactory<Program>
{
    public FixedSecretPicker Secrets { get; } = new("CRANE");

    public InMemoryMatchHistory History { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:History", $"Data Source={Path.Combine(Path.GetTempPath(), $"wordduel-test-{Guid.NewGuid():N}.db")}");
        builder.UseSetting("WordDuel:MinActionIntervalMs", "0");
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISecretWordPicker>();
            services.AddSingleton<ISecretWordPicker>(Secrets);
            services.RemoveAll<IMatchHistory>();
            services.AddSingleton<IMatchHistory>(History);
        });
    }
}

/// <summary>A test player: one real SignalR connection plus every view the server pushed to it.</summary>
internal sealed class TestPlayer : IAsyncDisposable
{
    private readonly HubConnection _connection;

    public ConcurrentQueue<PlayerView> Updates { get; } = new();

    private TestPlayer(HubConnection connection) => _connection = connection;

    public static async Task<TestPlayer> ConnectAsync(WordDuelFactory factory)
    {
        var server = factory.Server;
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(server.BaseAddress, HubRoutes.Duel), options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => server.CreateHandler();
            })
            .Build();

        var player = new TestPlayer(connection);
        connection.On<PlayerView>(HubMethods.RoomUpdated, view => player.Updates.Enqueue(view));
        await connection.StartAsync();
        return player;
    }

    public Task<HubResponse<T>> Invoke<T>(string method, params object?[] args) =>
        _connection.InvokeCoreAsync<HubResponse<T>>(method, args);

    /// <summary>Waits until a pushed view satisfies <paramref name="predicate"/>.</summary>
    public async Task<PlayerView> WaitFor(Func<PlayerView, bool> predicate, int timeoutMs = 5000)
    {
        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            var match = Updates.LastOrDefault(predicate);
            if (match is not null) return match;
            await Task.Delay(25);
        }
        throw new TimeoutException("The expected update never arrived.");
    }

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}

public class HubIntegrationTests(WordDuelFactory factory) : IClassFixture<WordDuelFactory>
{
    private async Task<(TestPlayer Alice, TestPlayer Bob, SessionTicket AliceTicket)> StartDuelAsync()
    {
        var alice = await TestPlayer.ConnectAsync(factory);
        var bob = await TestPlayer.ConnectAsync(factory);

        var created = await alice.Invoke<SessionTicket>(HubMethods.CreateRoom, "Alice");
        Assert.True(created.Ok, created.Error?.Message);

        var joined = await bob.Invoke<SessionTicket>(HubMethods.JoinRoom, created.Value!.RoomCode, "Bob");
        Assert.True(joined.Ok, joined.Error?.Message);
        return (alice, bob, created.Value);
    }

    [Fact]
    public async Task Two_real_connections_create_join_and_receive_the_match_start()
    {
        var (alice, bob, _) = await StartDuelAsync();
        await using var _ = alice;
        await using var __ = bob;

        var aliceView = await alice.WaitFor(v => v.Phase == MatchPhase.Playing);
        var bobView = await bob.WaitFor(v => v.Phase == MatchPhase.Playing);

        Assert.Equal("Bob", aliceView.Opponent!.Nickname);
        Assert.Equal("Alice", bobView.Opponent!.Nickname);
        Assert.NotNull(aliceView.RoundDeadline);
    }

    [Fact]
    public async Task Guess_counts_are_synchronised_but_the_words_stay_blind()
    {
        var (alice, bob, _) = await StartDuelAsync();
        await using var _ = alice;
        await using var __ = bob;

        var response = await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "slate");
        Assert.True(response.Ok);
        Assert.Equal("SLATE", response.Value!.Me.Board!.Guesses[0].Word);

        var bobView = await bob.WaitFor(v => v.Opponent?.AttemptsUsed == 1);
        var json = JsonSerializer.Serialize(bobView);
        Assert.DoesNotContain("SLATE", json);
        Assert.DoesNotContain("CRANE", json);
    }

    [Fact]
    public async Task Server_rejects_unauthorised_and_invalid_actions()
    {
        var (alice, bob, ticket) = await StartDuelAsync();
        await using var mallory = await TestPlayer.ConnectAsync(factory);
        await using var _ = alice;
        await using var __ = bob;

        Assert.Equal(ErrorCode.NotInRoom, (await mallory.Invoke<PlayerView>(HubMethods.SubmitGuess, "CRANE")).Error!.Code);
        Assert.Equal(ErrorCode.RoomFull, (await mallory.Invoke<SessionTicket>(HubMethods.JoinRoom, ticket.RoomCode, "Mallory")).Error!.Code);
        Assert.Equal(ErrorCode.InvalidToken, (await mallory.Invoke<SessionTicket>(HubMethods.Resume, ticket.RoomCode, "not-the-token")).Error!.Code);
        Assert.Equal(ErrorCode.WrongLength, (await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "CRANES")).Error!.Code);
        Assert.Equal(ErrorCode.NotInDictionary, (await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "QQQQQ")).Error!.Code);
    }

    [Fact]
    public async Task Finishing_a_round_reveals_the_word_both_boards_and_the_analysis_to_both_players()
    {
        var (alice, bob, _) = await StartDuelAsync();
        await using var _ = alice;
        await using var __ = bob;

        await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "TRACE");
        await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "CRANE");
        await bob.Invoke<PlayerView>(HubMethods.SubmitGuess, "CRANE");

        var aliceView = await alice.WaitFor(v => v.Phase == MatchPhase.RoundOver);
        var bobView = await bob.WaitFor(v => v.Phase == MatchPhase.RoundOver);

        Assert.Equal("CRANE", aliceView.LastRound!.Secret);
        Assert.Equal(Verdict.Lost, aliceView.LastRound.Verdict);   // Bob needed fewer guesses
        Assert.Equal(Verdict.Won, bobView.LastRound!.Verdict);
        Assert.Equal(2, bobView.LastRound.Opponent.Board.Guesses.Count);
        Assert.NotNull(bobView.LastRound.Analysis);
    }

    [Fact]
    public async Task A_new_connection_can_resume_a_seat_with_its_token()
    {
        var (alice, bob, ticket) = await StartDuelAsync();
        await using var _ = bob;
        await alice.Invoke<PlayerView>(HubMethods.SubmitGuess, "SLATE");
        await alice.DisposeAsync(); // simulates closing the tab / losing the network

        await using var aliceAgain = await TestPlayer.ConnectAsync(factory);
        var resumed = await aliceAgain.Invoke<SessionTicket>(HubMethods.Resume, ticket.RoomCode, ticket.Token);

        Assert.True(resumed.Ok);
        Assert.Equal("SLATE", resumed.Value!.View.Me.Board!.Guesses[0].Word);
    }

    [Fact]
    public async Task Public_endpoints_expose_health_and_finished_matches_only()
    {
        var client = factory.CreateClient();
        var health = await client.GetFromJsonAsync<JsonElement>("/health");
        Assert.Equal("ok", health.GetProperty("status").GetString());

        var history = await client.GetFromJsonAsync<JsonElement>("/api/history");
        Assert.Equal(JsonValueKind.Array, history.GetProperty("recent").ValueKind);
        Assert.Equal(JsonValueKind.Array, history.GetProperty("leaderboard").ValueKind);
    }
}
