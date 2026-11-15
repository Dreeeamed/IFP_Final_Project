using System.Collections.Immutable;

namespace WordDuel.Core.Words;

/// <summary>
/// The heart of the game: a deterministic, pure guess-evaluation function (Lecture 1/2).
/// Same secret + same guess → always the same feedback; no state is read or changed.
/// </summary>
public static class WordleEvaluator
{
    /// <summary>
    /// Scores <paramref name="guess"/> against <paramref name="secret"/> (both upper-case, equal length).
    ///
    /// Repeated letters follow the official rules:
    ///   1. Greens are assigned first, position by position.
    ///   2. Every secret letter that was NOT matched green is "available" once for a yellow.
    ///   3. Walking the guess left to right, a non-green letter is yellow only while an
    ///      available copy remains; each yellow consumes one copy. Otherwise it is gray.
    ///
    /// Example: secret CRANE, guess EERIE → ⬛⬛🟨⬛🟩 (the only E is already used by the green).
    /// </summary>
    public static ImmutableArray<LetterState> Evaluate(string secret, string guess)
    {
        ArgumentNullException.ThrowIfNull(secret);
        ArgumentNullException.ThrowIfNull(guess);
        if (secret.Length != guess.Length)
            throw new ArgumentException("Secret and guess must have the same length.", nameof(guess));

        // Pass 1 — greens: Zip pairs the letters position by position.
        var isExact = secret.Zip(guess, (s, g) => s == g).ToImmutableArray();

        // How many copies of each secret letter are still available for yellows.
        var available = secret
            .Where((_, index) => !isExact[index])
            .GroupBy(letter => letter)
            .ToImmutableDictionary(group => group.Key, group => group.Count());

        // Pass 2 — Aggregate threads an immutable accumulator through the guess, left to right.
        var start = new Walk(ImmutableArray<LetterState>.Empty, available);
        return guess
            .Select((letter, index) => (letter, exact: isExact[index]))
            .Aggregate(start, (walk, x) => walk.Next(x.letter, x.exact))
            .States;
    }

    /// <summary>Accumulator for pass 2: states produced so far and the copies still available.</summary>
    private sealed record Walk(ImmutableArray<LetterState> States, ImmutableDictionary<char, int> Available)
    {
        public Walk Next(char letter, bool exact) => (exact, Available.GetValueOrDefault(letter)) switch
        {
            (true, _) => this with { States = States.Add(LetterState.Correct) },
            (false, > 0) => new Walk(States.Add(LetterState.Present), Available.SetItem(letter, Available[letter] - 1)),
            _ => this with { States = States.Add(LetterState.Absent) }
        };
    }
}
