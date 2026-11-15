using WordDuel.Core.Analysis;
using WordDuel.Core.Words;

namespace WordDuel.Tests.Core;

/// <summary>
/// Lecture 9: "Compare implementations that produce the same result." Before trusting any benchmark,
/// prove the imperative baseline and the functional implementation agree.
/// </summary>
public class PerformanceBaselineTests
{
    [Fact]
    public void Imperative_and_functional_evaluators_agree_on_many_real_word_pairs()
    {
        var words = EmbeddedWordLists.Load().Answers.Where((_, i) => i % 7 == 0).ToList(); // ~240 words → ~57 000 pairs
        foreach (var secret in words)
        foreach (var guess in words)
            Assert.Equal(StrategyAnalyzer.PatternOf(secret, guess), ImperativeEvaluator.Evaluate(secret, guess));
    }

    [Theory]
    [InlineData("ABBEY", "KEBAB")]
    [InlineData("CRANE", "EERIE")]
    [InlineData("APPLE", "PAPPY")]
    [InlineData("ROBOT", "OOOOO")]
    public void Imperative_evaluator_handles_repeated_letters(string secret, string guess) =>
        Assert.Equal(FeedbackPattern.From(WordleEvaluator.Evaluate(secret, guess)), ImperativeEvaluator.Evaluate(secret, guess));

    [Fact]
    public void Imperative_and_LINQ_filters_keep_the_same_candidates()
    {
        var pool = EmbeddedWordLists.Load().Answers;
        var observed = StrategyAnalyzer.PatternOf("DIODE", "CRANE");
        var guess = new GuessResult("CRANE", observed.ToStates(5));

        Assert.Equal(ImperativeEvaluator.Filter(pool, "CRANE", observed), StrategyAnalyzer.Filter(pool, guess, StrategyAnalyzer.PatternOf));
    }
}
