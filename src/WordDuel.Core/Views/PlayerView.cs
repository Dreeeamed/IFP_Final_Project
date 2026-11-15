using WordDuel.Core.Analysis;
using WordDuel.Core.Match;
using WordDuel.Core.Words;

namespace WordDuel.Core.Views;

// Read models sent from the server to ONE player. They are plain, serialisable records.
// Every view is produced by PlayerViewProjection, the single place that decides what a
// player is allowed to see (Blind Duel).

public enum MatchPhase
{
    WaitingForOpponent,
    Playing,
    RoundOver,
    MatchOver
}

/// <summary>Result from the viewer's point of view.</summary>
public enum Verdict
{
    Won,
    Lost,
    Draw
}

public enum Presence
{
    Connected,
    Disconnected,
    Left
}

public sealed record GuessView(string Word, IReadOnlyList<LetterState> States);

public sealed record BoardView(IReadOnlyList<GuessView> Guesses, int MaxGuesses, BoardStatus Status)
{
    public static BoardView From(PlayerBoard board) => new(
        board.Guesses.Select(guess => new GuessView(guess.Word, guess.States)).ToArray(),
        board.MaxGuesses,
        board.Status);
}

public sealed record SelfView(string PlayerId, string Nickname, BoardView? Board);

/// <summary>
/// Everything a player may know about the opponent while a round is active:
/// name, connection, how many attempts were used and whether they are done. Never the words.
/// </summary>
public sealed record OpponentView(string Nickname, Presence Presence, int AttemptsUsed, int MaxGuesses, bool Finished);

public sealed record ScoreView(int MyWins, int OpponentWins, int Draws, int WinsNeeded, int MaxRounds, IReadOnlyList<Verdict> Rounds);

public sealed record PlayerRoundView(string Nickname, BoardView Board, bool Solved, int Attempts, double? SolveSeconds);

public sealed record AnalysisView(int PoolSize, PlayerStrategy Me, PlayerStrategy Opponent, IReadOnlyList<string> Highlights);

/// <summary>A completed round: only now are the secret and both boards revealed.</summary>
public sealed record RoundRevealView(
    int Number,
    string Secret,
    Verdict Verdict,
    string Explanation,
    RoundEndReason EndReason,
    double DurationSeconds,
    PlayerRoundView Me,
    PlayerRoundView Opponent,
    AnalysisView? Analysis);

public sealed record MatchResultView(Verdict Verdict, MatchEndReason? Reason, PlayerMatchStats MyStats, PlayerMatchStats OpponentStats);

/// <summary>The complete, personalised snapshot of a room. Each update replaces the previous one.</summary>
public sealed record PlayerView(
    string RoomCode,
    MatchPhase Phase,
    DateTimeOffset ServerTime,
    SelfView Me,
    OpponentView? Opponent,
    ScoreView Score,
    int RoundNumber,
    DateTimeOffset? RoundDeadline,
    DateTimeOffset? NextRoundAt,
    bool MeReady,
    bool OpponentReady,
    bool MeVotedRematch,
    bool OpponentVotedRematch,
    bool RematchAvailable,
    RoundRevealView? LastRound,
    MatchResultView? Match);
