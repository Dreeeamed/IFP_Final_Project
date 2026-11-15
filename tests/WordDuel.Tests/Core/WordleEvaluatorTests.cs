using WordDuel.Core.Words;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student A — pure guess evaluation, including every repeated-letter rule.</summary>
public class WordleEvaluatorTests
{
    [Theory]
    [InlineData("CRANE", "CRANE", "GGGGG")]   // exact match
    [InlineData("CRANE", "BLIMP", ".....")]   // nothing shared
    [InlineData("CRANE", "TRACE", ".GGYG")]   // C is present elsewhere, E is green
    [InlineData("CRANE", "NACRE", "YYYYG")]   // anagram: every letter present, only E fixed
    public void Basic_feedback(string secret, string guess, string expected) =>
        Assert.Equal(expected, Marks(WordleEvaluator.Evaluate(secret, guess)));

    [Theory]
    // The secret has one E, already used by the green E → the other two E's are gray.
    [InlineData("CRANE", "EERIE", "..Y.G")]
    // Two L's guessed, two available → both yellow.
    [InlineData("HELLO", "LLAMA", "YY...")]
    // Three P's guessed, the secret has two; the green consumes one, the first P takes the other.
    [InlineData("APPLE", "PAPPY", "YYG..")]
    // B appears twice in both words: one green, the remaining guessed B gets the remaining copy.
    [InlineData("ABBEY", "KEBAB", ".YGYY")]
    // Greens are assigned before yellows, even when the yellow candidate appears earlier.
    [InlineData("ROBOT", "OOOOO", ".G.G.")]
    // Two E's in the secret, two E's guessed in wrong places → both yellow.
    [InlineData("SPEED", "ERASE", "Y..YY")]
    // One S in the secret, two guessed: only the first unmatched S is yellow.
    [InlineData("SLATE", "SASSY", "GY...")]
    public void Repeated_letters_follow_the_official_rules(string secret, string guess, string expected) =>
        Assert.Equal(expected, Marks(WordleEvaluator.Evaluate(secret, guess)));

    [Fact]
    public void Evaluation_is_deterministic()
    {
        // ImmutableArray's own Equals compares references, so compare the contents explicitly.
        var first = WordleEvaluator.Evaluate("ABBEY", "KEBAB");
        var second = WordleEvaluator.Evaluate("ABBEY", "KEBAB");
        Assert.Equal(first.ToArray(), second.ToArray());
    }

    [Fact]
    public void Number_of_coloured_tiles_never_exceeds_letter_count_in_secret()
    {
        // Property check over many real words: for every letter, greens + yellows ≤ copies in the secret.
        var words = EmbeddedWordLists.Load().Answers.Take(150).ToList();
        foreach (var secret in words)
        foreach (var guess in words)
        {
            var states = WordleEvaluator.Evaluate(secret, guess);
            var coloured = guess.Zip(states).Where(x => x.Second != LetterState.Absent).GroupBy(x => x.First);
            foreach (var group in coloured)
                Assert.True(group.Count() <= secret.Count(c => c == group.Key), $"{secret}/{guess}");
        }
    }

    [Fact]
    public void Different_lengths_are_a_programming_error()
    {
        Assert.Throws<ArgumentException>(() => WordleEvaluator.Evaluate("CRANE", "CRANES"));
    }

    [Fact]
    public void Feedback_pattern_round_trips_through_its_integer_code()
    {
        var states = Pattern("GY.YG");
        var pattern = FeedbackPattern.From(states);
        Assert.Equal(states.ToArray(), pattern.ToStates(5).ToArray());
        Assert.Equal(FeedbackPattern.From(Pattern("GGGGG")), FeedbackPattern.From(WordleEvaluator.Evaluate("CRANE", "CRANE")));
    }
}
