using System.Collections.Immutable;
using System.Text.Json;
using WordDuel.Core.Analysis;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Views;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student B — the Blind Duel guarantee, verified on the exact objects sent over the network.</summary>
public class PlayerViewProjectionTests
{
    private static readonly ProjectionContext Context = new(
        "ABC234",
        T0,
        new Dictionary<PlayerId, Presence> { [Alice.Id] = Presence.Connected, [Bob.Id] = Presence.Connected },
        ImmutableDictionary<int, RoundAnalysis>.Empty);

    private static string Json(PlayerView view) => JsonSerializer.Serialize(view);

    [Fact]
    public void During_a_round_the_opponent_is_only_counts_and_flags()
    {
        // Alice has made two guesses; Bob looks at the room.
        var state = Guesses(StartedMatch("CRANE"), Alice, 5, "SLATE", "GRAPE");
        var bobView = PlayerViewProjection.Project(state, Bob.Id, Context);

        Assert.Equal(MatchPhase.Playing, bobView.Phase);
        Assert.Equal(2, bobView.Opponent!.AttemptsUsed);
        Assert.False(bobView.Opponent.Finished);
        Assert.Null(bobView.LastRound);

        var json = Json(bobView);
        Assert.DoesNotContain("SLATE", json);
        Assert.DoesNotContain("GRAPE", json);
        Assert.DoesNotContain("CRANE", json); // the secret
    }

    [Fact]
    public void Finishing_early_reveals_nothing_to_the_player_still_guessing()
    {
        var state = Guesses(StartedMatch("CRANE"), Alice, 5, "SLATE", "CRANE");
        var bobView = PlayerViewProjection.Project(state, Bob.Id, Context);

        Assert.True(bobView.Opponent!.Finished);
        Assert.Equal(MatchPhase.Playing, bobView.Phase);
        Assert.DoesNotContain("CRANE", Json(bobView));
        Assert.DoesNotContain("SLATE", Json(bobView));
    }

    [Fact]
    public void A_player_always_sees_their_own_board_with_feedback()
    {
        var state = Guesses(StartedMatch("CRANE"), Alice, 5, "TRACE");
        var aliceView = PlayerViewProjection.Project(state, Alice.Id, Context);

        var guess = Assert.Single(aliceView.Me.Board!.Guesses);
        Assert.Equal("TRACE", guess.Word);
        Assert.Equal(".GGYG", Marks(guess.States));
    }

    [Fact]
    public void After_the_round_both_boards_and_the_secret_are_revealed()
    {
        var state = Guesses(Guesses(StartedMatch("CRANE"), Alice, 5, "SLATE", "CRANE"), Bob, 10, "GRAPE", "TRACE", "CRATE", "BRAKE", "ABBEY", "HELLO");
        var bobView = PlayerViewProjection.Project(state, Bob.Id, Context);

        Assert.Equal(MatchPhase.RoundOver, bobView.Phase);
        Assert.Equal("CRANE", bobView.LastRound!.Secret);
        Assert.Equal(Verdict.Lost, bobView.LastRound.Verdict);
        Assert.Equal(new[] { "SLATE", "CRANE" }, bobView.LastRound.Opponent.Board.Guesses.Select(g => g.Word));
        Assert.Equal(1, bobView.Score.OpponentWins);
        Assert.Equal(new[] { Verdict.Lost }, bobView.Score.Rounds);
    }

    [Fact]
    public void Waiting_room_has_no_opponent_and_no_board()
    {
        var view = PlayerViewProjection.Project(MatchEngine.Create(Alice, MatchRules.BestOfThree), Alice.Id, Context);
        Assert.Equal(MatchPhase.WaitingForOpponent, view.Phase);
        Assert.Null(view.Opponent);
        Assert.Null(view.Me.Board);
    }

    [Fact]
    public void Rematch_is_unavailable_while_the_opponent_is_disconnected()
    {
        var over = Step(StartedMatch(), new Forfeit(Bob.Id), At(1));
        var context = Context with { Presence = new Dictionary<PlayerId, Presence> { [Alice.Id] = Presence.Connected, [Bob.Id] = Presence.Left } };
        var view = PlayerViewProjection.Project(over, Alice.Id, context);

        Assert.Equal(Verdict.Won, view.Match!.Verdict);
        Assert.Equal(MatchEndReason.Forfeit, view.Match.Reason);
        Assert.False(view.RematchAvailable);
    }
}
