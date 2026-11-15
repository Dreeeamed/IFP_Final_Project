using System.Text.Json;
using WordDuel.Core.Common;
using WordDuel.Core.Contracts;
using WordDuel.Core.Functional;
using WordDuel.Core.Views;
using WordDuel.Tests.TestSupport;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Infrastructure;

/// <summary>Student B — the imperative shell: rooms, identity, security, timers, persistence.</summary>
public class DuelServiceTests
{
    private const string AliceConn = "conn-alice";
    private const string BobConn = "conn-bob";
    private const string MalloryConn = "conn-mallory";

    private static async Task<(ServiceHarness H, SessionTicket Alice, SessionTicket Bob)> StartedDuel(Action<WordDuel.Infrastructure.DuelOptions>? configure = null)
    {
        var h = new ServiceHarness(configure);
        var alice = Unwrap(await h.Service.CreateRoomAsync(AliceConn, "Alice"));
        var bob = Unwrap(await h.Service.JoinRoomAsync(BobConn, alice.RoomCode, "Bob"));
        return (h, alice, bob);
    }

    private static async Task Guess(ServiceHarness h, string connection, params string[] words)
    {
        foreach (var word in words)
        {
            h.Wait();
            Unwrap(await h.Service.SubmitGuessAsync(connection, word));
        }
    }

    [Fact]
    public async Task Creating_a_room_returns_a_code_a_secret_token_and_a_waiting_view()
    {
        var h = new ServiceHarness();
        var ticket = Unwrap(await h.Service.CreateRoomAsync(AliceConn, "  Alice "));

        Assert.Equal(6, ticket.RoomCode.Length);
        Assert.False(string.IsNullOrWhiteSpace(ticket.Token));
        Assert.Equal(MatchPhase.WaitingForOpponent, ticket.View.Phase);
        Assert.Equal("Alice", ticket.View.Me.Nickname);
        Assert.Equal(1, h.Service.ActiveRooms);
    }

    [Theory]
    [InlineData("")]
    [InlineData("<b>bad</b>")]
    public async Task Invalid_nicknames_are_rejected(string nickname)
    {
        var h = new ServiceHarness();
        Assert.Equal(GameErrors.InvalidNickname, ErrorOf(await h.Service.CreateRoomAsync(AliceConn, nickname)));
        Assert.Equal(0, h.Service.ActiveRooms);
    }

    [Fact]
    public async Task Joining_starts_the_match_and_notifies_both_players()
    {
        var (h, alice, bob) = await StartedDuel();

        Assert.Equal(alice.RoomCode, bob.RoomCode);
        Assert.Equal(MatchPhase.Playing, bob.View.Phase);
        Assert.Equal(MatchPhase.Playing, h.Notifier.LastFor(AliceConn)!.Phase);
        Assert.Equal("Bob", h.Notifier.LastFor(AliceConn)!.Opponent!.Nickname);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("ABC-12")]
    public async Task Malformed_room_codes_are_rejected(string code)
    {
        var h = new ServiceHarness();
        Assert.Equal(GameErrors.InvalidRoomCode, ErrorOf(await h.Service.JoinRoomAsync(BobConn, code, "Bob")));
    }

    [Fact]
    public async Task Unknown_room_codes_are_reported()
    {
        var h = new ServiceHarness();
        Assert.Equal(GameErrors.RoomNotFound, ErrorOf(await h.Service.JoinRoomAsync(BobConn, "ZZZZZZ", "Bob")));
    }

    [Fact]
    public async Task A_room_holds_exactly_two_players()
    {
        var (h, alice, _) = await StartedDuel();
        Assert.Equal(GameErrors.RoomFull, ErrorOf(await h.Service.JoinRoomAsync(MalloryConn, alice.RoomCode, "Mallory")));
    }

    [Fact]
    public async Task A_connection_without_a_seat_cannot_submit_guesses()
    {
        var (h, _, _) = await StartedDuel();
        Assert.Equal(GameErrors.NotInRoom, ErrorOf(await h.Service.SubmitGuessAsync(MalloryConn, "CRANE")));
    }

    [Fact]
    public async Task Identity_comes_from_the_connection_so_nobody_can_guess_for_someone_else()
    {
        var (h, _, _) = await StartedDuel();
        await Guess(h, BobConn, "SLATE");

        var aliceView = h.Notifier.LastFor(AliceConn)!;
        Assert.Empty(aliceView.Me.Board!.Guesses);          // Alice's board untouched
        Assert.Equal(1, aliceView.Opponent!.AttemptsUsed);  // Bob's attempt counted for Bob
    }

    [Fact]
    public async Task Room_code_alone_does_not_grant_access_to_a_seat()
    {
        var (h, alice, _) = await StartedDuel();
        Assert.Equal(GameErrors.InvalidToken, ErrorOf(await h.Service.ResumeAsync(MalloryConn, alice.RoomCode, "guessed-token")));
        Assert.Equal(GameErrors.InvalidToken, ErrorOf(await h.Service.ResumeAsync(MalloryConn, alice.RoomCode, null)));
    }

    [Fact]
    public async Task Resuming_with_the_token_moves_the_seat_to_the_new_connection()
    {
        var (h, alice, _) = await StartedDuel();
        await h.Service.DisconnectedAsync(AliceConn);
        Assert.Equal(Presence.Disconnected, h.Notifier.LastFor(BobConn)!.Opponent!.Presence);

        var resumed = Unwrap(await h.Service.ResumeAsync("conn-alice-2", alice.RoomCode, alice.Token));
        Assert.Equal(MatchPhase.Playing, resumed.View.Phase);
        Assert.Equal(Presence.Connected, h.Notifier.LastFor(BobConn)!.Opponent!.Presence);

        h.Wait();
        Assert.Equal(GameErrors.NotInRoom, ErrorOf(await h.Service.SubmitGuessAsync(AliceConn, "CRANE")));  // old connection
        Assert.IsType<Success<PlayerView>>(await h.Service.SubmitGuessAsync("conn-alice-2", "CRANE"));
    }

    [Fact]
    public async Task Rapid_repeated_actions_are_throttled()
    {
        var (h, _, _) = await StartedDuel();
        h.Wait();
        Unwrap(await h.Service.SubmitGuessAsync(AliceConn, "SLATE"));
        Assert.Equal(GameErrors.TooFast, ErrorOf(await h.Service.SubmitGuessAsync(AliceConn, "GRAPE")));
    }

    [Fact]
    public async Task Invalid_and_duplicate_guesses_do_not_use_attempts()
    {
        var (h, _, _) = await StartedDuel();
        await Guess(h, AliceConn, "SLATE");
        h.Wait();
        Assert.Equal(ErrorCode.NotInDictionary, ErrorOf(await h.Service.SubmitGuessAsync(AliceConn, "QQQQQ")).Code);
        h.Wait();
        Assert.Equal(ErrorCode.AlreadyGuessed, ErrorOf(await h.Service.SubmitGuessAsync(AliceConn, "slate")).Code);

        Assert.Equal(1, h.Notifier.LastFor(BobConn)!.Opponent!.AttemptsUsed);
    }

    [Fact]
    public async Task Pushed_views_never_contain_the_opponents_words_or_the_secret_during_a_round()
    {
        var (h, _, _) = await StartedDuel();
        await Guess(h, AliceConn, "SLATE", "GRAPE");

        var bobUpdates = h.Notifier.Sent.Where(s => s.Connection == BobConn).Select(s => JsonSerializer.Serialize(s.View)).ToList();
        Assert.NotEmpty(bobUpdates);
        Assert.All(bobUpdates, json =>
        {
            Assert.DoesNotContain("SLATE", json);
            Assert.DoesNotContain("GRAPE", json);
            Assert.DoesNotContain("CRANE", json);
        });
    }

    [Fact]
    public async Task Round_completes_when_both_finish_and_includes_the_analysis()
    {
        var (h, _, _) = await StartedDuel();
        await Guess(h, AliceConn, "SLATE", "CRANE");
        await Guess(h, BobConn, "TRACE", "CRATE", "CRANE");

        var view = h.Notifier.LastFor(BobConn)!;
        Assert.Equal(MatchPhase.RoundOver, view.Phase);
        Assert.Equal("CRANE", view.LastRound!.Secret);
        Assert.Equal(Verdict.Lost, view.LastRound.Verdict);
        Assert.NotNull(view.LastRound.Analysis);
        Assert.Equal(3, view.LastRound.Analysis!.Me.Guesses.Count);
    }

    [Fact]
    public async Task Server_clock_ends_a_stalled_round()
    {
        var (h, _, _) = await StartedDuel(o => o.RoundSeconds = 60);
        await Guess(h, AliceConn, "CRANE");

        h.Clock.Advance(TimeSpan.FromSeconds(61));
        await h.Service.TickAsync();

        var view = h.Notifier.LastFor(AliceConn)!;
        Assert.Equal(MatchPhase.RoundOver, view.Phase);
        Assert.Equal(Verdict.Won, view.LastRound!.Verdict);
        Assert.Equal(WordDuel.Core.Match.RoundEndReason.TimeExpired, view.LastRound.EndReason);
    }

    [Fact]
    public async Task A_player_who_does_not_reconnect_forfeits_and_the_match_is_saved()
    {
        var (h, _, _) = await StartedDuel();
        await h.Service.DisconnectedAsync(BobConn);

        h.Clock.Advance(TimeSpan.FromSeconds(30));
        await h.Service.TickAsync();
        Assert.Equal(MatchPhase.Playing, h.Notifier.LastFor(AliceConn)!.Phase); // still within grace period

        h.Clock.Advance(TimeSpan.FromSeconds(31));
        await h.Service.TickAsync();
        var view = h.Notifier.LastFor(AliceConn)!;
        Assert.Equal(MatchPhase.MatchOver, view.Phase);
        Assert.Equal(Verdict.Won, view.Match!.Verdict);
        Assert.False(view.RematchAvailable);

        var saved = Assert.Single(await h.History.RecentAsync(10));
        Assert.Equal("Alice", saved.WinnerNickname);
        Assert.Equal("Forfeit", saved.EndReason);
    }

    [Fact]
    public async Task Full_best_of_three_and_rematch()
    {
        var (h, _, _) = await StartedDuel();

        for (var round = 0; round < 2; round++)
        {
            await Guess(h, AliceConn, "CRANE");
            await Guess(h, BobConn, "SLATE", "GRAPE", "TRACE", "CRATE", "BRAKE", "ABBEY");
            if (round == 0)
            {
                h.Wait();
                Unwrap(await h.Service.MarkReadyAsync(AliceConn));
                h.Wait();
                Unwrap(await h.Service.MarkReadyAsync(BobConn));
            }
        }

        var over = h.Notifier.LastFor(AliceConn)!;
        Assert.Equal(MatchPhase.MatchOver, over.Phase);
        Assert.Equal(2, over.Score.MyWins);
        Assert.True(over.RematchAvailable);
        Assert.Equal("Alice", Assert.Single(await h.History.RecentAsync(5)).WinnerNickname);

        h.Wait();
        Unwrap(await h.Service.VoteRematchAsync(BobConn));
        h.Wait();
        var rematch = Unwrap(await h.Service.VoteRematchAsync(AliceConn));
        Assert.Equal(MatchPhase.Playing, rematch.Phase);
        Assert.Equal(1, rematch.RoundNumber);
        Assert.Equal(0, rematch.Score.MyWins);
    }

    [Fact]
    public async Task Leaving_the_waiting_room_closes_it()
    {
        var h = new ServiceHarness();
        var alice = Unwrap(await h.Service.CreateRoomAsync(AliceConn, "Alice"));
        Unwrap(await h.Service.LeaveAsync(AliceConn));

        Assert.Equal(0, h.Service.ActiveRooms);
        Assert.Equal(GameErrors.RoomNotFound, ErrorOf(await h.Service.JoinRoomAsync(BobConn, alice.RoomCode, "Bob")));
    }

    [Fact]
    public async Task Server_capacity_is_enforced()
    {
        var h = new ServiceHarness(o => o.MaxRooms = 1);
        Unwrap(await h.Service.CreateRoomAsync(AliceConn, "Alice"));
        Assert.Equal(GameErrors.ServerFull, ErrorOf(await h.Service.CreateRoomAsync(BobConn, "Bob")));
    }
}
