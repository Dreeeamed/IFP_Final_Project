using WordDuel.Core.Common;

namespace WordDuel.Core.Functional;

/// <summary>
/// Lecture 6 — Monadic composition for <see cref="Result{T}"/>.
///
///   Map  : Result&lt;A&gt; + (A → B)          → Result&lt;B&gt;   (plain transformation)
///   Bind : Result&lt;A&gt; + (A → Result&lt;B&gt;) → Result&lt;B&gt;   (step that can fail; flattens)
///
/// A Failure skips every later Map/Bind and travels unchanged to the end of the
/// pipeline, where the imperative shell pattern-matches it into a user message.
///
/// Design note: unlike the lecture's Bind, these methods do not catch exceptions.
/// In our core an exception means a programming error, which should stay loud.
/// Code at the boundaries that calls something that may throw uses <see cref="Try{T}"/>.
/// </summary>
public static class ResultExtensions
{
    public static Result<TOut> Map<TIn, TOut>(this Result<TIn> source, Func<TIn, TOut> f) => source switch
    {
        Success<TIn> s => new Success<TOut>(f(s.Value)),
        Failure<TIn> e => new Failure<TOut>(e.Error),
        _ => throw new InvalidOperationException("Unknown Result case")
    };

    public static Result<TOut> Bind<TIn, TOut>(this Result<TIn> source, Func<TIn, Result<TOut>> f) => source switch
    {
        Success<TIn> s => f(s.Value),
        Failure<TIn> e => new Failure<TOut>(e.Error),
        _ => throw new InvalidOperationException("Unknown Result case")
    };

    /// <summary>Async Bind (Lecture 6, §9): the next step returns Task&lt;Result&lt;TOut&gt;&gt;.</summary>
    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(this Result<TIn> source, Func<TIn, Task<Result<TOut>>> f) => source switch
    {
        Success<TIn> s => await f(s.Value),
        Failure<TIn> e => new Failure<TOut>(e.Error),
        _ => throw new InvalidOperationException("Unknown Result case")
    };

    /// <summary>Guard step: keep the value only if <paramref name="predicate"/> holds.</summary>
    public static Result<T> Ensure<T>(this Result<T> source, Func<T, bool> predicate, Func<T, GameError> error) =>
        source.Bind(value => predicate(value) ? (Result<T>)new Success<T>(value) : new Failure<T>(error(value)));

    /// <summary>Observe a success without changing it (Lecture 6 OnSomething / Lecture 4 Tap).</summary>
    public static Result<T> OnSuccess<T>(this Result<T> source, Action<T> action)
    {
        if (source is Success<T> s) action(s.Value);
        return source;
    }

    /// <summary>Observe a failure without changing it (Lecture 6 OnError), e.g. for logging.</summary>
    public static Result<T> OnFailure<T>(this Result<T> source, Action<GameError> action)
    {
        if (source is Failure<T> f) action(f.Error);
        return source;
    }

    /// <summary>Leave the Result world by handling both cases.</summary>
    public static TOut Match<TIn, TOut>(this Result<TIn> source, Func<TIn, TOut> success, Func<GameError, TOut> failure) => source switch
    {
        Success<TIn> s => success(s.Value),
        Failure<TIn> e => failure(e.Error),
        _ => throw new InvalidOperationException("Unknown Result case")
    };

    /// <summary>Discards the error and keeps only presence/absence.</summary>
    public static Maybe<T> ToMaybe<T>(this Result<T> source) =>
        source is Success<T> s ? new Something<T>(s.Value) : new Nothing<T>();

    /// <summary>
    /// Boundary helper (an improved Lecture 4 MapWithTryCatch): runs code that may throw and turns
    /// the exception into a Failure instead of silently swallowing it.
    /// </summary>
    public static Result<T> Try<T>(Func<T> action, Func<Exception, GameError> onError)
    {
        try
        {
            return new Success<T>(action());
        }
        catch (Exception ex)
        {
            return new Failure<T>(onError(ex));
        }
    }
}
