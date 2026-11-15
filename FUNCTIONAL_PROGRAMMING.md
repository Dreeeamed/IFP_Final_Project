# Functional programming in Word Duel — lecture-to-code map

Every row points to real code. Paths are relative to `src/` unless they start with `tests/`.
"A" = Student A (weeks 1–5), "B" = Student B (weeks 5–10) — the *planned* owner, see CONTRIBUTIONS.md.
Week mapping assumption: lecture *n* = week *n* (L33 = week 3).

## Summary

| Lecture | Concept | Actual project file / function | Student | Why it is used here |
|---|---|---|---|---|
| L1 | Pure function | `WordDuel.Core/Words/WordleEvaluator.cs` → `Evaluate(secret, guess)` | A | Same inputs always give the same colours; no hidden state → trivially testable and safe to run on server *and* client |
| L1 | Immutability ("return the change explicitly") | `Words/PlayerBoard.cs` → `Submit` returns a **new** `PlayerBoard` via `with` | A | The previous board stays valid; no accidental shared mutation between players |
| L1 | Imperative vs declarative | `WordleEvaluator.Evaluate` (LINQ) vs `Words/ImperativeEvaluator.cs` (loops) | A/B | Both exist and are proven equivalent; benchmarks compare them (L9) |
| L1 | Expressions instead of statements (`(month, leap) switch`) | `Match/RoundScoring.cs` → `Decide` uses `(a, b) switch` | A | The scoring table reads exactly like the written rules |
| L1 | `newState = Update(oldState, command)` | `Match/MatchEngine.cs` → `Apply(state, command, context)` | B | The whole match is a sequence of pure transitions |
| L1 | Functional core / imperative shell | `WordDuel.Core` vs `WordDuel.Infrastructure/DuelService.cs` | B | I/O, time, randomness and locking are isolated at the edge |
| L2 | Functions as values / method groups | `Words/GuessValidator.cs` → `RulesFor` passes `dictionary.IsAllowed` and `GameErrors.NotInDictionary` as `Func`s | A | Rules become data that can be listed, ordered and tested |
| L2 | `Func` / `Action` delegates, lambdas | `GuessValidator.Rule(Func<string,bool>, Func<string,GameError>)`; `FunctionalExtensions.Tap(Action<T>)` | A | Behaviour is passed in, not hard-coded |
| L2 | LINQ `Select` / `Where` / `Aggregate` | `WordleEvaluator.Evaluate` (`Zip`, `Where`, `GroupBy`, `Aggregate`); `FeedbackPattern.From` (`Aggregate`) | A | Two-pass Wordle algorithm without mutable counters |
| L2 | LINQ grouping / aggregation | `Words/PlayerBoard.cs` → `KeyboardState.From` (`SelectMany`, `GroupBy`, `Max`); `Match/Leaderboard.cs` → `Compute` | A | Keyboard colours and leaderboard derived from data, never stored |
| L2 | Deferred execution | `Functional/Iteration.cs` → `Generate` consumed by `Take` in `WordDuel.Infrastructure/Rooms/RoomRegistry.cs` → `Create` | B | Infinite lazy sequence of codes; only as many generated as needed |
| L2 | Make dependencies explicit | `Match/MatchCommand.cs` → `TransitionContext(Now, NextSecret)`; `WordDictionary` parameters | B | The engine never reads the clock or RNG itself |
| L33 | Records and `with` | all states in `Match/MatchState.cs`; `ActiveRound with { Boards = … }` in `MatchEngine.ApplyGuess` | A/B | Value-oriented data with non-destructive updates |
| L33 | Immutable collections | `ImmutableList` (board, history), `ImmutableDictionary` (boards per player), `ImmutableHashSet` (ready/votes), `FrozenSet` in `Words/WordDictionary.cs` | A | Deep immutability, not just read-only interfaces (L33 slide 9) |
| L33 | Property, relational and list patterns | `PlayerBoard.Status`: `[.., { IsCorrect: true }]` (list pattern) and `{ Count: var used } when …`; `WordleEvaluator.Walk.Next`: `(false, > 0)` | A | Branching on the *shape* of data in one expression |
| L33 | Tuples in pipelines | `StrategyAnalyzer.AnalyzePlayer` → `(guess, number, before, after)`; `WordleEvaluator` → `(letter, exact)` | A/B | Temporary grouping of related values inside a LINQ chain |
| L33 | Nullable reference analysis | `Directory.Build.props` → `<Nullable>enable</Nullable>`; solution builds with 0 warnings | shared | Null handled explicitly (`Maybe`, `?`) |
| L4 | Higher-order function accepting a function | `GuessValidator.Validate(rules, raw)`; `StrategyAnalyzer.AnalyzePlayer(…, patternOf)` | A/B | Same algorithm with configurable behaviour (also enables benchmarking/memoizing) |
| L4 | Higher-order function returning a function | `GuessValidator.For(dictionary)`; `Memoization.Memoize`; `Currying.Curry`; `FunctionalExtensions.Compose` | A/B | Configure once, call many times |
| L4 | Closures | `Memoization.Memoize` (captured `Dictionary`); `GuessValidator.For` (captured rule list) | B | The returned function remembers its environment |
| L4 | Map (pipe) | `Functional/FunctionalExtensions.cs` → `Pipe`; used in `Players/Nickname.cs` → `Validate`, `Common/RoomCode.cs` → `Normalize` | A | Top-to-bottom pipelines without temporary variables (named `Pipe` to avoid clashing with `Result.Map`) |
| L4 | Compose | `GuessValidator.Normalize = (null→"").Compose(Trim).Compose(ToUpper)` | A | A new function built from small ones |
| L4 | Fork | `Match/MatchStatistics.cs` → average guesses `Fork(Sum, Count, (s, c) => …)`; `StrategyAnalyzer.AnalyzePlayer` (partition + membership) | A/B | Two independent computations on one input, joined |
| L4 | Tap | `FunctionalExtensions.Tap`; `ResultExtensions.OnSuccess/OnFailure` (logging in `DuelService.Transition`) | A/B | Observe a pipeline without changing it |
| L4 | Open/closed with function lists | `Analysis/Highlights.cs` → `PlayerRules`, `ComparisonRules`; `GuessValidator.RulesFor` | A/B | Add an insight or a rule by adding one function |
| L4 | MapWithTryCatch (improved) | `ResultExtensions.Try(action, onError)` | B | Exceptions at a boundary become `Failure` values *with* a reason |
| L4 | Functions as component parameters | `WordDuel.Web.Client/Components/GameTimer.razor` → `[Parameter] Func<DateTimeOffset> Now` | B | The countdown uses the server-synchronised clock supplied by the page |
| L5 | Discriminated union (class hierarchy, closed) | `Functional/Maybe.cs`, `Functional/Result.cs`, `Match/RoundScoring.cs` (`RoundOutcome`), `Match/MatchState.cs`, `Match/MatchCommand.cs` — all with `private protected` constructors | A (types) / B (state machine) | Each case carries only its own data; no other assembly can add cases |
| L5 | Maybe: Something / Nothing | `Maybe.cs`; `MaybeExtensions.Find`, `FirstOrNothing`; `Duel.Find(id)` | A | Absence without `null` |
| L5 | Result: Success / Failure | `Result.cs`; every fallible operation returns `Result<T>` (`GuessValidator.Validate`, `PlayerBoard.Submit`, `MatchEngine.Apply`, `DuelService.*Async`) | A | Failure is part of the type, not an exception |
| L5 | Pattern matching consumers | `Views/PlayerViewProjection.cs` → `Project` (`state switch`); `RoundScoring.Describe`; `Pages/Room.razor` → `switch (result) { case Success<…> … case Failure<…> … }` | A/B | Each case handled explicitly at the boundary |
| L5 | Classify input before game logic | `GuessValidator.Validate` → `ValidGuess` (internal constructor) | A | Later code can trust the stronger type |
| L5 | Distinguish "not found" from "error" | `Common/GameError.cs` → `RoomNotFound`, `NotInRoom`, `InvalidToken`, `Unexpected` | B | The UI reacts differently (redirect to join vs show error) |
| L6 | Map vs Bind | `Functional/ResultExtensions.cs`, `Functional/MaybeExtensions.cs` | B | Map for plain functions, Bind for steps that can fail (no `Result<Result<T>>`) |
| L6 | Railway-oriented pipeline | `MatchEngine.ApplyGuess`: `Find → ToResult → Ensure → Bind(Submit) → Map → Map`; `DuelService.SubmitGuessAsync`: identity → throttle → validate → transition → view | B | The happy path reads top-to-bottom; any failure skips the rest |
| L6 | BindAsync | `ResultExtensions.BindAsync`; `DuelService.JoinRoomAsync`, `CreateRoomAsync`; `Pages/Home.razor` → `CreateAsync`, `JoinAsync` | B | Mixing async I/O into the railway |
| L6 | OnSomething / OnError style observers | `MaybeExtensions.OnSomething` (in `DuelService.Leave`), `ResultExtensions.OnFailure` (logging) | B | Side effects at well-defined points |
| L6 | Fold with Bind (Aggregate over Results) | `MatchEngine.Replay` | B | A whole match replayed; the first failure stops it |
| L6 | Monad laws | `tests/…/Core/FunctionalToolkitTests.cs` → `Result_left_identity`, `Result_right_identity`, `Result_associativity`, `Maybe_obeys_the_three_laws` | B | Proof that our Bind adds no surprising behaviour |
| L6 | Reader idea (environment injected at run time) | `TransitionContext`, `ProjectionContext` | B | Same benefit, simpler syntax (documented trade-off) |
| L7 | Currying | `Analysis/StrategyAnalyzer.cs` → `ConsistentWith` = `Curry((guess, observed, candidate) => …)`; used as `ConsistentWith(f)(guess)(pattern)` in `Filter` | B | Fixing guess and pattern yields a predicate for `Where` |
| L7 | Partial application | `GuessValidator.For` → `Validate.Partial(rules)`; stored as `_validateGuess` in `DuelService` | B | "Bind a rule set, leaving the candidate as the final input" (L7 slide 69) |
| L7 | Indefinite loop with a max-step policy | `RoomRegistry.Create`: `Generate(nextCode).Take(25).FirstOrNothing(claim)`; `MatchRules.MaxRounds` | B | Never loops forever; reports `ServerFull` instead |
| L7 | Iterators (`yield return`) | `Iteration.Scan` (successive candidate pools), `Iteration.Generate` | B | States produced lazily and consumed with LINQ |
| L7 | Loop lives in one helper (trampolining idea) | `WordDuel.Web/Hubs/GameHub.cs` → `RoundClockService` (`PeriodicTimer`) turns time into `ClockTick` commands | B | The only loop is in the shell; the core stays a pure transition |
| L8 | Memoize with closure dictionary + `TryGetValue` | `Functional/Memoization.cs` → `Memoize<TIn,TOut>` | B | First call computes, later calls reuse |
| L8 | Composite keys | `Memoize<T1,T2,TOut>` with a `ValueTuple` key | B | Structural equality instead of fragile string keys |
| L8 | Local memoization lifetime | `StrategyAnalyzer.AnalyzeRound` → `PatternOf.Memoize()` per round | B | Bounded cache, collected after the analysis |
| L8 | Measure and test memoization | `StrategyAnalyzerTests.Memoization_avoids_repeated_pattern_evaluations` (invocation counter); `docs/PERFORMANCE.md` (2.9× faster) | B | Benefit proven, not assumed |
| L8 | Purity requirement / hidden inputs | `Analysis/Highlights.cs` formats with `FormattableString.Invariant` | B | Server culture would otherwise be a hidden input |
| L9 | Benchmarks with a baseline | `benchmarks/WordDuel.Benchmarks/Program.cs` (`EvaluatorBenchmarks`, `FilterBenchmarks`, `AnalysisBenchmarks`) | B | Honest comparison: imperative 54× faster evaluator, LINQ overhead only 15 % |
| L9 | Same-result verification | `tests/…/Core/PerformanceBaselineTests.cs` | B | Benchmarks compare equivalent code |
| L9/L10 | Library choice | own `Maybe`/`Result` instead of LanguageExt/OneOf (ARCHITECTURE.md §10) | B | Small scope, no third-party types in public API |
| L10 | Game state as one record + phases as functions | `MatchState` + `MatchEngine.Apply` | B | Each turn transforms one immutable state into the next |
| L10 | Command union | `Match/MatchCommand.cs` | B | Closed set of actions, no string comparisons |
| L10 | Injected clock and randomness | `WordDuel.Infrastructure/Abstractions.cs` → `IClock`, `ISecretWordPicker`; fakes in `tests/…/TestSupport/Fakes.cs` | B | Deterministic tests of timers and secrets |
| L10 | Smart construction (`ValidSpend`) | `Words/GuessValidator.cs` → `ValidGuess` | A | Type proves validation happened |
| L10 | Dependency boundaries | `Contracts/HubContracts.cs` → `HubResponse<T>.From(Result<T>)` | B | The wire format is a plain DTO; the union stays inside the code |

## Where functional style was deliberately *not* used

Lecture 1 and Lecture 9 stress choosing the right tool. These parts are intentionally imperative:

* `DuelService`, `GameRoom`, `RoomRegistry` — they *are* the imperative shell (locks, dictionaries of live connections).
* `RoundClockService` — a `while` loop over a `PeriodicTimer`.
* `MaybeExtensions.FirstOrNothing` and `Iteration.Scan` contain a `foreach` inside a small, tested helper
  (the same trade-off as the lecture's `IterateUntil`).
* `ImperativeEvaluator` — the benchmark baseline (still pure: it mutates only local spans).
* Blazor components keep small UI fields (`_input`, `_shake`) — UI interaction is shell code.

## Short explanations for the defense

**Why records?** Value equality and `with` make state transitions explicit; tests can compare whole values.

**Why `Result` instead of exceptions?** An invalid word is an expected outcome, not an exceptional one. The signature
`Result<ValidGuess> Validate(…)` tells every caller that it can fail and forces them to handle it.

**Map vs Bind in one sentence.** `Map` lifts a plain function `A → B` into the container; `Bind` chains a function
`A → Result<B>` and flattens, so the container never nests.

**Why is the engine pure if the game has timers?** The engine never asks for the time. The shell passes `Now`
inside `TransitionContext`, and the background clock sends a `ClockTick` command. Tests simply pass any time they want.

**Why memoize there?** In one analysis, `Partition` and `Filter` both need `PatternOf(candidate, guess)` for the same
pairs, and players often share an opener. The cache lives only for that one round.
