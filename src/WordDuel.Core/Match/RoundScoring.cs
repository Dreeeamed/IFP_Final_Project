using WordDuel.Core.Players;

namespace WordDuel.Core.Match;

/// <summary>
/// A validated, server-produced record of how one player finished a round.
/// <see cref="SolveTime"/> is measured by the server clock from the round start; it is null when unsolved.
/// </summary>
public sealed record CompletionRecord(PlayerId Player, bool Solved, int Attempts, TimeSpan? SolveTime);

public enum WinReason
{
    OnlySolver,
    FewerGuesses,
    FasterSolve
}

public enum DrawReason
{
    NobodySolved,
    IdenticalResult
}

/// <summary>Discriminated union (Lecture 5): a round is either won by someone or drawn.</summary>
public abstract record RoundOutcome
{
    private protected RoundOutcome() { }
}

public sealed record RoundWon(PlayerId Winner, WinReason Reason) : RoundOutcome;

public sealed record RoundDrawn(DrawReason Reason) : RoundOutcome;

/// <summary>Pure scoring rules for a single round.</summary>
public static class RoundScoring
{
    /// <summary>
    /// Decides a round from two completion records with a tuple switch expression (Lecture 1/3.3).
    /// The arms are ordered from most to least specific:
    ///   1. only one player solved → that player wins;
    ///   2. nobody solved → draw;
    ///   3. both solved → fewer guesses wins, then the faster server-measured time, else a draw.
    /// </summary>
    public static RoundOutcome Decide(CompletionRecord a, CompletionRecord b) => (a, b) switch
    {
        ({ Solved: true }, { Solved: false }) => new RoundWon(a.Player, WinReason.OnlySolver),
        ({ Solved: false }, { Solved: true }) => new RoundWon(b.Player, WinReason.OnlySolver),
        ({ Solved: false }, { Solved: false }) => new RoundDrawn(DrawReason.NobodySolved),
        _ when a.Attempts != b.Attempts => new RoundWon(a.Attempts < b.Attempts ? a.Player : b.Player, WinReason.FewerGuesses),
        _ when a.SolveTime != b.SolveTime => new RoundWon(a.SolveTime < b.SolveTime ? a.Player : b.Player, WinReason.FasterSolve),
        _ => new RoundDrawn(DrawReason.IdenticalResult)
    };

    /// <summary>Human-readable explanation used by the result screens.</summary>
    public static string Describe(RoundOutcome outcome) => outcome switch
    {
        RoundWon { Reason: WinReason.OnlySolver } => "Only one player found the word",
        RoundWon { Reason: WinReason.FewerGuesses } => "Solved in fewer guesses",
        RoundWon { Reason: WinReason.FasterSolve } => "Same number of guesses — solved faster",
        RoundDrawn { Reason: DrawReason.NobodySolved } => "Nobody found the word",
        RoundDrawn { Reason: DrawReason.IdenticalResult } => "Identical guesses and time",
        _ => "Round finished"
    };
}
