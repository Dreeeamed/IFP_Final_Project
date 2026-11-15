using System.Collections.Immutable;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Words;

namespace WordDuel.Core.Analysis;

/// <summary>What one guess achieved, computed from real game data after the round.</summary>
public sealed record GuessInsight(
    int Number,
    string Word,
    IReadOnlyList<LetterState> States,
    int CandidatesBefore,
    int CandidatesAfter,
    double ExpectedAfter,
    int PossibleOutcomes,
    bool WasPossibleAnswer,
    IReadOnlyList<string> SampleRemaining)
{
    public int Eliminated => CandidatesBefore - CandidatesAfter;

    public double EliminationPercent => CandidatesBefore == 0 ? 0 : 100.0 * Eliminated / CandidatesBefore;
}

/// <summary>A player's whole round, guess by guess.</summary>
public sealed record PlayerStrategy(
    PlayerId Player,
    string Nickname,
    bool Solved,
    int Attempts,
    double? SolveSeconds,
    IReadOnlyList<GuessInsight> Guesses)
{
    /// <summary>
    /// Average elimination over informative guesses only: once a single candidate is left, a guess
    /// cannot eliminate anything, so counting it would unfairly drag the average down.
    /// </summary>
    public double AverageElimination =>
        Guesses.Where(guess => guess.CandidatesBefore > 1).Select(guess => guess.EliminationPercent).DefaultIfEmpty(0).Average();

    public GuessInsight? BestGuess => Guesses.MaxBy(guess => guess.EliminationPercent);

    /// <summary>Remaining candidates after the n-th guess (n starts at 1), or the pool size for n = 0.</summary>
    public int RemainingAfter(int n, int poolSize) =>
        n == 0 || Guesses.Count == 0 ? poolSize : Guesses[Math.Min(n, Guesses.Count) - 1].CandidatesAfter;
}

/// <summary>The complete post-round analysis for both players.</summary>
public sealed record RoundAnalysis(
    int RoundNumber,
    string Secret,
    int PoolSize,
    PlayerStrategy First,
    PlayerStrategy Second,
    IReadOnlyList<string> Highlights)
{
    public PlayerStrategy For(PlayerId player) => First.Player == player ? First : Second;
}

/// <summary>
/// Explainable, deterministic Word Strategy Analyzer.
///
/// Idea: start with every possible answer. A guess plus its real feedback is only consistent with
/// candidates that would have produced exactly that feedback. Re-using the game's own evaluator for
/// this check guarantees that repeated letters are handled exactly like in the game.
/// </summary>
public static class StrategyAnalyzer
{
    /// <summary>The feedback a guess would get if <c>candidate</c> were the secret (pure).</summary>
    public static readonly Func<string, string, FeedbackPattern> PatternOf =
        (candidate, guess) => FeedbackPattern.From(WordleEvaluator.Evaluate(candidate, guess));

    /// <summary>
    /// Curried consistency test (Lecture 7): guess → observed pattern → candidate → bool.
    /// Supplying the first two arguments yields a ready-made predicate for LINQ Where.
    /// </summary>
    public static Func<string, Func<FeedbackPattern, Func<string, bool>>> ConsistentWith(Func<string, string, FeedbackPattern> patternOf) =>
        Currying.Curry<string, FeedbackPattern, string, bool>(
            (guess, observed, candidate) => patternOf(candidate, guess) == observed);

    /// <summary>Candidate elimination for one guess: keep only words that agree with the feedback.</summary>
    public static ImmutableArray<string> Filter(IEnumerable<string> pool, GuessResult guess, Func<string, string, FeedbackPattern> patternOf) =>
        pool.Where(ConsistentWith(patternOf)(guess.Word)(guess.Pattern)).ToImmutableArray();

    /// <summary>
    /// How well a guess splits a pool, before its feedback is known: the pool is grouped by the
    /// pattern each candidate would produce. Expected size of the remaining pool = Σ size² / total.
    /// </summary>
    public static (double ExpectedRemaining, int Outcomes) Partition(IReadOnlyCollection<string> pool, string guess, Func<string, string, FeedbackPattern> patternOf)
    {
        if (pool.Count == 0) return (0, 0);
        var bucketSizes = pool.GroupBy(candidate => patternOf(candidate, guess)).Select(bucket => bucket.Count()).ToList();
        return (bucketSizes.Sum(size => (double)size * size) / pool.Count, bucketSizes.Count);
    }

    /// <summary>Analyses one player's guesses against the candidate pool.</summary>
    public static PlayerStrategy AnalyzePlayer(
        ImmutableArray<string> pool,
        PlayerInfo player,
        CompletionRecord result,
        IReadOnlyList<GuessResult> guesses,
        Func<string, string, FeedbackPattern> patternOf)
    {
        // Scan (an iterator, Lecture 7) yields the pool before guess 1, after guess 1, after guess 2 …
        var pools = guesses.Scan(pool, (current, guess) => Filter(current, guess, patternOf)).ToImmutableArray();

        var insights = guesses
            .Select((guess, index) => (guess, number: index + 1, before: pools[index], after: pools[index + 1]))
            .Select(step => step.before.Fork(
                before => Partition(before, step.guess.Word, patternOf),
                before => before.Contains(step.guess.Word),
                (partition, wasPossible) => new GuessInsight(
                    Number: step.number,
                    Word: step.guess.Word,
                    States: step.guess.States,
                    CandidatesBefore: step.before.Length,
                    CandidatesAfter: step.after.Length,
                    ExpectedAfter: Math.Round(partition.ExpectedRemaining, 1),
                    PossibleOutcomes: partition.Outcomes,
                    WasPossibleAnswer: wasPossible,
                    SampleRemaining: step.after.Take(6).ToImmutableArray())))
            .ToImmutableArray();

        return new PlayerStrategy(player.Id, player.Nickname, result.Solved, result.Attempts, result.SolveTime?.TotalSeconds, insights);
    }

    /// <summary>
    /// Analyses a finished round for both players.
    ///
    /// Memoization (Lecture 8): Partition and Filter evaluate the same (candidate, guess) pairs, and
    /// players often share guesses such as their opener. One memoized pattern function, created
    /// for this round only, computes every pair once; the cache is discarded with the delegate.
    /// The benchmark project measures the effect instead of assuming it.
    /// </summary>
    public static RoundAnalysis AnalyzeRound(WordDictionary dictionary, Duel duel, RoundSummary round, bool memoize = true)
    {
        var patternOf = memoize ? PatternOf.Memoize() : PatternOf;

        PlayerStrategy Analyze(PlayerInfo player) => AnalyzePlayer(
            dictionary.Answers, player, round.Results[player.Id], round.Boards[player.Id].Board.Guesses, patternOf);

        var first = Analyze(duel.First);
        var second = Analyze(duel.Second);
        return new RoundAnalysis(round.Number, round.Secret, dictionary.Answers.Length, first, second, Highlights.For(first, second, dictionary.Answers.Length));
    }
}
