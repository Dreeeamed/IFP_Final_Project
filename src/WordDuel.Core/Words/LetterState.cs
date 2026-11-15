using System.Collections.Immutable;

namespace WordDuel.Core.Words;

/// <summary>
/// Feedback for one letter of a guess. The numeric order matters: a higher value is
/// "better" information, which lets the keyboard pick the best state with Max().
/// </summary>
public enum LetterState
{
    /// <summary>Gray: no unmatched copy of this letter is left in the secret.</summary>
    Absent = 0,

    /// <summary>Yellow: the letter is in the secret, but in another position.</summary>
    Present = 1,

    /// <summary>Green: right letter, right position.</summary>
    Correct = 2
}

/// <summary>
/// A whole feedback row packed into one integer (each letter is a base-3 digit).
/// Value equality and cheap hashing make it a good dictionary key for the analyzer.
/// </summary>
public readonly record struct FeedbackPattern(int Code)
{
    /// <summary>Folds the states into one number with Aggregate: code = code * 3 + state.</summary>
    public static FeedbackPattern From(IEnumerable<LetterState> states) =>
        new(states.Aggregate(0, (code, state) => code * 3 + (int)state));

    /// <summary>Decodes the pattern back into per-letter states.</summary>
    public ImmutableArray<LetterState> ToStates(int length)
    {
        var code = Code; // lambdas in a struct cannot capture `this`
        return Enumerable.Range(0, length)
            .Select(position => (LetterState)(code / Pow3(length - 1 - position) % 3))
            .ToImmutableArray();
    }

    private static int Pow3(int exponent) => Enumerable.Repeat(3, exponent).Aggregate(1, (acc, x) => acc * x);
}
