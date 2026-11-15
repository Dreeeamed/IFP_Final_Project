using System.Collections.Immutable;
using System.Security.Cryptography;
using WordDuel.Core.Common;
using WordDuel.Core.Players;
using WordDuel.Core.Views;
using WordDuel.Core.Words;

namespace WordDuel.Infrastructure;

// Interfaces for everything impure the shell needs (Lecture 10: "Interfaces … IRandomSource … the
// game engine depends on contracts; tests provide deterministic fakes").

/// <summary>Server time. Injected so tests can control it.</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }
}

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}

/// <summary>Chooses secret words. The choice happens only on the server.</summary>
public interface ISecretWordPicker
{
    string Pick(WordDictionary dictionary, IReadOnlySet<string> exclude);
}

/// <summary>Uniform choice with a cryptographic RNG, so the next secret cannot be predicted from earlier ones.</summary>
public sealed class RandomSecretWordPicker : ISecretWordPicker
{
    public string Pick(WordDictionary dictionary, IReadOnlySet<string> exclude)
    {
        var answers = dictionary.Answers;
        var candidates = exclude.Count == 0 || exclude.Count >= answers.Length
            ? answers
            : answers.Where(word => !exclude.Contains(word)).ToImmutableArray();
        return candidates[RandomNumberGenerator.GetInt32(candidates.Length)];
    }
}

/// <summary>Creates room codes, player ids and session tokens.</summary>
public interface IIdentityGenerator
{
    string NewRoomCode();

    PlayerId NewPlayerId();

    /// <summary>A secret, unguessable value that proves which seat a browser owns.</summary>
    string NewToken();
}

public sealed class SecureIdentityGenerator : IIdentityGenerator
{
    public string NewRoomCode() => RandomNumberGenerator.GetString(RoomCode.Alphabet, RoomCode.Length);

    public PlayerId NewPlayerId() => new(Convert.ToHexString(RandomNumberGenerator.GetBytes(8)).ToLowerInvariant());

    // 256 bits of randomness: guessing a valid token is infeasible.
    public string NewToken() => Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
}

/// <summary>Delivers a personalised view to one connection. Implemented with SignalR by the web host.</summary>
public interface IRoomNotifier
{
    Task SendAsync(string connectionId, PlayerView view);
}
