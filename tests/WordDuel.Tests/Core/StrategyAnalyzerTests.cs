using System.Collections.Immutable;
using WordDuel.Core.Analysis;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Words;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student B — candidate elimination, partition quality, memoization and highlights.</summary>
public class StrategyAnalyzerTests
{
    private static GuessResult Result(string secret, string guess) => new(guess, WordleEvaluator.Evaluate(secret, guess));

    [Fact]
    public void Filter_keeps_exactly_the_words_consistent_with_the_feedback()
    {
        var pool = Dictionary.Answers;
        var guess = Result("SKATE", "STEAK");     // feedback S=green, T/E/A/K yellow
        var remaining = StrategyAnalyzer.Filter(pool, guess, StrategyAnalyzer.PatternOf);

        Assert.Contains("SKATE", remaining);
        Assert.DoesNotContain("STAKE", remaining); // S and T would both be green → different feedback
        Assert.DoesNotContain("STEAK", remaining); // STEAK itself would have been all green
        Assert.All(remaining, word => Assert.Equal(guess.Pattern, StrategyAnalyzer.PatternOf(word, "STEAK")));
    }

    [Fact]
    public void Filtering_respects_repeated_letters_like_the_game()
    {
        // Secret ABBEY, guess KEBAB → .YGYY: the second B is yellow, so the answer needs TWO B's.
        // LABEL has B, E and A in allowed places but only one B, so it must be eliminated.
        var guess = Result("ABBEY", "KEBAB");
        var pool = ImmutableArray.Create("ABBEY", "BABES", "LABEL", "BRAKE");
        var remaining = StrategyAnalyzer.Filter(pool, guess, StrategyAnalyzer.PatternOf);

        Assert.Equal(new[] { "ABBEY", "BABES" }, remaining);
    }

    [Fact]
    public void Gray_duplicates_do_not_eliminate_words_containing_one_copy()
    {
        // Secret CRANE, guess EERIE → ..Y.G. Two E's are gray, yet the answer still contains an E.
        var guess = Result("CRANE", "EERIE");
        var remaining = StrategyAnalyzer.Filter(ImmutableArray.Create("CRANE", "BRAKE", "GRAPE", "ROBOT"), guess, StrategyAnalyzer.PatternOf);

        Assert.Contains("CRANE", remaining);
        Assert.DoesNotContain("ROBOT", remaining);
    }

    [Fact]
    public void Curried_predicate_can_be_specialised_step_by_step()
    {
        var consistent = StrategyAnalyzer.ConsistentWith(StrategyAnalyzer.PatternOf);
        var afterSteak = consistent("STEAK");                          // Func<FeedbackPattern, Func<string, bool>>
        var matchesSkate = afterSteak(Result("SKATE", "STEAK").Pattern); // Func<string, bool>

        Assert.True(matchesSkate("SKATE"));
        Assert.False(matchesSkate("CRANE"));
    }

    [Fact]
    public void Partition_computes_expected_remaining_candidates()
    {
        // Guess CRANE splits the pool into {CRANE} (GGGGG) and {SLATE, SKATE, STAKE} (..G.G):
        // expected remaining = (1² + 3²) / 4 = 2.5
        var pool = new[] { "CRANE", "SLATE", "SKATE", "STAKE" };
        var (expected, outcomes) = StrategyAnalyzer.Partition(pool, "CRANE", StrategyAnalyzer.PatternOf);

        Assert.Equal(2, outcomes);
        Assert.Equal(2.5, expected, precision: 6);
    }

    [Fact]
    public void Player_analysis_tracks_candidates_before_and_after_each_guess()
    {
        var guesses = new[] { Result("CRANE", "SLATE"), Result("CRANE", "TRACE"), Result("CRANE", "CRANE") };
        var record = new CompletionRecord(Alice.Id, true, 3, TimeSpan.FromSeconds(42));
        var strategy = StrategyAnalyzer.AnalyzePlayer(Dictionary.Answers, Alice, record, guesses, StrategyAnalyzer.PatternOf);

        Assert.Equal(3, strategy.Guesses.Count);
        Assert.Equal(Dictionary.Answers.Length, strategy.Guesses[0].CandidatesBefore);
        Assert.Equal(strategy.Guesses[0].CandidatesAfter, strategy.Guesses[1].CandidatesBefore);
        Assert.Equal(1, strategy.Guesses[2].CandidatesAfter);                // only the secret survives the solving guess
        Assert.All(strategy.Guesses, g => Assert.Equal(g.CandidatesBefore - g.CandidatesAfter, g.Eliminated));
        Assert.All(strategy.Guesses, g => Assert.InRange(g.EliminationPercent, 0, 100));
        Assert.True(strategy.Guesses[2].WasPossibleAnswer);
        Assert.Equal(42, strategy.SolveSeconds);
    }

    [Fact]
    public void Memoized_and_plain_analysis_give_identical_results()
    {
        var round = (BetweenRounds)Guesses(Guesses(StartedMatch("CRANE"), Alice, 1, "SLATE", "CRANE"), Bob, 5, "SLATE", "TRACE", "CRANE");
        var summary = round.Duel.History[0];

        var memo = StrategyAnalyzer.AnalyzeRound(Dictionary, round.Duel, summary, memoize: true);
        var plain = StrategyAnalyzer.AnalyzeRound(Dictionary, round.Duel, summary, memoize: false);

        Assert.Equal(Describe(plain), Describe(memo));
        Assert.Equal(plain.Highlights, memo.Highlights);
    }

    [Fact]
    public void Memoization_avoids_repeated_pattern_evaluations()
    {
        var calls = 0;
        Func<string, string, FeedbackPattern> counting = (candidate, guess) =>
        {
            calls++;
            return StrategyAnalyzer.PatternOf(candidate, guess);
        };
        var guesses = new[] { Result("CRANE", "SLATE"), Result("CRANE", "CRANE") };
        var record = new CompletionRecord(Alice.Id, true, 2, TimeSpan.FromSeconds(5));

        StrategyAnalyzer.AnalyzePlayer(Dictionary.Answers, Alice, record, guesses, counting);
        var plainCalls = calls;

        calls = 0;
        StrategyAnalyzer.AnalyzePlayer(Dictionary.Answers, Alice, record, guesses, Memoization.Memoize(counting));
        var memoCalls = calls;

        // Partition and Filter both evaluate every (candidate, guess) pair; memoization halves the work.
        Assert.Equal(plainCalls, memoCalls * 2);
    }

    [Fact]
    public void Highlights_are_built_from_real_numbers()
    {
        var round = (BetweenRounds)Guesses(Guesses(StartedMatch("CRANE"), Alice, 1, "SLATE", "CRANE"), Bob, 5, "SLATE", "TRACE", "CRANE");
        var analysis = StrategyAnalyzer.AnalyzeRound(Dictionary, round.Duel, round.Duel.History[0]);

        Assert.Contains(analysis.Highlights, h => h.Contains("Alice solved the word using fewer attempts (2 vs 3)"));
        Assert.All(analysis.Highlights, h => Assert.False(string.IsNullOrWhiteSpace(h)));
        Assert.Equal(analysis.First.Guesses[0].CandidatesAfter, analysis.Second.Guesses[0].CandidatesAfter); // same opener, same secret
    }

    private static string Describe(RoundAnalysis analysis) =>
        string.Join("|", new[] { analysis.First, analysis.Second }.SelectMany(p => p.Guesses)
            .Select(g => $"{g.Word}:{g.CandidatesBefore}>{g.CandidatesAfter}:{g.ExpectedAfter}:{g.PossibleOutcomes}:{g.WasPossibleAnswer}"));
}
