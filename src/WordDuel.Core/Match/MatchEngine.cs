using System.Collections.Immutable;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Players;
using WordDuel.Core.Words;

namespace WordDuel.Core.Match;

/// <summary>
/// The pure state machine of a match:  newState = Apply(oldState, command, context).
/// No I/O, no clock, no randomness — time and the next secret arrive in <see cref="TransitionContext"/>.
/// The imperative shell (Infrastructure) owns the single mutable reference to the current state.
/// </summary>
public static class MatchEngine
{
    public static MatchState Create(PlayerInfo host, MatchRules rules) => new WaitingForOpponent(host) { Rules = rules };

    /// <summary>
    /// Pattern matching on the (state, command) pair (Lecture 3.3 / 5): every legal combination
    /// has an arm; anything else becomes a Failure value instead of an exception.
    /// </summary>
    public static Result<MatchState> Apply(MatchState state, MatchCommand command, TransitionContext context) => (state, command) switch
    {
        (WaitingForOpponent waiting, OpponentJoins join) => StartMatch(waiting, join.Opponent, context),
        (RoundInProgress playing, SubmitGuess guess) => ApplyGuess(playing, guess, context),
        (RoundInProgress playing, ClockTick) => Ok(context.Now >= playing.Round.Deadline
            ? CompleteRound(playing, RoundEndReason.TimeExpired, context)
            : playing),
        (BetweenRounds pause, MarkReady ready) => ApplyReady(pause, ready.Player, context),
        (BetweenRounds pause, ClockTick) => Ok(context.Now >= pause.NextRoundAt
            ? StartRound(pause.Rules, pause.Duel, context)
            : pause),
        (MatchOver over, VoteRematch vote) => ApplyRematchVote(over, vote.Player, context),
        (RoundInProgress playing, Forfeit quit) => ForfeitMatch(playing.Rules, playing.Duel, quit.Player),
        (BetweenRounds pause, Forfeit quit) => ForfeitMatch(pause.Rules, pause.Duel, quit.Player),
        (MatchOver, Forfeit) => Ok(state),
        (_, ClockTick) => Ok(state),
        (_, SubmitGuess) => Fail(GameErrors.RoundNotActive),
        (_, OpponentJoins) => Fail(GameErrors.RoomFull),
        (_, MarkReady) => Fail(GameErrors.InvalidAction("get ready for the next round")),
        (_, VoteRematch) => Fail(GameErrors.InvalidAction("request a rematch")),
        _ => Fail(GameErrors.InvalidAction("do that"))
    };

    /// <summary>
    /// Folds a sequence of commands over a state with Aggregate + Bind (railway over a list):
    /// the first failure stops all later commands. Useful for replaying a match in tests.
    /// </summary>
    public static Result<MatchState> Replay(MatchState initial, IEnumerable<(MatchCommand Command, TransitionContext Context)> steps) =>
        steps.Aggregate(
            Ok(initial),
            (result, step) => result.Bind(state => Apply(state, step.Command, step.Context)));

    /// <summary>Pure match-level rule: is the match decided after the rounds played so far?</summary>
    public static Maybe<MatchResult> DecideMatch(MatchRules rules, Duel duel)
    {
        var first = duel.WinsOf(duel.First.Id);
        var second = duel.WinsOf(duel.Second.Id);
        return (first, second) switch
        {
            _ when first >= rules.WinsNeeded => Some(new MatchWon(duel.First.Id, MatchEndReason.ReachedWinTarget)),
            _ when second >= rules.WinsNeeded => Some(new MatchWon(duel.Second.Id, MatchEndReason.ReachedWinTarget)),
            _ when duel.RoundsPlayed < rules.MaxRounds => Maybe.None<MatchResult>(),
            _ when first == second => Some(new MatchDrawn()),
            _ => Some(new MatchWon(first > second ? duel.First.Id : duel.Second.Id, MatchEndReason.RoundLimit))
        };
    }

    /// <summary>Every secret used in the current match (the shell avoids repeating them).</summary>
    public static ImmutableHashSet<string> UsedSecrets(MatchState state) => state switch
    {
        RoundInProgress playing => playing.Duel.History.Select(r => r.Secret).Append(playing.Round.Secret).ToImmutableHashSet(),
        BetweenRounds pause => pause.Duel.History.Select(r => r.Secret).ToImmutableHashSet(),
        MatchOver over => over.Duel.History.Select(r => r.Secret).ToImmutableHashSet(),
        _ => ImmutableHashSet<string>.Empty
    };

    // ── Transitions ─────────────────────────────────────────────────────────

    private static Result<MatchState> StartMatch(WaitingForOpponent waiting, PlayerInfo opponent, TransitionContext context) =>
        opponent.Id == waiting.Host.Id
            ? Fail(GameErrors.InvalidAction("join your own room twice"))
            : Ok(StartRound(waiting.Rules, new Duel(waiting.Host, opponent, ImmutableList<RoundSummary>.Empty), context));

    private static MatchState StartRound(MatchRules rules, Duel duel, TransitionContext context) =>
        new RoundInProgress(
            duel,
            new ActiveRound(
                Number: duel.RoundsPlayed + 1,
                Secret: context.NextSecret,
                StartedAt: context.Now,
                Deadline: context.Now + rules.RoundDuration,
                Boards: duel.Players.ToImmutableDictionary(player => player.Id, _ => PlayerRound.Start(rules.MaxGuesses))))
        { Rules = rules };

    /// <summary>
    /// Railway-oriented guess pipeline (Lecture 6). Each step either passes a value forward or
    /// switches to the failure track, after which the remaining lambdas are skipped.
    /// </summary>
    private static Result<MatchState> ApplyGuess(RoundInProgress state, SubmitGuess command, TransitionContext context) =>
        state.Round.Boards.Find(command.Player)                                         // Maybe<PlayerRound>
            .ToResult(GameErrors.NotInRoom)                                             // → Result<PlayerRound>
            .Ensure(_ => context.Now < state.Round.Deadline, _ => GameErrors.RoundNotActive)
            .Bind(round => round.Board.Submit(state.Round.Secret, command.Guess)       // may fail: finished / repeated
                .Map(board => round with { Board = board, FinishedAt = board.IsFinished ? context.Now : null }))
            .Map(round => state with { Round = state.Round with { Boards = state.Round.Boards.SetItem(command.Player, round) } })
            .Map(updated => updated.Round.AllFinished
                ? CompleteRound(updated, RoundEndReason.AllFinished, context)
                : updated);

    private static MatchState CompleteRound(RoundInProgress state, RoundEndReason reason, TransitionContext context)
    {
        var round = state.Round;
        var results = round.Boards.ToImmutableDictionary(
            entry => entry.Key,
            entry => ToCompletionRecord(entry.Key, entry.Value, round.StartedAt));

        var outcome = RoundScoring.Decide(results[state.Duel.First.Id], results[state.Duel.Second.Id]);
        var summary = new RoundSummary(round.Number, round.Secret, outcome, reason, round.StartedAt, context.Now, round.Boards, results);
        var duel = state.Duel with { History = state.Duel.History.Add(summary) };

        return DecideMatch(state.Rules, duel) switch
        {
            Something<MatchResult> decided => new MatchOver(duel, decided.Value, ImmutableHashSet<PlayerId>.Empty) { Rules = state.Rules },
            _ => new BetweenRounds(duel, context.Now + state.Rules.BreakDuration, ImmutableHashSet<PlayerId>.Empty) { Rules = state.Rules }
        };
    }

    private static CompletionRecord ToCompletionRecord(PlayerId player, PlayerRound round, DateTimeOffset startedAt) =>
        round.Board.Status == BoardStatus.Solved
            ? new CompletionRecord(player, true, round.Board.AttemptsUsed, round.FinishedAt - startedAt)
            : new CompletionRecord(player, false, round.Board.AttemptsUsed, null);

    private static Result<MatchState> ApplyReady(BetweenRounds pause, PlayerId player, TransitionContext context) =>
        RequireMember(pause.Duel, player)
            .Map(_ => pause with { Ready = pause.Ready.Add(player) })
            .Map(updated => updated.Ready.Count == 2
                ? StartRound(updated.Rules, updated.Duel, context)
                : updated);

    private static Result<MatchState> ApplyRematchVote(MatchOver over, PlayerId player, TransitionContext context) =>
        over.Result is MatchWon { Reason: MatchEndReason.Forfeit }
            ? Fail(GameErrors.InvalidAction("start a rematch after a player left"))
            : RequireMember(over.Duel, player)
                .Map(_ => over with { RematchVotes = over.RematchVotes.Add(player) })
                .Map(updated => updated.RematchVotes.Count == 2
                    ? StartRound(updated.Rules, updated.Duel with { History = ImmutableList<RoundSummary>.Empty }, context)
                    : updated);

    private static Result<MatchState> ForfeitMatch(MatchRules rules, Duel duel, PlayerId quitter) =>
        RequireMember(duel, quitter)
            .Map(_ => (MatchState)new MatchOver(
                duel,
                new MatchWon(duel.OpponentOf(quitter).Id, MatchEndReason.Forfeit),
                ImmutableHashSet<PlayerId>.Empty) { Rules = rules });

    private static Result<PlayerInfo> RequireMember(Duel duel, PlayerId player) =>
        duel.Find(player).ToResult(GameErrors.NotInRoom);

    private static Result<MatchState> Ok(MatchState state) => new Success<MatchState>(state);

    private static Result<MatchState> Fail(GameError error) => new Failure<MatchState>(error);

    private static Maybe<MatchResult> Some(MatchResult result) => new Something<MatchResult>(result);
}
