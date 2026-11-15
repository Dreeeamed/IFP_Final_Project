using WordDuel.Core.Players;
using WordDuel.Core.Words;

namespace WordDuel.Core.Match;

/// <summary>
/// Everything that can happen to a match, as a closed discriminated union.
/// The server translates network calls and timer ticks into these commands; the pure
/// <see cref="MatchEngine"/> decides what they mean (Lecture 10, "Command model").
/// </summary>
public abstract record MatchCommand
{
    private protected MatchCommand() { }
}

public sealed record OpponentJoins(PlayerInfo Opponent) : MatchCommand;

/// <summary>A guess that has already passed validation (the type guarantees it).</summary>
public sealed record SubmitGuess(PlayerId Player, ValidGuess Guess) : MatchCommand;

public sealed record MarkReady(PlayerId Player) : MatchCommand;

public sealed record VoteRematch(PlayerId Player) : MatchCommand;

/// <summary>A player left or did not come back after a disconnect.</summary>
public sealed record Forfeit(PlayerId Player) : MatchCommand;

/// <summary>Time has passed; deadlines may have expired.</summary>
public sealed record ClockTick : MatchCommand
{
    public static ClockTick Instance { get; } = new();
}

/// <summary>
/// Values from the outside world that a transition may need, passed in explicitly so the
/// engine stays pure: the current server time and a freshly drawn secret word that is used
/// only if this transition starts a new round. (It plays the role of the Reader monad's
/// environment from Lecture 6, implemented as an ordinary parameter for clarity.)
/// </summary>
public sealed record TransitionContext(DateTimeOffset Now, string NextSecret);
