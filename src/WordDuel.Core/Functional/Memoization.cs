namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 8 — Memoization.
///
/// <c>Memoize</c> wraps a pure function so that the first call with a key computes the value
/// and later calls with the same key return the stored result. The cache is a dictionary
/// captured by the returned closure, so its lifetime is the lifetime of the delegate.
///
/// We create memoized delegates for one calculation only (one round analysis), which keeps the
/// key space bounded and lets the whole cache be garbage-collected afterwards
/// (Lecture 8: "Why is local memoization often safer?"). These delegates are therefore not
/// shared between threads and use a plain Dictionary.
///
/// Only memoize pure functions: a cache hit skips the function body entirely.
/// </summary>
public static class Memoization
{
    public static Func<TIn, TOut> Memoize<TIn, TOut>(this Func<TIn, TOut> function) where TIn : notnull
    {
        var cache = new Dictionary<TIn, TOut>();
        return input =>
        {
            if (cache.TryGetValue(input, out var value))
                return value;                  // hit: reuse the earlier result
            value = function(input);           // miss: compute once …
            cache[input] = value;              // … and remember it
            return value;
        };
    }

    /// <summary>
    /// Two-argument version. The key is a ValueTuple, which has structural equality and avoids
    /// fragile string keys such as $"{a},{b}" (Lecture 8, "Composite keys").
    /// </summary>
    public static Func<T1, T2, TOut> Memoize<T1, T2, TOut>(this Func<T1, T2, TOut> function)
    {
        var cache = new Dictionary<(T1, T2), TOut>();
        return (a, b) =>
        {
            var key = (a, b);
            if (!cache.TryGetValue(key, out var value))
                cache[key] = value = function(a, b);
            return value;
        };
    }
}
