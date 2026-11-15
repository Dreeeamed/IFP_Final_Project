using System.Collections.Immutable;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;

namespace WordDuel.Core.Words;

/// <summary>One submitted guess together with the feedback the server calculated for it.</summary>
public sealed record GuessResult(string Word, ImmutableArray<LetterState> States)
{
    public bool IsCorrect => States.All(state => state == LetterState.Correct);

    public FeedbackPattern Pattern => FeedbackPattern.From(States);

    public static GuessResult Evaluate(string secret, ValidGuess guess) =>
        new(guess.Word, WordleEvaluator.Evaluate(secret, guess.Word));

    // ImmutableArray compares by reference, so equality is defined by content explicitly.
    public bool Equals(GuessResult? other) =>
        other is not null && Word == other.Word && States.SequenceEqual(other.States);

    public override int GetHashCode() => HashCode.Combine(Word, Pattern.Code);
}

public enum BoardStatus
{
    InProgress,
    Solved,
    OutOfGuesses
}

/// <summary>
/// A player's board for one round. Immutable: submitting a guess returns a new board
/// (Lecture 3.3, "with" expressions) and the previous board is left untouched.
/// </summary>
public sealed record PlayerBoard(ImmutableList<GuessResult> Guesses, int MaxGuesses)
{
    public const int DefaultMaxGuesses = 6;

    public static PlayerBoard Empty(int maxGuesses = DefaultMaxGuesses) => new(ImmutableList<GuessResult>.Empty, maxGuesses);

    public int AttemptsUsed => Guesses.Count;

    public int AttemptsLeft => Math.Max(0, MaxGuesses - AttemptsUsed);

    /// <summary>
    /// Status derived from the guesses with a C# 11 list pattern (Lecture 3.3):
    /// "the last guess is all green" means solved.
    /// </summary>
    public BoardStatus Status => Guesses switch
    {
        [.., { IsCorrect: true }] => BoardStatus.Solved,
        { Count: var used } when used >= MaxGuesses => BoardStatus.OutOfGuesses,
        _ => BoardStatus.InProgress
    };

    public bool IsFinished => Status is not BoardStatus.InProgress;

    public bool HasGuessed(string word) => Guesses.Any(guess => guess.Word == word);

    /// <summary>
    /// Pure state transition: (board, secret, guess) → new board, or the reason the guess is refused.
    /// Repeating a word is refused instead of silently costing an attempt; this also makes
    /// accidental double submissions (double-click, network retry) harmless.
    /// </summary>
    public Result<PlayerBoard> Submit(string secret, ValidGuess guess) => this switch
    {
        { IsFinished: true } => new Failure<PlayerBoard>(GameErrors.BoardFinished),
        _ when HasGuessed(guess.Word) => new Failure<PlayerBoard>(GameErrors.AlreadyGuessed(guess.Word)),
        _ => new Success<PlayerBoard>(this with { Guesses = Guesses.Add(GuessResult.Evaluate(secret, guess)) })
    };
}

/// <summary>Keyboard colouring derived from a board (pure LINQ, no mutable bookkeeping).</summary>
public static class KeyboardState
{
    /// <summary>
    /// The best state seen for every letter: Correct beats Present beats Absent.
    /// SelectMany flattens all (letter, state) pairs, GroupBy collects them per letter, Max picks the best.
    /// </summary>
    public static ImmutableDictionary<char, LetterState> From(IEnumerable<GuessResult> guesses) =>
        guesses
            .SelectMany(guess => guess.Word.Zip(guess.States, (letter, state) => (letter, state)))
            .GroupBy(pair => pair.letter)
            .ToImmutableDictionary(group => group.Key, group => group.Max(pair => pair.state));
}
