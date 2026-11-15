# Contributions

> **Honesty note.** The first complete version of this repository was produced in a single AI-assisted
> session. The split below is therefore a **work allocation plan**, *not* proof of who wrote what.
> Each student must study, rework and extend their modules themselves, record that work in the log
> below and commit it under their own Git identity. Both students must be able to explain the whole system.

Week mapping assumption: lecture *n* = week *n* (L33 = week 3) — see `docs/IMPLEMENTATION_PLAN.md`.

## Student A — Weeks 1–5 (L1, L2, L33, L4, L5)

Focus: pure functions, LINQ, immutability, delegates/lambdas, higher-order functions, basic pattern
matching, union *types*, reusable UI components.

| Area | Files |
|---|---|
| Wordle rules (pure) | `src/WordDuel.Core/Words/WordleEvaluator.cs`, `Words/LetterState.cs` (`LetterState`, `FeedbackPattern`) |
| Dictionary & validation | `Words/WordDictionary.cs`, `Words/EmbeddedWordLists.cs`, `Words/GuessValidator.cs` (`ValidGuess`, rules, `Normalize`) |
| Immutable board & keyboard | `Words/PlayerBoard.cs` (`GuessResult`, `PlayerBoard`, `KeyboardState`) |
| Players | `Players/Nickname.cs` |
| Round scoring (tuple pattern matching) | `Match/RoundScoring.cs` (`CompletionRecord`, `RoundOutcome` union, `Decide`, `Describe`) |
| LINQ statistics | `Match/MatchStatistics.cs`, `Match/Leaderboard.cs` |
| Union types + combinators | `Functional/Maybe.cs`, `Functional/Result.cs`, `Functional/FunctionalExtensions.cs` (`Pipe`, `Tap`, `Compose`, `Fork`), `Common/GameError.cs` |
| Reusable Blazor components | `WordTile`, `GuessRow`, `WordBoard`, `VirtualKeyboard`, `OpponentStatus`, `MatchScoreboard`, `HowToPlay`, `Icon`, `Modal`, `DataTable<TItem>` (all in `src/WordDuel.Web.Client/Components/`) |
| Pages | `Pages/Practice.razor`, `Pages/HowToPlayPage.razor`, `Pages/History.razor`, `Pages/NotFound.razor` |
| Styling | `src/WordDuel.Web/wwwroot/app.css` (tiles, keyboard, animations, responsive rules) |
| Tests | `tests/WordDuel.Tests/Core/WordleEvaluatorTests.cs`, `ValidationAndBoardTests.cs`, `RoundScoringTests.cs`, combinator & statistics tests in `FunctionalToolkitTests.cs` |

Student A should be able to explain: pure vs impure, functional vs imperative, LINQ (`Where`, `Select`,
`Aggregate`, `GroupBy`, deferred execution), immutability and records, first-class functions, delegates,
lambdas, closures, higher-order functions, composition, switch expressions and patterns, and how
`Maybe`/`Result` are modelled as discriminated unions.

## Student B — Weeks 5–10 (L5, L6, L7, L8, L9, L10)

Focus: union-based state machine, Result/Maybe pipelines (Map/Bind), currying & partial application,
iterators, memoization, performance, functional core / imperative shell, SignalR multiplayer.

| Area | Files |
|---|---|
| Monadic operations | `Functional/ResultExtensions.cs`, `Functional/MaybeExtensions.cs` |
| Currying, memoization, iterators | `Functional/Currying.cs`, `Functional/Memoization.cs`, `Functional/Iteration.cs` |
| Match state machine | `Match/MatchState.cs`, `Match/MatchCommand.cs`, `Match/MatchEngine.cs` |
| Strategy analyzer | `Analysis/StrategyAnalyzer.cs`, `Analysis/Highlights.cs` |
| Blind-Duel projection & wire contract | `Views/PlayerView.cs`, `Views/PlayerViewProjection.cs`, `Contracts/HubContracts.cs`, `Common/RoomCode.cs` |
| Performance baseline | `Words/ImperativeEvaluator.cs`, `benchmarks/WordDuel.Benchmarks/Program.cs`, `docs/PERFORMANCE.md` |
| Imperative shell | `src/WordDuel.Infrastructure/*` (`DuelService`, `GameRoom`, `RoomRegistry`, `Abstractions`, `DuelOptions`, `Persistence/MatchHistory.cs`) |
| Server host | `src/WordDuel.Web/Hubs/GameHub.cs` (hub, notifier, round clock), `ServiceRegistration.cs` |
| Client connection & duel UI | `Services/DuelConnection.cs`, `Pages/Room.razor`, components `RoomLobby`, `GameTimer`, `RoundResult`, `StrategyAnalysis`, `MatchResult`, `ConnectionPill`, `ToastHost` |
| Tests | `Core/MatchEngineTests.cs`, `Core/PlayerViewProjectionTests.cs`, `Core/StrategyAnalyzerTests.cs`, `Core/PerformanceBaselineTests.cs`, monad-law/currying/memoization tests in `Core/FunctionalToolkitTests.cs`, `Infrastructure/DuelServiceTests.cs`, `Integration/HubIntegrationTests.cs` |

Student B should be able to explain: discriminated unions, Maybe/Result, Map vs Bind, railway-oriented
programming, monad laws, currying vs partial application, iterators and bounded indefinite loops,
memoization (closures, keys, lifetime, purity), benchmark interpretation, functional core / imperative shell,
SignalR, authoritative server design.

## Shared modules and integration points

| Shared item | Why shared |
|---|---|
| `WordDuel.Web/Program.cs`, `Components/App.razor`, `WordDuel.Web.Client/Program.cs`, `Layout/MainLayout.razor` | composition roots — both students wire their parts here |
| `Services/UserPreferences.cs`, `Services/BrowserInterop.cs`, `Services/ToastService.cs`, `wwwroot/js/wordduel.js` | UI infrastructure used by both students' components |
| `Services/PracticeSession.cs` | A's board + validator, B's analyzer |
| `Pages/Home.razor` | A's form UI, B's `Result`/`BindAsync` connection calls |
| Documentation | both students present it |

Integration contracts between the two halves:

1. **`Result<T>`/`Maybe<T>` types (A) ↔ `Map`/`Bind` (B).** A defines the unions in week 5; B composes them.
2. **`GuessValidator.Validate` (A) → `ValidGuess` → `SubmitGuess` command (B).**
3. **`PlayerBoard.Submit` (A) is called inside `MatchEngine.ApplyGuess` (B).**
4. **`RoundScoring.Decide` (A) is called by `MatchEngine.CompleteRound` (B).**
5. **`WordleEvaluator.Evaluate` (A) is the pattern function inside `StrategyAnalyzer` (B)** — guaranteeing the
   analyzer follows the same duplicate-letter rules.
6. **`PlayerView` records (B) are rendered by A's components** (`WordBoard`, `OpponentStatus`, `MatchScoreboard`).

## How to work in the shared repository

1. Each student configures their own identity once:
   ```bash
   git config user.name "Full Name"
   ```
   ```bash
   git config user.email "student@example.com"
   ```
2. Work on a personal branch, e.g. `student-a/evaluator-refactor` or `student-b/analyzer-hints`.
3. Make small commits that describe *what you did and understood* (e.g. "Rewrite KeyboardState with GroupBy; add tests").
4. Open a pull request; the other student reviews it (this also prepares you for cross-questions).
5. Run `dotnet test WordDuel.sln` before every push.
6. Record the work in the log below with dates and commit hashes.

Suggested genuine contributions (so that each student has code of their own to show):

* **Student A:** add a "hard mode" rule (revealed hints must be used) as an extra validation rule; add a component
  for a per-letter statistics chart; add more evaluator edge-case tests.
* **Student B:** add an analyzer hint "best possible next guess among remaining candidates"; add a
  thread-safe memoizer with `ConcurrentDictionary` + `Lazy` (Lecture 8) and a shared opener cache; add a test
  for simultaneous guesses from both players.

## Actual work log

| Date | Student | Work done (files / functions) | Commit / PR | Hours |
|---|---|---|---|---|
| | A | | | |
| | A | | | |
| | B | | | |
| | B | | | |
| | A + B | | | |

## Defense readiness checklist

- [ ] Student A can walk through `WordleEvaluator.Evaluate` with the ABBEY/KEBAB example.
- [ ] Student A can explain every `switch` arm of `RoundScoring.Decide`.
- [ ] Student B can explain `MatchEngine.ApplyGuess` line by line (railway).
- [ ] Student B can explain why `Memoize` makes `AnalyzeRound` 2.9× faster and why the cache is local.
- [ ] Both can explain why the opponent's guesses never reach the browser (`PlayerViewProjection`).
- [ ] Both have answered the cross-training questions in `DEFENSE_GUIDE.md`.
