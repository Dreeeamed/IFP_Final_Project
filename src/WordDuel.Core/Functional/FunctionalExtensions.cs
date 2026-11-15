namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 4 — Higher-order functions and combinators.
/// Each method accepts and/or returns a function (Func/Action), so behaviour
/// can be passed around and combined like data.
/// </summary>
public static class FunctionalExtensions
{
    /// <summary>
    /// Lecture 4's Map (a.k.a. pipe/chain): apply <paramref name="f"/> to a single value.
    /// Lets a multi-stage calculation read top-to-bottom without throwaway variables:
    /// <c>raw.Pipe(Trim).Pipe(ToUpper)</c>.
    /// It is named Pipe here because a generic Map on <i>any</i> T is ambiguous with
    /// Result.Map and Maybe.Map (Lecture 6, "Pragmatic naming choice").
    /// </summary>
    public static TOut Pipe<TIn, TOut>(this TIn value, Func<TIn, TOut> f) => f(value);

    /// <summary>
    /// Tap: run a side effect (logging, metrics) and return the original value unchanged,
    /// so it can sit in the middle of a pipeline. The side effect is deliberate and visible.
    /// </summary>
    public static T Tap<T>(this T value, Action<T> action)
    {
        action(value);
        return value;
    }

    /// <summary>
    /// Compose: build a new function (f then g) that runs <paramref name="first"/> and passes its
    /// output to <paramref name="next"/>. Nothing executes until the composed function is called.
    /// </summary>
    public static Func<TIn, TOut> Compose<TIn, TMiddle, TOut>(this Func<TIn, TMiddle> first, Func<TMiddle, TOut> next) =>
        input => next(first(input));

    /// <summary>
    /// Fork (a.k.a. converge): give the same input to two independent "prongs"
    /// and combine their results with <paramref name="join"/>.
    /// Example: average = values.Fork(Sum, Count, (sum, count) => sum / count).
    /// </summary>
    public static TOut Fork<TIn, T1, T2, TOut>(
        this TIn value,
        Func<TIn, T1> first,
        Func<TIn, T2> second,
        Func<T1, T2, TOut> join) =>
        join(first(value), second(value));
}
