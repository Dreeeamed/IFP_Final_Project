using System.Collections.Immutable;
using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Words;
using Xunit.Sdk;

namespace WordDuel.Tests.TestSupport;

/// <summary>Small, deterministic fixtures shared by the tests.</summary>
internal static class TestData
{
    public static readonly WordDictionary Dictionary = WordDictionary.Create(
        answers:
        [
            "CRANE", "CRATE", "TRACE", "GRAPE", "BRAKE", "SLATE", "ABBEY", "APPLE", "HELLO", "ROBOT",
            "SPEED", "STEAK", "SKATE", "STAKE", "SHAKE", "SNAKE", "QUEEN", "PIZZA", "MOTOR", "LEMON"
        ],
        allowedGuesses: ["EERIE", "LLAMA", "PAPPY", "ERASE", "OTTER", "KEBAB", "AUDIO", "ROUTE", "TEARS", "BLIMP"]);

    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static readonly PlayerInfo Alice = new(new PlayerId("alice"), "Alice");
    public static readonly PlayerInfo Bob = new(new PlayerId("bob"), "Bob");
    public static readonly PlayerInfo Mallory = new(new PlayerId("mallory"), "Mallory");

    public static ValidGuess Valid(string word) => Unwrap(GuessValidator.Validate(Dictionary, word));

    public static TransitionContext At(double seconds, string nextSecret = "CRANE") => new(T0.AddSeconds(seconds), nextSecret);

    public static T Unwrap<T>(Result<T> result) => result switch
    {
        Success<T> success => success.Value,
        Failure<T> failure => throw new XunitException($"Expected Success but got Failure: {failure.Error}"),
        _ => throw new XunitException("Unknown Result case")
    };

    public static GameError ErrorOf<T>(Result<T> result) => result switch
    {
        Failure<T> failure => failure.Error,
        _ => throw new XunitException($"Expected Failure but got {result}")
    };

    /// <summary>'G' = green (Correct), 'Y' = yellow (Present), '.' = gray (Absent).</summary>
    public static ImmutableArray<LetterState> Pattern(string marks) =>
        marks.Select(mark => mark switch
        {
            'G' => LetterState.Correct,
            'Y' => LetterState.Present,
            '.' => LetterState.Absent,
            _ => throw new ArgumentException($"Unknown mark {mark}")
        }).ToImmutableArray();

    public static string Marks(IEnumerable<LetterState> states) =>
        string.Concat(states.Select(state => state switch
        {
            LetterState.Correct => 'G',
            LetterState.Present => 'Y',
            _ => '.'
        }));

    /// <summary>Applies a command and expects success.</summary>
    public static MatchState Step(MatchState state, MatchCommand command, TransitionContext context) =>
        Unwrap(MatchEngine.Apply(state, command, context));

    /// <summary>Waiting room → both players joined, round 1 started at T0 with the given secret.</summary>
    public static RoundInProgress StartedMatch(string secret = "CRANE", MatchRules? rules = null) =>
        (RoundInProgress)Step(MatchEngine.Create(Alice, rules ?? MatchRules.BestOfThree), new OpponentJoins(Bob), At(0, secret));

    /// <summary>Submits several guesses for one player at increasing times.</summary>
    public static MatchState Guesses(MatchState state, PlayerInfo player, double startSecond, params string[] words) =>
        words.Select((word, index) => (word, index))
            .Aggregate(state, (current, x) => Step(current, new SubmitGuess(player.Id, Valid(x.word)), At(startSecond + x.index)));
}
