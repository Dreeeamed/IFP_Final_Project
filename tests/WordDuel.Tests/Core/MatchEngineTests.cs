using System.Collections.Immutable;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student B — the pure match state machine.</summary>
public class MatchEngineTests
{
    private static readonly string[] SixMisses = ["SLATE", "ABBEY", "APPLE", "HELLO", "ROBOT", "SPEED"];

    [Fact]
    public void Joining_starts_round_one_with_server_time_and_deadline()
    {
        var state = StartedMatch("GRAPE");

        Assert.Equal(1, state.Round.Number);
        Assert.Equal("GRAPE", state.Round.Secret);
        Assert.Equal(T0, state.Round.StartedAt);
        Assert.Equal(T0 + MatchRules.BestOfThree.RoundDuration, state.Round.Deadline);
        Assert.Equal(2, state.Round.Boards.Count);
    }

    [Fact]
    public void Host_cannot_join_own_room_and_a_third_player_cannot_join()
    {
        var waiting = MatchEngine.Create(Alice, MatchRules.BestOfThree);
        Assert.Equal(ErrorCode.InvalidAction, ErrorOf(MatchEngine.Apply(waiting, new OpponentJoins(Alice), At(0))).Code);

        var playing = StartedMatch();
        Assert.Equal(GameErrors.RoomFull, ErrorOf(MatchEngine.Apply(playing, new OpponentJoins(Mallory), At(1))));
    }

    [Fact]
    public void Guess_from_a_player_outside_the_match_is_refused()
    {
        var result = MatchEngine.Apply(StartedMatch(), new SubmitGuess(Mallory.Id, Valid("CRANE")), At(5));
        Assert.Equal(GameErrors.NotInRoom, ErrorOf(result));
    }

    [Fact]
    public void Guess_before_the_match_starts_is_refused()
    {
        var waiting = MatchEngine.Create(Alice, MatchRules.BestOfThree);
        Assert.Equal(GameErrors.RoundNotActive, ErrorOf(MatchEngine.Apply(waiting, new SubmitGuess(Alice.Id, Valid("CRANE")), At(1))));
    }

    [Fact]
    public void Round_continues_until_both_players_finish()
    {
        var state = Guesses(StartedMatch(), Alice, 10, "SLATE", "CRANE");

        var playing = Assert.IsType<RoundInProgress>(state);
        Assert.True(playing.Round.Boards[Alice.Id].Board.IsFinished);
        Assert.Equal(T0.AddSeconds(11), playing.Round.Boards[Alice.Id].FinishedAt);
        Assert.False(playing.Round.Boards[Bob.Id].Board.IsFinished);
    }

    [Fact]
    public void Only_solver_wins_the_round()
    {
        var state = Guesses(Guesses(StartedMatch(), Alice, 10, "SLATE", "CRANE"), Bob, 20, SixMisses);

        var pause = Assert.IsType<BetweenRounds>(state);
        var round = Assert.Single(pause.Duel.History);
        Assert.Equal(new RoundWon(Alice.Id, WinReason.OnlySolver), round.Outcome);
        Assert.Equal(RoundEndReason.AllFinished, round.EndReason);
        Assert.Equal(1, pause.Duel.WinsOf(Alice.Id));
    }

    [Fact]
    public void Fewer_guesses_win_when_both_solve()
    {
        var state = Guesses(Guesses(StartedMatch(), Bob, 5, "SLATE", "TRACE", "CRANE"), Alice, 30, "GRAPE", "CRANE");
        var round = Assert.Single(Assert.IsType<BetweenRounds>(state).Duel.History);
        Assert.Equal(new RoundWon(Alice.Id, WinReason.FewerGuesses), round.Outcome);
    }

    [Fact]
    public void Equal_guess_count_is_decided_by_server_measured_time()
    {
        // Bob finishes at t=6s, Alice at t=41s, both in two guesses.
        var state = Guesses(Guesses(StartedMatch(), Bob, 5, "SLATE", "CRANE"), Alice, 40, "GRAPE", "CRANE");
        var round = Assert.Single(Assert.IsType<BetweenRounds>(state).Duel.History);

        Assert.Equal(new RoundWon(Bob.Id, WinReason.FasterSolve), round.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(6), round.Results[Bob.Id].SolveTime);
    }

    [Fact]
    public void Nobody_solving_is_a_draw_and_awards_no_point()
    {
        var state = Guesses(Guesses(StartedMatch(), Alice, 1, SixMisses), Bob, 10, SixMisses);
        var pause = Assert.IsType<BetweenRounds>(state);

        Assert.IsType<RoundDrawn>(Assert.Single(pause.Duel.History).Outcome);
        Assert.Equal(0, pause.Duel.WinsOf(Alice.Id));
        Assert.Equal(0, pause.Duel.WinsOf(Bob.Id));
        Assert.Equal(1, pause.Duel.Draws);
    }

    [Fact]
    public void Clock_tick_after_the_deadline_ends_the_round_and_unfinished_players_count_as_unsolved()
    {
        var state = Guesses(StartedMatch(), Alice, 1, "CRANE");
        var deadline = MatchRules.BestOfThree.RoundDuration.TotalSeconds;

        Assert.IsType<RoundInProgress>(Step(state, ClockTick.Instance, At(deadline - 1)));

        var pause = Assert.IsType<BetweenRounds>(Step(state, ClockTick.Instance, At(deadline)));
        var round = Assert.Single(pause.Duel.History);
        Assert.Equal(RoundEndReason.TimeExpired, round.EndReason);
        Assert.Equal(new RoundWon(Alice.Id, WinReason.OnlySolver), round.Outcome);
        Assert.False(round.Results[Bob.Id].Solved);
    }

    [Fact]
    public void Guess_after_the_deadline_is_refused_even_before_the_tick()
    {
        var deadline = MatchRules.BestOfThree.RoundDuration.TotalSeconds;
        var result = MatchEngine.Apply(StartedMatch(), new SubmitGuess(Alice.Id, Valid("CRANE")), At(deadline + 1));
        Assert.Equal(GameErrors.RoundNotActive, ErrorOf(result));
    }

    [Fact]
    public void Both_players_ready_starts_the_next_round_with_the_supplied_secret()
    {
        var pause = (BetweenRounds)Guesses(Guesses(StartedMatch(), Alice, 1, "CRANE"), Bob, 2, SixMisses);

        var halfReady = Assert.IsType<BetweenRounds>(Step(pause, new MarkReady(Alice.Id), At(20, "GRAPE")));
        Assert.Contains(Alice.Id, halfReady.Ready);

        var next = Assert.IsType<RoundInProgress>(Step(halfReady, new MarkReady(Bob.Id), At(21, "GRAPE")));
        Assert.Equal(2, next.Round.Number);
        Assert.Equal("GRAPE", next.Round.Secret);
        Assert.All(next.Round.Boards.Values, round => Assert.Equal(0, round.Board.AttemptsUsed));
    }

    [Fact]
    public void Break_timer_starts_the_next_round_automatically()
    {
        var pause = (BetweenRounds)Guesses(Guesses(StartedMatch(), Alice, 1, "CRANE"), Bob, 2, SixMisses);
        var endOfBreak = (pause.NextRoundAt - T0).TotalSeconds;

        Assert.IsType<BetweenRounds>(Step(pause, ClockTick.Instance, At(endOfBreak - 1)));
        Assert.IsType<RoundInProgress>(Step(pause, ClockTick.Instance, At(endOfBreak, "SLATE")));
    }

    [Fact]
    public void First_to_two_round_wins_takes_the_best_of_three()
    {
        var state = PlayRoundWonBy(StartedMatch("CRANE"), Alice, "CRANE", startSecond: 0, nextSecret: "GRAPE");
        state = PlayRoundWonBy(state, Alice, "GRAPE", startSecond: 40, nextSecret: "LEMON");

        var over = Assert.IsType<MatchOver>(state);
        Assert.Equal(new MatchWon(Alice.Id, MatchEndReason.ReachedWinTarget), over.Result);
        Assert.Equal(2, over.Duel.RoundsPlayed);
    }

    [Fact]
    public void Draws_extend_the_match_but_the_round_limit_stops_it()
    {
        var rules = MatchRules.BestOfThree with { MaxRounds = 3 };
        MatchState state = StartedMatch(rules: rules);
        for (var round = 0; round < 3; round++)
        {
            // Each round starts 31 s after the previous one (both players press "ready").
            state = Guesses(Guesses(state, Alice, round * 40 + 1, SixMisses), Bob, round * 40 + 10, SixMisses);
            if (state is BetweenRounds)
                state = Step(Step(state, new MarkReady(Alice.Id), At(round * 40 + 30)), new MarkReady(Bob.Id), At(round * 40 + 31));
        }

        var over = Assert.IsType<MatchOver>(state);
        Assert.IsType<MatchDrawn>(over.Result);
        Assert.Equal(3, over.Duel.Draws);
    }

    [Fact]
    public void Round_limit_with_unequal_wins_gives_the_match_to_the_leader()
    {
        var duel = StartedMatch().Duel;
        var rules = MatchRules.BestOfThree with { MaxRounds = 1 };
        var history = duel with
        {
            History =
            [
                new RoundSummary(1, "CRANE", new RoundWon(Bob.Id, WinReason.OnlySolver), RoundEndReason.AllFinished, T0, T0,
                    ImmutableDictionary<PlayerId, PlayerRound>.Empty, ImmutableDictionary<PlayerId, CompletionRecord>.Empty)
            ]
        };

        Assert.Equal(new MatchWon(Bob.Id, MatchEndReason.RoundLimit), ((Something<MatchResult>)MatchEngine.DecideMatch(rules, history)).Value);
    }

    [Fact]
    public void Rematch_needs_both_votes_and_starts_a_fresh_match()
    {
        var over = WinMatch();
        var oneVote = Assert.IsType<MatchOver>(Step(over, new VoteRematch(Bob.Id), At(900, "LEMON")));
        var rematch = Assert.IsType<RoundInProgress>(Step(oneVote, new VoteRematch(Alice.Id), At(901, "LEMON")));

        Assert.Empty(rematch.Duel.History);
        Assert.Equal(1, rematch.Round.Number);
        Assert.Equal("LEMON", rematch.Round.Secret);
    }

    [Fact]
    public void Forfeit_ends_the_match_for_the_opponent_and_blocks_a_rematch()
    {
        var over = Assert.IsType<MatchOver>(Step(StartedMatch(), new Forfeit(Bob.Id), At(30)));
        Assert.Equal(new MatchWon(Alice.Id, MatchEndReason.Forfeit), over.Result);
        Assert.Equal(ErrorCode.InvalidAction, ErrorOf(MatchEngine.Apply(over, new VoteRematch(Alice.Id), At(31))).Code);
    }

    [Fact]
    public void Invalid_actions_are_failures_not_exceptions()
    {
        var playing = StartedMatch();
        Assert.Equal(ErrorCode.InvalidAction, ErrorOf(MatchEngine.Apply(playing, new MarkReady(Alice.Id), At(1))).Code);
        Assert.Equal(ErrorCode.InvalidAction, ErrorOf(MatchEngine.Apply(playing, new VoteRematch(Alice.Id), At(1))).Code);
    }

    [Fact]
    public void Replay_folds_commands_and_stops_at_the_first_failure()
    {
        var steps = new (MatchCommand, TransitionContext)[]
        {
            (new OpponentJoins(Bob), At(0)),
            (new SubmitGuess(Alice.Id, Valid("CRANE")), At(5)),
            (new SubmitGuess(Alice.Id, Valid("SLATE")), At(6)),   // Alice is already finished → failure
            (new SubmitGuess(Bob.Id, Valid("CRANE")), At(7))      // never applied
        };

        var result = MatchEngine.Replay(MatchEngine.Create(Alice, MatchRules.BestOfThree), steps);
        Assert.Equal(GameErrors.BoardFinished, ErrorOf(result));
    }

    [Fact]
    public void Used_secrets_cover_every_round_of_the_match()
    {
        var state = PlayRoundWonBy(StartedMatch("CRANE"), Alice, "CRANE", 0, "GRAPE");
        Assert.Equal(new[] { "CRANE", "GRAPE" }, MatchEngine.UsedSecrets(state).Order());
    }

    // ── helpers ──────────────────────────────────────────────────────────────

    /// <summary>Winner solves first try, loser misses six times, then both are ready.</summary>
    private static MatchState PlayRoundWonBy(MatchState state, PlayerInfo winner, string secret, double startSecond, string nextSecret)
    {
        var loser = winner == Alice ? Bob : Alice;
        state = Guesses(state, winner, startSecond + 1, secret);
        state = Guesses(state, loser, startSecond + 10, SixMisses.Where(w => w != secret).Take(6).ToArray());
        return state is BetweenRounds
            ? Step(Step(state, new MarkReady(Alice.Id), At(startSecond + 30, nextSecret)), new MarkReady(Bob.Id), At(startSecond + 31, nextSecret))
            : state;
    }

    private static MatchState WinMatch()
    {
        var state = PlayRoundWonBy(StartedMatch("CRANE"), Alice, "CRANE", 0, "GRAPE");
        return PlayRoundWonBy(state, Alice, "GRAPE", 40, "LEMON");
    }
}
