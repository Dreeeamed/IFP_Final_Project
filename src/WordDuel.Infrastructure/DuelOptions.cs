using WordDuel.Core.Match;

namespace WordDuel.Infrastructure;

/// <summary>Server settings, bound from the "WordDuel" section of appsettings.json.</summary>
public sealed class DuelOptions
{
    public const string Section = "WordDuel";

    public int WinsNeeded { get; set; } = 2;
    public int MaxRounds { get; set; } = 5;
    public int RoundSeconds { get; set; } = 300;
    public int BreakSeconds { get; set; } = 45;

    /// <summary>How long a disconnected player may take to come back before forfeiting.</summary>
    public int ReconnectGraceSeconds { get; set; } = 60;

    /// <summary>Rooms with no activity for this long are removed.</summary>
    public int IdleRoomMinutes { get; set; } = 30;

    /// <summary>Upper bound on simultaneous rooms (protects server memory).</summary>
    public int MaxRooms { get; set; } = 2000;

    /// <summary>Minimum gap between two actions from the same player (anti-spam).</summary>
    public int MinActionIntervalMs { get; set; } = 200;

    public MatchRules ToRules() => new()
    {
        WinsNeeded = WinsNeeded,
        MaxRounds = MaxRounds,
        RoundDuration = TimeSpan.FromSeconds(RoundSeconds),
        BreakDuration = TimeSpan.FromSeconds(BreakSeconds)
    };
}
