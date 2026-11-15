namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 7 — Currying and partial application.
///
/// Currying turns f(a, b, c) into a chain of one-argument functions a → b → c → result.
/// Partial application fixes some leading arguments at once and returns a function of the rest.
/// Both create specialised functions from one general implementation; the stored arguments
/// live in a closure (Lecture 4, MakeAddFunc).
///
/// C# does not curry automatically, so we provide only the arities the project actually uses
/// (Lecture 7: "Add only the combinations the application actually uses").
/// </summary>
public static class Currying
{
    public static Func<T1, Func<T2, TOut>> Curry<T1, T2, TOut>(this Func<T1, T2, TOut> f) =>
        a => b => f(a, b);

    public static Func<T1, Func<T2, Func<T3, TOut>>> Curry<T1, T2, T3, TOut>(this Func<T1, T2, T3, TOut> f) =>
        a => b => c => f(a, b, c);

    /// <summary>Two arguments → one: fix the first argument.</summary>
    public static Func<T2, TOut> Partial<T1, T2, TOut>(this Func<T1, T2, TOut> f, T1 first) =>
        second => f(first, second);

    /// <summary>Three arguments → one: fix the first two arguments together.</summary>
    public static Func<T3, TOut> Partial<T1, T2, T3, TOut>(this Func<T1, T2, T3, TOut> f, T1 first, T2 second) =>
        third => f(first, second, third);
}
