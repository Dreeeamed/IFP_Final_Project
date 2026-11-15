namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 6 — Functional flow for <see cref="Maybe{T}"/>.
/// Map lifts a plain function into the Maybe context; Bind chains a function that
/// itself returns a Maybe and flattens the result (no Maybe&lt;Maybe&lt;T&gt;&gt;).
/// Nothing short-circuits: later lambdas are skipped ("railway-oriented programming").
/// </summary>
public static class MaybeExtensions
{
    public static Maybe<TOut> Map<TIn, TOut>(this Maybe<TIn> source, Func<TIn, TOut> f) => source switch
    {
        Something<TIn> s => new Something<TOut>(f(s.Value)),
        _ => new Nothing<TOut>()
    };

    public static Maybe<TOut> Bind<TIn, TOut>(this Maybe<TIn> source, Func<TIn, Maybe<TOut>> f) => source switch
    {
        Something<TIn> s => f(s.Value),
        _ => new Nothing<TOut>()
    };

    /// <summary>Leaves the Maybe world: choose a value for each case (pattern matching at the boundary).</summary>
    public static TOut Match<TIn, TOut>(this Maybe<TIn> source, Func<TIn, TOut> something, Func<TOut> nothing) => source switch
    {
        Something<TIn> s => something(s.Value),
        _ => nothing()
    };

    public static T OrElse<T>(this Maybe<T> source, T fallback) => source is Something<T> s ? s.Value : fallback;

    /// <summary>Lecture 6 OnSomething: run an action for a present value and return the Maybe unchanged.</summary>
    public static Maybe<T> OnSomething<T>(this Maybe<T> source, Action<T> action)
    {
        if (source is Something<T> s) action(s.Value);
        return source;
    }

    /// <summary>Converts absence into a failure with a meaningful reason.</summary>
    public static Result<T> ToResult<T>(this Maybe<T> source, Common.GameError errorIfNothing) => source switch
    {
        Something<T> s => new Success<T>(s.Value),
        _ => new Failure<T>(errorIfNothing)
    };

    /// <summary>Safe dictionary lookup: a missing key is Nothing instead of an exception.</summary>
    public static Maybe<TValue> Find<TKey, TValue>(this IReadOnlyDictionary<TKey, TValue> dictionary, TKey key) =>
        dictionary.TryGetValue(key, out var value) ? new Something<TValue>(value) : new Nothing<TValue>();

    /// <summary>The first element that satisfies <paramref name="predicate"/>, or Nothing (no null, no exception).</summary>
    public static Maybe<T> FirstOrNothing<T>(this IEnumerable<T> source, Func<T, bool> predicate)
    {
        foreach (var item in source)
        {
            if (predicate(item)) return new Something<T>(item);
        }
        return new Nothing<T>();
    }

    /// <summary>Keeps only the values of Something cases (the Maybe analogue of LINQ's OfType).</summary>
    public static IEnumerable<T> Values<T>(this IEnumerable<Maybe<T>> source) =>
        source.OfType<Something<T>>().Select(s => s.Value);
}
