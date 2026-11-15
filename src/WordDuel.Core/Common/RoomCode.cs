using WordDuel.Core.Functional;

namespace WordDuel.Core.Common;

/// <summary>Pure rules for the short, shareable room code.</summary>
public static class RoomCode
{
    public const int Length = 6;

    /// <summary>No 0/O or 1/I, so codes are easy to read aloud and type on a phone.</summary>
    public const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    public static Result<string> Normalize(string? raw) =>
        (raw ?? string.Empty).Trim().ToUpperInvariant()
            .Pipe(code => code.Length == Length && code.All(Alphabet.Contains)
                ? Result.Ok(code)
                : Result.Fail<string>(GameErrors.InvalidRoomCode));
}
