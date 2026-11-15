using WordDuel.Core.Common;

namespace WordDuel.Core.Functional;

// ─────────────────────────────────────────────────────────────────────────────
// Lecture 5 — Discriminated unions: Result<T>
//
// An operation either succeeds (Success carries the value) or fails
// (Failure carries a GameError). Callers must pattern-match on the case
// instead of guessing from null or catching exceptions.
//
// The lecture's Result stores an Exception in Failure. Our failures are
// *expected* business outcomes (an invalid guess, a full room), so Failure
// stores a user-safe GameError instead (Lecture 5: "Avoid exposing sensitive
// exception messages to users"). Exceptions still mean "bug".
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>The outcome of an operation that can fail: <see cref="Success{T}"/> or <see cref="Failure{T}"/>.</summary>
public abstract record Result<T>
{
    private protected Result() { }

    public bool IsSuccess => this is Success<T>;
}

/// <summary>The successful case, carrying the produced value.</summary>
public sealed record Success<T>(T Value) : Result<T>;

/// <summary>The failed case, carrying the reason.</summary>
public sealed record Failure<T>(GameError Error) : Result<T>;

/// <summary>Convenience constructors.</summary>
public static class Result
{
    public static Result<T> Ok<T>(T value) => new Success<T>(value);

    public static Result<T> Fail<T>(GameError error) => new Failure<T>(error);
}
