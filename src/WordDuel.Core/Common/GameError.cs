namespace WordDuel.Core.Common;

/// <summary>Stable machine-readable error categories shared by the server and the client.</summary>
public enum ErrorCode
{
    InvalidInput,
    WrongLength,
    NotInDictionary,
    AlreadyGuessed,
    BoardFinished,
    RoundNotActive,
    NotInRoom,
    RoomNotFound,
    RoomFull,
    ServerFull,
    InvalidToken,
    InvalidAction,
    TooFast,
    Unexpected
}

/// <summary>
/// An expected, user-safe failure. Errors are ordinary values (Lecture 5/6): they travel
/// inside <c>Failure&lt;T&gt;</c> instead of being thrown, and the message never contains
/// internal details such as the secret word or exception text.
/// </summary>
public sealed record GameError(ErrorCode Code, string Message)
{
    public override string ToString() => $"{Code}: {Message}";
}

/// <summary>Factory functions for every error the game can report.</summary>
public static class GameErrors
{
    public static GameError EmptyGuess { get; } = new(ErrorCode.InvalidInput, "Type a word first.");
    public static GameError NotLetters { get; } = new(ErrorCode.InvalidInput, "Use letters A–Z only.");
    public static GameError WrongLength(int length) => new(ErrorCode.WrongLength, $"Guesses must have exactly {length} letters.");
    public static GameError NotInDictionary(string word) => new(ErrorCode.NotInDictionary, $"{word} is not in the word list.");
    public static GameError AlreadyGuessed(string word) => new(ErrorCode.AlreadyGuessed, $"You already tried {word}.");
    public static GameError BoardFinished { get; } = new(ErrorCode.BoardFinished, "Your board is finished for this round.");
    public static GameError RoundNotActive { get; } = new(ErrorCode.RoundNotActive, "There is no active round right now.");
    public static GameError NotInRoom { get; } = new(ErrorCode.NotInRoom, "You are not a player in this room.");
    public static GameError RoomNotFound { get; } = new(ErrorCode.RoomNotFound, "No room with that code exists (it may have expired).");
    public static GameError RoomFull { get; } = new(ErrorCode.RoomFull, "That room already has two players.");
    public static GameError ServerFull { get; } = new(ErrorCode.ServerFull, "The server is busy. Please try again in a minute.");
    public static GameError InvalidToken { get; } = new(ErrorCode.InvalidToken, "Your session for this room is no longer valid.");
    public static GameError InvalidRoomCode { get; } = new(ErrorCode.InvalidInput, "Room codes have 6 letters or digits.");
    public static GameError InvalidNickname { get; } = new(ErrorCode.InvalidInput, "Nicknames need 1–16 letters, digits, spaces, '-' or '_'.");
    public static GameError TooFast { get; } = new(ErrorCode.TooFast, "Slow down — one guess at a time.");
    public static GameError InvalidAction(string action) => new(ErrorCode.InvalidAction, $"You can't {action} right now.");
    public static GameError Unexpected { get; } = new(ErrorCode.Unexpected, "Something went wrong on the server. Please try again.");
    public static GameError ConnectionLost { get; } = new(ErrorCode.Unexpected, "No connection to the game server. Retrying…");
}
