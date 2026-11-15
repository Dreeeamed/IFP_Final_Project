using System.Collections.Immutable;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;

namespace WordDuel.Core.Words;

/// <summary>
/// A guess that has passed every validation rule. The constructor is internal, so the only way
/// to obtain one is through <see cref="GuessValidator"/>: holding a ValidGuess proves the word was
/// checked ("smart construction", Lecture 10 — like ValidSpend in the Martian Trail example).
/// </summary>
public sealed record ValidGuess
{
    internal ValidGuess(string word) => Word = word;

    public string Word { get; }

    public override string ToString() => Word;
}

/// <summary>
/// Pure word validation built from small first-class functions.
/// </summary>
public static class GuessValidator
{
    /// <summary>A validation rule is data: a predicate plus a function that builds the error.</summary>
    public sealed record Rule(string Name, Func<string, bool> Passes, Func<string, GameError> Error);

    /// <summary>
    /// Normalisation pipeline made with function composition (Lecture 4, Compose):
    /// null → "" → trimmed → upper-case. Nothing runs until the composed function is called.
    /// </summary>
    public static readonly Func<string?, string> Normalize =
        ((Func<string?, string>)(raw => raw ?? string.Empty))
            .Compose(text => text.Trim())
            .Compose(text => text.ToUpperInvariant());

    /// <summary>
    /// The ordered rule list. Note the method group <c>dictionary.IsAllowed</c> used directly
    /// as a <c>Func&lt;string, bool&gt;</c> — methods are first-class values (Lecture 2).
    /// </summary>
    public static ImmutableArray<Rule> RulesFor(WordDictionary dictionary) =>
    [
        new("not empty", word => word.Length > 0, _ => GameErrors.EmptyGuess),
        new("letters only", word => word.All(char.IsAsciiLetter), _ => GameErrors.NotLetters),
        new("five letters", word => word.Length == WordDictionary.WordLength, _ => GameErrors.WrongLength(WordDictionary.WordLength)),
        new("in word list", dictionary.IsAllowed, GameErrors.NotInDictionary)
    ];

    /// <summary>
    /// Higher-order validation: the first rule that fails decides the error; if none fails the
    /// word becomes a <see cref="ValidGuess"/>. The result is a discriminated union, not a bool.
    /// </summary>
    public static Result<ValidGuess> Validate(IReadOnlyList<Rule> rules, string? raw)
    {
        var word = Normalize(raw);
        return rules.FirstOrNothing(rule => !rule.Passes(word)) switch
        {
            Something<Rule> broken => new Failure<ValidGuess>(broken.Value.Error(word)),
            _ => new Success<ValidGuess>(new ValidGuess(word))
        };
    }

    public static Result<ValidGuess> Validate(WordDictionary dictionary, string? raw) =>
        Validate(RulesFor(dictionary), raw);

    /// <summary>
    /// Partial application (Lecture 7, "Validation: bind a rule set, leaving the candidate as the
    /// final input"): returns a ready-to-use validator with the rule list already fixed.
    /// </summary>
    public static Func<string?, Result<ValidGuess>> For(WordDictionary dictionary) =>
        ((Func<IReadOnlyList<Rule>, string?, Result<ValidGuess>>)Validate).Partial(RulesFor(dictionary));
}
