# Performance measurements (Lecture 9)

> "Claims require a baseline and repeatable measurements." — we measured instead of assuming that
> functional code is faster or slower.

## How the numbers were produced

```bash
dotnet run -c Release --project benchmarks/WordDuel.Benchmarks -- --filter * --job short
```

* BenchmarkDotNet 0.15.8, `ShortRun` job (3 warm-ups, 3 iterations), `[MemoryDiagnoser]`
* Machine: 13th Gen Intel Core i7-13620H, Windows 11, .NET 10.0.12 (X64 RyuJIT)
* Raw reports: `benchmarks/results/results/*.md|csv|html` (regenerated on every run)
* **ShortRun numbers are indicative.** Absolute times will differ on other machines; ratios are what matter.

Before benchmarking we proved that the implementations being compared return the same results
(`tests/WordDuel.Tests/Core/PerformanceBaselineTests.cs`: ~57 000 word pairs, repeated-letter cases,
identical filter output). Lecture 9: "Compare implementations that produce the same result."

## 1. Guess evaluation — 1 680 evaluations (one opener vs every answer)

| Implementation | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| `ImperativeEvaluator.Evaluate` (Span, loops, local mutation) | **21.5 µs** | 0 B | 1.00 |
| `WordleEvaluator.Evaluate` + `FeedbackPattern.From` (LINQ, `ImmutableDictionary`, `Aggregate`) | 1 161.7 µs | 3.6 MB | 53.9 |

Per single evaluation that is ~13 ns versus ~0.7 µs.

## 2. Candidate filtering — one guess over the full pool

| Implementation | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| `foreach` + `List` + imperative evaluator | **21.8 µs** | 2.2 KB | 1.00 |
| LINQ `Where` + curried predicate + **functional** evaluator (used in the game) | 1 126.5 µs | 3.5 MB | 51.8 |
| LINQ `Where` + curried predicate + **imperative** evaluator | 25.0 µs | 1.0 KB | 1.15 |

## 3. Full round analysis — two players, shared opener (CRANE)

| Implementation | Mean | Allocated | Ratio |
|---|---:|---:|---:|
| `StrategyAnalyzer.AnalyzeRound(..., memoize: false)` | 4.99 ms | 14.6 MB | 1.00 |
| `StrategyAnalyzer.AnalyzeRound(..., memoize: true)` | **1.70 ms** | 4.0 MB | **0.34** |

## Interpretation

1. **The cost is in the immutable data structures, not in LINQ.** Swapping only the evaluator
   (row 3 of table 2) brings the LINQ pipeline within 15 % of the hand-written loop. The functional
   evaluator allocates an `ImmutableDictionary` and grows an `ImmutableArray` five times per call —
   exactly the "general-purpose abstractions can be expensive in a hot path" lesson from Lecture 9
   (`GroupBy` + `ToDictionary` was the slowest variant in the lecture's elevator puzzle too).
2. **Memoization pays off where work is genuinely repeated.** `Partition` and `Filter` evaluate the same
   (candidate, guess) pairs, and both players opened with the same word. One memoized delegate per round
   makes the analysis 2.9× faster and allocate 3.6× less (Lecture 8).
3. **Does it matter for the game?** No:
   * gameplay evaluates *one* guess per request (~0.7 µs);
   * the analysis runs *once per round* on the server (~1.7 ms).
   Both are far below network latency (tens of milliseconds), so we keep the readable functional
   evaluator as the single source of truth, as Lecture 9 recommends ("the engineering decision depends
   on workload, constraints and team capability").
4. **The faster version is still pure.** `ImperativeEvaluator.Evaluate` mutates only local `Span`s and has
   no observable side effects, so it is referentially transparent. Because `StrategyAnalyzer` receives the
   pattern function as a parameter (a higher-order function), switching implementations is a one-line change
   if a future feature needed analysis at a much larger scale.
