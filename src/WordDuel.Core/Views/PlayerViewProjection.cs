using System.Collections.Immutable;
using WordDuel.Core.Analysis;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;

namespace WordDuel.Core.Views;

/// <summary>Shell-owned facts the projection needs, passed in explicitly.</summary>
public sealed record ProjectionContext(
    string RoomCode,
    DateTimeOffset Now,
    IReadOnlyDictionary<PlayerId, Presence> Presence,
    IReadOnlyDictionary<int, RoundAnalysis> Analyses);

/// <summary>
/// The Blind Duel rule, as one pure function: (match state, viewer) → what that viewer may see.
///
/// While a round is active the opponent appears only as name, presence, attempts used and a
/// finished flag. The secret word and the opponent's guesses/feedback exist only in
/// <see cref="RoundSummary"/> values, i.e. after the round has ended for both players.
/// Because it is pure, the rule is verified directly by unit tests.
/// </summary>
public static class PlayerViewProjection
{
    public static PlayerView Project(MatchState state, PlayerId viewer, ProjectionContext context) => state switch
    {
        WaitingForOpponent waiting => Waiting(waiting, viewer, context),
        RoundInProgress playing => Playing(playing, viewer, context),
        BetweenRounds pause => Between(pause, viewer, context),
        MatchOver over => Over(over, viewer, context),
        _ => throw new InvalidOperationException("Unknown match state")
    };

    private static PlayerView Waiting(WaitingForOpponent waiting, PlayerId viewer, ProjectionContext context) =>
        Base(context, MatchPhase.WaitingForOpponent,
            me: new SelfView(viewer.Value, waiting.Host.Nickname, null),
            opponent: null,
            score: new ScoreView(0, 0, 0, waiting.Rules.WinsNeeded, waiting.Rules.MaxRounds, []));

    private static PlayerView Playing(RoundInProgress playing, PlayerId viewer, ProjectionContext context)
    {
        var duel = playing.Duel;
        var opponent = duel.OpponentOf(viewer);
        var myRound = playing.Round.Boards[viewer];
        var theirRound = playing.Round.Boards[opponent.Id];

        return Base(context, MatchPhase.Playing,
                me: new SelfView(viewer.Value, duel.Find(viewer).Map(p => p.Nickname).OrElse(""), BoardView.From(myRound.Board)),
                // Blind Duel: counts and flags only — theirRound.Board.Guesses is deliberately not read.
                opponent: new OpponentView(opponent.Nickname, PresenceOf(opponent.Id, context), theirRound.Board.AttemptsUsed, theirRound.Board.MaxGuesses, theirRound.Board.IsFinished),
                score: Score(playing.Rules, duel, viewer))
            with
            {
                RoundNumber = playing.Round.Number,
                RoundDeadline = playing.Round.Deadline
            };
    }

    private static PlayerView Between(BetweenRounds pause, PlayerId viewer, ProjectionContext context)
    {
        var opponent = pause.Duel.OpponentOf(viewer);
        return Revealed(pause.Duel, pause.Rules, viewer, context, MatchPhase.RoundOver) with
        {
            NextRoundAt = pause.NextRoundAt,
            MeReady = pause.Ready.Contains(viewer),
            OpponentReady = pause.Ready.Contains(opponent.Id)
        };
    }

    private static PlayerView Over(MatchOver over, PlayerId viewer, ProjectionContext context)
    {
        var opponent = over.Duel.OpponentOf(viewer);
        var verdict = over.Result switch
        {
            MatchWon won when won.Winner == viewer => Verdict.Won,
            MatchWon => Verdict.Lost,
            _ => Verdict.Draw
        };
        var reason = over.Result is MatchWon w ? w.Reason : (MatchEndReason?)null;
        var rematchPossible = reason is not MatchEndReason.Forfeit && PresenceOf(opponent.Id, context) is Presence.Connected;

        return Revealed(over.Duel, over.Rules, viewer, context, MatchPhase.MatchOver) with
        {
            Match = new MatchResultView(verdict, reason, MatchStatistics.For(over.Duel, viewer), MatchStatistics.For(over.Duel, opponent.Id)),
            MeVotedRematch = over.RematchVotes.Contains(viewer),
            OpponentVotedRematch = over.RematchVotes.Contains(opponent.Id),
            RematchAvailable = rematchPossible
        };
    }

    /// <summary>Shared projection for phases in which the last round is over and may be revealed.</summary>
    private static PlayerView Revealed(Duel duel, MatchRules rules, PlayerId viewer, ProjectionContext context, MatchPhase phase)
    {
        var opponent = duel.OpponentOf(viewer);
        var last = duel.History.LastOrDefault();
        var me = duel.Find(viewer).Map(p => p.Nickname).OrElse("");

        return Base(context, phase,
                me: new SelfView(viewer.Value, me, last is null ? null : BoardView.From(last.Boards[viewer].Board)),
                opponent: new OpponentView(opponent.Nickname, PresenceOf(opponent.Id, context),
                    last?.Boards[opponent.Id].Board.AttemptsUsed ?? 0, rules.MaxGuesses, Finished: true),
                score: Score(rules, duel, viewer))
            with
            {
                RoundNumber = last?.Number ?? 0,
                LastRound = last is null ? null : Reveal(duel, last, viewer, context)
            };
    }

    private static RoundRevealView Reveal(Duel duel, RoundSummary round, PlayerId viewer, ProjectionContext context)
    {
        var opponent = duel.OpponentOf(viewer);
        var analysis = context.Analyses.TryGetValue(round.Number, out var found)
            ? new AnalysisView(found.PoolSize, found.For(viewer), found.For(opponent.Id), found.Highlights)
            : null;

        return new RoundRevealView(
            round.Number,
            round.Secret,
            VerdictFor(round.Outcome, viewer),
            RoundScoring.Describe(round.Outcome),
            round.EndReason,
            (round.EndedAt - round.StartedAt).TotalSeconds,
            PlayerRound(duel.Find(viewer).Map(p => p.Nickname).OrElse(""), round, viewer),
            PlayerRound(opponent.Nickname, round, opponent.Id),
            analysis);
    }

    private static PlayerRoundView PlayerRound(string nickname, RoundSummary round, PlayerId player)
    {
        var record = round.Results[player];
        return new PlayerRoundView(nickname, BoardView.From(round.Boards[player].Board), record.Solved, record.Attempts, record.SolveTime?.TotalSeconds);
    }

    private static ScoreView Score(MatchRules rules, Duel duel, PlayerId viewer) => new(
        duel.WinsOf(viewer),
        duel.WinsOf(duel.OpponentOf(viewer).Id),
        duel.Draws,
        rules.WinsNeeded,
        rules.MaxRounds,
        duel.History.Select(round => VerdictFor(round.Outcome, viewer)).ToImmutableArray());

    private static Verdict VerdictFor(RoundOutcome outcome, PlayerId viewer) => outcome switch
    {
        RoundWon won when won.Winner == viewer => Verdict.Won,
        RoundWon => Verdict.Lost,
        _ => Verdict.Draw
    };

    private static Presence PresenceOf(PlayerId player, ProjectionContext context) =>
        context.Presence.TryGetValue(player, out var presence) ? presence : Presence.Disconnected;

    private static PlayerView Base(ProjectionContext context, MatchPhase phase, SelfView me, OpponentView? opponent, ScoreView score) => new(
        RoomCode: context.RoomCode,
        Phase: phase,
        ServerTime: context.Now,
        Me: me,
        Opponent: opponent,
        Score: score,
        RoundNumber: 0,
        RoundDeadline: null,
        NextRoundAt: null,
        MeReady: false,
        OpponentReady: false,
        MeVotedRematch: false,
        OpponentVotedRematch: false,
        RematchAvailable: false,
        LastRound: null,
        Match: null);
}
