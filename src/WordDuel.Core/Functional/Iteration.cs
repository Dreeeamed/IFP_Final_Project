namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 7 — Indefinite loops and iterators.
/// Successive states are produced lazily by iterator methods (<c>yield return</c>), so a
/// consumer decides when to stop with a terminating LINQ operation such as Take or First.
/// </summary>
public static class Iteration
{
    /// <summary>
    /// An infinite, lazy sequence: next(), next(), next() …
    /// Safe only when consumed with a terminating operator (Take/First), e.g. generating
    /// random room codes until a free one appears, with a hard upper bound.
    /// </summary>
    public static IEnumerable<T> Generate<T>(Func<T> next)
    {
        while (true)
            yield return next();
    }

    /// <summary>
    /// Scan: like Aggregate, but yields every intermediate accumulator, not just the last one.
    /// The analyzer uses it to get the candidate pool after each guess.
    /// The seed is yielded first, so the result has one more element than the source.
    /// </summary>
    public static IEnumerable<TState> Scan<T, TState>(this IEnumerable<T> source, TState seed, Func<TState, T, TState> step)
    {
        var state = seed;
        yield return state;
        foreach (var item in source)
        {
            state = step(state, item);
            yield return state;
        }
    }
}
