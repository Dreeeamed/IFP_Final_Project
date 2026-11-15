namespace WordDuel.Core.Functional;

// ─────────────────────────────────────────────────────────────────────────────
// Lecture 5 — Discriminated unions: Maybe<T>
//
// A value is either Something (it carries a T) or Nothing (no value).
// We follow the lecture's class-hierarchy emulation, written with C# records
// (Lecture 3.3) so that two Somethings with equal values are equal.
//
// The constructor is `private protected`, so no code outside this assembly can
// add a third case. This closes the union, which an abstract base class alone
// does not do (Lecture 5, "Emulation boundary").
//
// Unlike the lecture's three-state Maybe, this is a "true Maybe" (Lecture 6, §16):
// failures that carry a reason use Result<T> instead.
// ─────────────────────────────────────────────────────────────────────────────

/// <summary>An optional value: either <see cref="Something{T}"/> or <see cref="Nothing{T}"/>.</summary>
public abstract record Maybe<T>
{
    private protected Maybe() { }
}

/// <summary>The case that carries a value.</summary>
public sealed record Something<T>(T Value) : Maybe<T>;

/// <summary>The case that represents absence. It has no payload.</summary>
public sealed record Nothing<T> : Maybe<T>;

/// <summary>Convenience constructors so call sites read naturally.</summary>
public static class Maybe
{
    public static Maybe<T> Some<T>(T value) => new Something<T>(value);

    public static Maybe<T> None<T>() => new Nothing<T>();

    /// <summary>Lifts a possibly-null reference into Maybe (null becomes Nothing).</summary>
    public static Maybe<T> FromNullable<T>(T? value) where T : class =>
        value is null ? new Nothing<T>() : new Something<T>(value);
}
