using System.Collections.Immutable;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using WordDuel.Core.Analysis;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Words;

// Lecture 9: measure, don't assume. Run with:
//   dotnet run -c Release --project benchmarks/WordDuel.Benchmarks -- --filter * --job short
BenchmarkSwitcher.FromAssembly(typeof(EvaluatorBenchmarks).Assembly).Run(args);

/// <summary>Scoring one opener against every possible answer (1 680 evaluations).</summary>
[MemoryDiagnoser]
public class EvaluatorBenchmarks
{
    private ImmutableArray<string> _answers;

    [GlobalSetup]
    public void Setup() => _answers = EmbeddedWordLists.Load().Answers;

    [Benchmark(Baseline = true, Description = "Imperative (Span + loops)")]
    public int Imperative()
    {
        var sum = 0;
        foreach (var secret in _answers) sum += ImperativeEvaluator.Evaluate(secret, "CRANE").Code;
        return sum;
    }

    [Benchmark(Description = "Functional (LINQ + immutable, used by the game)")]
    public int Functional()
    {
        var sum = 0;
        foreach (var secret in _answers) sum += StrategyAnalyzer.PatternOf(secret, "CRANE").Code;
        return sum;
    }
}

/// <summary>Candidate elimination for one guess over the full answer pool.</summary>
[MemoryDiagnoser]
public class FilterBenchmarks
{
    private ImmutableArray<string> _answers;
    private GuessResult _guess = default!;

    [GlobalSetup]
    public void Setup()
    {
        _answers = EmbeddedWordLists.Load().Answers;
        _guess = new GuessResult("CRANE", WordleEvaluator.Evaluate("DIODE", "CRANE"));
    }

    [Benchmark(Baseline = true, Description = "foreach + List + imperative evaluator")]
    public int ImperativeLoop() => ImperativeEvaluator.Filter(_answers, _guess.Word, _guess.Pattern).Count;

    [Benchmark(Description = "LINQ Where + curried predicate + functional evaluator")]
    public int LinqPipeline() => StrategyAnalyzer.Filter(_answers, _guess, StrategyAnalyzer.PatternOf).Length;

    [Benchmark(Description = "LINQ Where + curried predicate + imperative evaluator")]
    public int LinqWithFastEvaluator() => StrategyAnalyzer.Filter(_answers, _guess, ImperativeEvaluator.Evaluate).Length;
}

/// <summary>The complete post-round analysis for two players who share an opener.</summary>
[MemoryDiagnoser]
public class AnalysisBenchmarks
{
    private WordDictionary _dictionary = default!;
    private Duel _duel = default!;
    private RoundSummary _round = default!;

    [GlobalSetup]
    public void Setup()
    {
        _dictionary = EmbeddedWordLists.Load();
        var alice = new PlayerInfo(new PlayerId("a"), "Alice");
        var bob = new PlayerInfo(new PlayerId("b"), "Bob");
        var t0 = DateTimeOffset.UnixEpoch;
        var validate = GuessValidator.For(_dictionary);

        MatchCommand Guess(PlayerInfo p, string w) => new SubmitGuess(p.Id, ((Success<ValidGuess>)validate(w)).Value);
        var steps = new (MatchCommand, TransitionContext)[]
        {
            (new OpponentJoins(bob), new(t0, "DIODE")),
            (Guess(alice, "CRANE"), new(t0.AddSeconds(5), "DIODE")),
            (Guess(alice, "SLOTH"), new(t0.AddSeconds(15), "DIODE")),
            (Guess(alice, "PUDGY"), new(t0.AddSeconds(25), "DIODE")),
            (Guess(alice, "DIODE"), new(t0.AddSeconds(35), "DIODE")),
            (Guess(bob, "CRANE"), new(t0.AddSeconds(6), "DIODE")),
            (Guess(bob, "MOTOR"), new(t0.AddSeconds(16), "DIODE")),
            (Guess(bob, "DIODE"), new(t0.AddSeconds(26), "DIODE"))
        };
        var state = (BetweenRounds)((Success<MatchState>)MatchEngine.Replay(MatchEngine.Create(alice, MatchRules.BestOfThree), steps)).Value;
        _duel = state.Duel;
        _round = state.Duel.History[0];
    }

    [Benchmark(Baseline = true, Description = "AnalyzeRound without memoization")]
    public int Plain() => StrategyAnalyzer.AnalyzeRound(_dictionary, _duel, _round, memoize: false).Highlights.Count;

    [Benchmark(Description = "AnalyzeRound with local memoization")]
    public int Memoized() => StrategyAnalyzer.AnalyzeRound(_dictionary, _duel, _round, memoize: true).Highlights.Count;
}
