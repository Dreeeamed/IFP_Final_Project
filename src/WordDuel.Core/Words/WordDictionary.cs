using System.Collections.Frozen;
using System.Collections.Immutable;

namespace WordDuel.Core.Words;

/// <summary>
/// The immutable word lists used by the game.
/// <list type="bullet">
/// <item><see cref="Answers"/>: possible secret words (also the analyzer's candidate pool).</item>
/// <item>Allowed guesses: every answer plus a larger list of valid English words.</item>
/// </list>
/// Lookups use a <see cref="FrozenSet{T}"/>, .NET's read-only set optimised for fast reads.
/// </summary>
public sealed class WordDictionary
{
    public const int WordLength = 5;

    private readonly FrozenSet<string> _allowed;
    private readonly FrozenSet<string> _answers;

    private WordDictionary(ImmutableArray<string> answers, FrozenSet<string> allowed)
    {
        Answers = answers;
        _answers = answers.ToFrozenSet(StringComparer.Ordinal);
        _allowed = allowed;
    }

    /// <summary>Possible secret words in alphabetical order.</summary>
    public ImmutableArray<string> Answers { get; }

    public int AllowedCount => _allowed.Count;

    /// <summary>
    /// Builds a dictionary from raw word sequences with a LINQ cleaning pipeline:
    /// normalise → keep well-formed words → remove duplicates. Answers are always allowed guesses.
    /// </summary>
    public static WordDictionary Create(IEnumerable<string> answers, IEnumerable<string> allowedGuesses)
    {
        var answerList = Clean(answers).Order(StringComparer.Ordinal).ToImmutableArray();
        var allowed = Clean(allowedGuesses).Concat(answerList).ToFrozenSet(StringComparer.Ordinal);
        return new WordDictionary(answerList, allowed);
    }

    public bool IsAllowed(string word) => _allowed.Contains(word);

    public bool IsAnswer(string word) => _answers.Contains(word);

    /// <summary>Exactly five upper-case ASCII letters.</summary>
    public static bool IsWellFormed(string word) =>
        word.Length == WordLength && word.All(char.IsAsciiLetterUpper);

    private static IEnumerable<string> Clean(IEnumerable<string> words) =>
        words.Select(word => word.Trim().ToUpperInvariant())
             .Where(IsWellFormed)
             .Distinct(StringComparer.Ordinal);
}
