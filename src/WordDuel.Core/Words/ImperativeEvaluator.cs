namespace WordDuel.Core.Words;

/// <summary>
/// Lecture 9 — an imperative baseline used ONLY for benchmarking and for a cross-check test.
/// It mutates local arrays (no LINQ, no immutable collections) and must always agree with
/// <see cref="WordleEvaluator"/>; the game itself uses the functional version.
/// </summary>
public static class ImperativeEvaluator
{
    public static FeedbackPattern Evaluate(string secret, string guess)
    {
        Span<int> available = stackalloc int[26];
        Span<int> states = stackalloc int[WordDictionary.WordLength];

        for (var i = 0; i < guess.Length; i++)
        {
            if (secret[i] == guess[i]) states[i] = (int)LetterState.Correct;
            else available[secret[i] - 'A']++;
        }

        for (var i = 0; i < guess.Length; i++)
        {
            if (states[i] == (int)LetterState.Correct) continue;
            var letter = guess[i] - 'A';
            if (available[letter] > 0)
            {
                states[i] = (int)LetterState.Present;
                available[letter]--;
            }
        }

        var code = 0;
        for (var i = 0; i < guess.Length; i++)
            code = code * 3 + states[i];
        return new FeedbackPattern(code);
    }

    /// <summary>Imperative candidate filter: a foreach loop and a mutable list.</summary>
    public static List<string> Filter(IReadOnlyList<string> pool, string guess, FeedbackPattern observed)
    {
        var kept = new List<string>();
        foreach (var candidate in pool)
        {
            if (Evaluate(candidate, guess) == observed)
                kept.Add(candidate);
        }
        return kept;
    }
}
