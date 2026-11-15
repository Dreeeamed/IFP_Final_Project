using System.Collections.Immutable;
using WordDuel.Core.Functional;
using WordDuel.Core.Players;
using WordDuel.Core.Words;

namespace WordDuel.Core.Match;

/// <summary>Configurable match rules. The defaults are "Best of 3".</summary>
public sealed record MatchRules
{
    /// <summary>Round wins needed to take the match (2 = best of 3).</summary>
    public int WinsNeeded { get; init; } = 2;

    /// <summary>Draws do not score, so extra rounds may be played — but never more than this.</summary>
    public int MaxRounds { get; init; } = 5;

    public int MaxGuesses { get; init; } = PlayerBoard.DefaultMaxGuesses;

    /// <summary>Hard time limit per round, enforced by the server clock.</summary>
    public TimeSpan RoundDuration { get; init; } = TimeSpan.FromMinutes(5);

    /// <summary>Pause between rounds; the next round starts early when both players are ready.</summary>
    public TimeSpan BreakDuration { get; init; } = TimeSpan.FromSeconds(45);

    public static MatchRules BestOfThree { get; } = new();
}

/// <summary>One player's state inside a round. FinishedAt is set by the server when the board finishes.</summary>
public sealed record PlayerRound(PlayerBoard Board, DateTimeOffset? FinishedAt)
{
    public static PlayerRound Start(int maxGuesses) => new(PlayerBoard.Empty(maxGuesses), null);
}

/// <summary>The round currently being played. The secret never leaves the server while this exists.</summary>
public sealed record ActiveRound(
    int Number,
    string Secret,
    DateTimeOffset StartedAt,
    DateTimeOffset Deadline,
    ImmutableDictionary<PlayerId, PlayerRound> Boards)
{
    public bool AllFinished => Boards.Values.All(round => round.Board.IsFinished);
}

public enum RoundEndReason
{
    AllFinished,
    TimeExpired
}

/// <summary>An immutable record of a finished round: everything needed for the reveal and the analysis.</summary>
public sealed record RoundSummary(
    int Number,
    string Secret,
    RoundOutcome Outcome,
    RoundEndReason EndReason,
    DateTimeOffset StartedAt,
    DateTimeOffset EndedAt,
    ImmutableDictionary<PlayerId, PlayerRound> Boards,
    ImmutableDictionary<PlayerId, CompletionRecord> Results);

/// <summary>The two players and the rounds they have finished. The score is derived, never stored.</summary>
public sealed record Duel(PlayerInfo First, PlayerInfo Second, ImmutableList<RoundSummary> History)
{
    public ImmutableArray<PlayerInfo> Players => [First, Second];

    public bool Contains(PlayerId id) => First.Id == id || Second.Id == id;

    public PlayerInfo OpponentOf(PlayerId id) => First.Id == id ? Second : First;

    public Maybe<PlayerInfo> Find(PlayerId id) => Players.FirstOrNothing(player => player.Id == id);

    /// <summary>Single source of truth: wins are counted from the history with LINQ.</summary>
    public int WinsOf(PlayerId id) => History.Count(round => round.Outcome is RoundWon won && won.Winner == id);

    public int Draws => History.Count(round => round.Outcome is RoundDrawn);

    public int RoundsPlayed => History.Count;
}

public enum MatchEndReason
{
    ReachedWinTarget,
    RoundLimit,
    Forfeit
}

/// <summary>Discriminated union: how a match ended.</summary>
public abstract record MatchResult
{
    private protected MatchResult() { }
}

public sealed record MatchWon(PlayerId Winner, MatchEndReason Reason) : MatchResult;

public sealed record MatchDrawn : MatchResult;

/// <summary>
/// The match state machine as a closed discriminated union (Lecture 5).
/// Each case carries only the data that is meaningful in that phase, so impossible
/// combinations (e.g. "waiting for opponent" with a score) cannot be represented.
/// </summary>
public abstract record MatchState
{
    private protected MatchState() { }

    public required MatchRules Rules { get; init; }
}

/// <summary>The host has created the room; nobody has joined yet.</summary>
public sealed record WaitingForOpponent(PlayerInfo Host) : MatchState;

/// <summary>A round is being played by both players.</summary>
public sealed record RoundInProgress(Duel Duel, ActiveRound Round) : MatchState;

/// <summary>The last round has been revealed; the next one starts when both are ready or the break ends.</summary>
public sealed record BetweenRounds(Duel Duel, DateTimeOffset NextRoundAt, ImmutableHashSet<PlayerId> Ready) : MatchState;

/// <summary>The match is decided; both players may vote for a rematch.</summary>
public sealed record MatchOver(Duel Duel, MatchResult Result, ImmutableHashSet<PlayerId> RematchVotes) : MatchState;
