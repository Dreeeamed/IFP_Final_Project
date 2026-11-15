using WordDuel.Core.Common;
using WordDuel.Core.Functional;

namespace WordDuel.Core.Players;

/// <summary>Strongly typed player identifier (a random value created by the server).</summary>
public readonly record struct PlayerId(string Value)
{
    public override string ToString() => Value;
}

/// <summary>Public information about a seated player.</summary>
public sealed record PlayerInfo(PlayerId Id, string Nickname);

/// <summary>Pure nickname rules.</summary>
public static class Nickname
{
    public const int MaxLength = 16;

    /// <summary>
    /// Pipe (Lecture 4 Map) pipeline: trim → collapse inner whitespace → check the rules.
    /// Any Unicode letter is accepted, so non-Latin names work too.
    /// </summary>
    public static Result<string> Validate(string? raw) =>
        (raw ?? string.Empty)
            .Pipe(text => text.Trim())
            .Pipe(text => string.Join(' ', text.Split(' ', StringSplitOptions.RemoveEmptyEntries)))
            .Pipe(name => name.Length is >= 1 and <= MaxLength && name.All(IsAllowed)
                ? Result.Ok(name)
                : Result.Fail<string>(GameErrors.InvalidNickname));

    private static bool IsAllowed(char c) => char.IsLetterOrDigit(c) || c is ' ' or '-' or '_';
}
