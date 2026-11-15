using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Students A and B — the functional toolkit behaves exactly as the lectures describe.</summary>
public class FunctionalToolkitTests
{
    private static readonly GameError Boom = new(ErrorCode.Unexpected, "boom");

    // ── Lecture 6: monad laws for Result and Maybe ─────────────────────────────

    [Fact]
    public void Result_left_identity()
    {
        Func<int, Result<int>> doubleIt = x => Result.Ok(x * 2);
        Assert.Equal(doubleIt(100), Result.Ok(100).Bind(doubleIt));
    }

    [Fact]
    public void Result_right_identity()
    {
        var input = Result.Ok(200);
        Assert.Equal(input, input.Bind(Result.Ok));
    }

    [Fact]
    public void Result_associativity()
    {
        Func<int, Result<int>> op1 = x => Result.Ok(x * 2);
        Func<int, Result<int>> op2 = x => Result.Ok(x + 100);
        var input = Result.Ok(100);

        Assert.Equal(input.Bind(op1).Bind(op2), input.Bind(x => op1(x).Bind(op2)));
        Assert.Equal(Result.Ok(300), input.Bind(op1).Bind(op2));
    }

    [Fact]
    public void Maybe_obeys_the_three_laws()
    {
        Func<int, Maybe<int>> f = x => Maybe.Some(x * 2);
        Func<int, Maybe<int>> g = x => x > 150 ? Maybe.Some(x - 1) : Maybe.None<int>();
        var m = Maybe.Some(100);

        Assert.Equal(f(100), Maybe.Some(100).Bind(f));                    // left identity
        Assert.Equal(m, m.Bind(Maybe.Some));                              // right identity
        Assert.Equal(m.Bind(f).Bind(g), m.Bind(x => f(x).Bind(g)));       // associativity
    }

    [Fact]
    public void Failure_skips_every_later_step()
    {
        var laterStepRan = false;
        var result = Result.Fail<int>(Boom)
            .Map(x => x + 1)
            .Bind(x => { laterStepRan = true; return Result.Ok(x); });

        Assert.False(laterStepRan);
        Assert.Equal(Boom, ErrorOf(result));
    }

    [Fact]
    public void Map_wraps_a_plain_result_while_Bind_flattens_a_wrapped_one()
    {
        var mapped = Result.Ok(2).Map(x => Result.Ok(x));      // Result<Result<int>>
        var bound = Result.Ok(2).Bind(x => Result.Ok(x));       // Result<int>
        Assert.IsType<Success<Result<int>>>(mapped);
        Assert.IsType<Success<int>>(bound);
    }

    [Fact]
    public void Ensure_and_observers_do_not_change_the_value()
    {
        var seen = new List<string>();
        var result = Result.Ok(5)
            .Ensure(x => x > 0, _ => Boom)
            .OnSuccess(x => seen.Add($"ok {x}"))
            .OnFailure(e => seen.Add("fail"))
            .Ensure(x => x > 10, _ => Boom)
            .OnFailure(e => seen.Add($"fail {e.Message}"));

        Assert.Equal(new[] { "ok 5", "fail boom" }, seen);
        Assert.Equal(Boom, ErrorOf(result));
    }

    [Fact]
    public void Try_turns_exceptions_into_failures_at_the_boundary()
    {
        var result = ResultExtensions.Try<int>(() => throw new InvalidOperationException("db down"), _ => Boom);
        Assert.Equal(Boom, ErrorOf(result));
    }

    [Fact]
    public async Task BindAsync_composes_asynchronous_steps()
    {
        var result = await Result.Ok(20).BindAsync(async x => { await Task.Yield(); return Result.Ok(x + 1); });
        Assert.Equal(Result.Ok(21), result);
    }

    // ── Lecture 4: higher-order combinators ───────────────────────────────────

    [Fact]
    public void Compose_runs_left_to_right()
    {
        Func<int, int> addOne = x => x + 1;
        var addOneThenDouble = addOne.Compose(x => x * 2);
        Assert.Equal(8, addOneThenDouble(3));
    }

    [Fact]
    public void Fork_feeds_one_input_to_two_prongs()
    {
        var numbers = new[] { 4, 8, 15, 16, 23, 42 };
        var average = numbers.Fork(xs => xs.Sum(), xs => xs.Length, (sum, count) => sum / count);
        Assert.Equal(18, average);
    }

    [Fact]
    public void Pipe_and_Tap_build_readable_pipelines()
    {
        var log = new List<decimal>();
        var celsius = 100m
            .Pipe(x => x - 32)
            .Tap(log.Add)
            .Pipe(x => x * 5 / 9)
            .Pipe(x => Math.Round(x, 2));

        Assert.Equal(37.78m, celsius);
        Assert.Equal(new[] { 68m }, log);
    }

    // ── Lecture 7: currying, partial application, iterators ───────────────────

    [Fact]
    public void Curry_preserves_argument_order()
    {
        Func<decimal, decimal, decimal> subtract = (x, y) => x - y;
        var curried = subtract.Curry();
        Assert.Equal(7m, curried(10m)(3m));
    }

    [Fact]
    public void Partial_application_fixes_leading_arguments()
    {
        Func<string, string, string, string> format = (a, b, c) => $"{a}-{b}-{c}";
        Assert.Equal("x-y-z", format.Partial("x", "y")("z"));
        Func<int, int, int> multiply = (a, b) => a * b;
        Assert.Equal(12, multiply.Partial(3)(4));
    }

    [Fact]
    public void Scan_yields_the_seed_and_every_intermediate_state()
    {
        var running = new[] { 1, 2, 3 }.Scan(0, (sum, x) => sum + x);
        Assert.Equal(new[] { 0, 1, 3, 6 }, running);
    }

    [Fact]
    public void Generate_is_lazy_and_stopped_by_a_terminating_operator()
    {
        var counter = 0;
        var firstEven = Iteration.Generate(() => ++counter).FirstOrNothing(x => x % 2 == 0);
        Assert.Equal(Maybe.Some(2), firstEven);
        Assert.Equal(2, counter); // only two values were ever produced
    }

    // ── Lecture 8: memoization ────────────────────────────────────────────────

    [Fact]
    public void Memoize_runs_the_function_once_per_key()
    {
        var calls = 0;
        Func<string, int> length = s => { calls++; return s.Length; };
        var memo = length.Memoize();

        Assert.Equal(5, memo("CRANE"));
        Assert.Equal(5, memo("CRANE"));
        Assert.Equal(1, calls);
        Assert.Equal(2, memo("NO"));
        Assert.Equal(2, calls);
    }

    [Fact]
    public void Each_memoized_delegate_has_its_own_cache()
    {
        var calls = 0;
        Func<int, int, int> add = (a, b) => { calls++; return a + b; };

        var first = add.Memoize();
        first(1, 2);
        first(1, 2);
        var second = add.Memoize();
        second(1, 2);

        Assert.Equal(2, calls);  // the second delegate started with an empty cache

        first(2, 1);             // (2, 1) is a different tuple key from (1, 2)
        Assert.Equal(3, calls);
    }

    // ── Lecture 4/5: statistics and safe lookups ──────────────────────────────

    [Fact]
    public void Match_statistics_are_derived_from_the_history()
    {
        var state = (BetweenRounds)Guesses(Guesses(StartedMatch("CRANE"), Alice, 1, "SLATE", "CRANE"), Bob, 5, "SLATE", "TRACE", "CRANE");
        var stats = MatchStatistics.For(state.Duel, Alice.Id);

        Assert.Equal(1, stats.RoundsWon);
        Assert.Equal(1, stats.RoundsSolved);
        Assert.Equal(2, stats.TotalGuesses);
        Assert.Equal(2.0, stats.AverageGuessesWhenSolved);
        Assert.Equal(2.0, stats.FastestSolveSeconds);
    }

    [Fact]
    public void Find_returns_Nothing_instead_of_throwing()
    {
        IReadOnlyDictionary<string, int> scores = new Dictionary<string, int> { ["a"] = 1 };
        Assert.Equal(Maybe.Some(1), scores.Find("a"));
        Assert.IsType<Nothing<int>>(scores.Find("missing"));
    }
}
