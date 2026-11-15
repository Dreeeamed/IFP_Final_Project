# Defense guide

Format (from the requirements): 15–20 minutes, group call, **every member may be asked about any part**,
refusing to defend = 0. Slide plan: `docs/PRESENTATION_OUTLINE.md`.

## 1. Speaking order and timing

| Block | Who | Time | Content |
|---|---|---|---|
| Opening, problem, plan, technologies, UI | Student A | ~5 min | slides 1–8 |
| FP weeks 1–5 in the code | Student A | ~1.5 min | slide 9 + open files |
| Architecture, Blind Duel, state machine | Student B | ~2.5 min | slides 10–12 |
| FP weeks 5–10, analyzer, performance, testing | Student B | ~3.5 min | slides 13–16 |
| Live demo | A drives player 1, B drives player 2 | ~5 min | script below |
| Limitations, conclusion | B, then A | ~1 min | slides 18–19 |

## 2. Files to have open (in this order)

**Student A**
1. `src/WordDuel.Core/Words/WordleEvaluator.cs` — the two-pass evaluator
2. `src/WordDuel.Core/Words/PlayerBoard.cs` — immutable board, list pattern, `KeyboardState.From`
3. `src/WordDuel.Core/Match/RoundScoring.cs` — tuple switch
4. `src/WordDuel.Core/Words/GuessValidator.cs` — rules as functions, `Compose`, `ValidGuess`
5. `src/WordDuel.Core/Functional/Result.cs` + `Maybe.cs` — closed unions
6. `tests/WordDuel.Tests/Core/WordleEvaluatorTests.cs`
7. `src/WordDuel.Web.Client/Components/WordBoard.razor` + `VirtualKeyboard.razor`

**Student B**
1. `src/WordDuel.Core/Match/MatchEngine.cs` — `Apply` switch, `ApplyGuess` railway
2. `src/WordDuel.Core/Functional/ResultExtensions.cs` — Map/Bind
3. `src/WordDuel.Core/Views/PlayerViewProjection.cs` — Blind Duel
4. `src/WordDuel.Core/Analysis/StrategyAnalyzer.cs` — curried predicate, Scan, memoization
5. `src/WordDuel.Infrastructure/DuelService.cs` — `SubmitGuessAsync`, `Transition`, `WithRoomAsync`
6. `src/WordDuel.Web/Hubs/GameHub.cs`
7. `docs/PERFORMANCE.md`

## 3. Key code walkthroughs (practise these aloud)

### Student A — `WordleEvaluator.Evaluate` with secret ABBEY, guess KEBAB

1. `Zip` compares positions → only index 2 (B = B) is exact.
2. The non-exact secret letters A, B, E, Y are counted with `GroupBy` → `{A:1, B:1, E:1, Y:1}`.
3. `Aggregate` walks K-E-B-A-B with an immutable accumulator `(States, Available)`:
   K → gray; E → yellow (E: 0); B → green (exact); A → yellow (A: 0); B → yellow (B: 0).
4. Result ⬛🟨🟩🟨🟨. A second B would be yellow only because ABBEY really has two B's.
   "No variable is ever modified — each step returns a new accumulator."

### Student A — `RoundScoring.Decide`

"Exactly the rules from the requirements, top to bottom: only one solver wins; nobody solved is a draw;
then fewer guesses; then faster server time; otherwise a draw. It is an expression — a switch over a tuple,
like the `(month, leap) switch` example in Lecture 1."

### Student B — `MatchEngine.ApplyGuess` (railway)

```csharp
state.Round.Boards.Find(command.Player)        // Maybe<PlayerRound>   – is this player in the round?
    .ToResult(GameErrors.NotInRoom)             // Result<PlayerRound>
    .Ensure(_ => now < deadline, …)             // still time left?
    .Bind(round => round.Board.Submit(…))       // may fail: finished / repeated word
    .Map(board => round with { … })             // plain transformation
    .Map(… state with { Round = … })
    .Map(updated => AllFinished ? CompleteRound(…) : updated);
```

"Every step either continues on the success track or switches to the failure track; once a Failure exists,
the remaining lambdas are skipped. `Bind` is used where the step itself returns a Result, `Map` where it
returns a plain value."

### Student B — `PlayerViewProjection.Playing`

"During a round the opponent becomes `OpponentView(nickname, presence, AttemptsUsed, MaxGuesses, IsFinished)`.
The opponent's `Guesses` list is never read, and the secret is only reachable through `RoundSummary`, which
exists only after the round. Tests serialise the view to JSON and assert the words are absent."

## 4. Live demonstration script (≈5 minutes)

Preparation (before the call): `dotnet run --project src/WordDuel.Web --launch-profile http`; two browser
windows side by side (A = left, B = right); optional phone on the same Wi-Fi with the `lan` profile.
Optionally set `RoundSeconds` to 120 for the demo.

1. **A (left):** nickname → *Create private room* → show the code tiles and *Copy invite link*.
2. **B (right):** paste the link → nickname → *Join duel*. Point out both windows switch instantly (SignalR push).
3. **A:** type an invalid word (e.g. `QQQQQ`) → error toast + shake, no attempt used. Then a real guess (`CRANE`).
4. **B:** point at the opponent card: "1 / 6 attempts" — no letters (*Blind Duel*). Optionally open DevTools →
   Network → WS to show the pushed JSON contains no opponent words.
5. Both play until someone solves. Show "Solved — waiting for opponent" on the solver's side and
   "Finished in N attempts" on the other side.
6. When both are finished: verdict, revealed word, both boards, then scroll to the **Strategy analysis**
   (candidates before → after, expected remaining, highlights).
7. **B:** refresh the page mid-round of round 2 → the board comes back (token resume).
8. **A:** *Leave match* → confirm → B gets "Victory — your opponent left". Open *History* → the match is there.
9. If time allows: the phone layout (DevTools device mode) and colour-blind toggle.

## 5. Backup plan

| Problem | Backup |
|---|---|
| No internet | Everything runs locally; NuGet packages are already restored. Use two windows on one laptop. |
| Wi-Fi blocks phone ↔ laptop | Use two windows on the laptop (sessionStorage makes each tab a separate player). |
| Port 5031 busy | `dotnet run --project src/WordDuel.Web --urls http://localhost:5080` |
| Build broken on the defense machine | Run `dotnet publish src/WordDuel.Web -c Release -o publish` the day before and start `publish/WordDuel.Web.exe` (or `dotnet publish/WordDuel.Web.dll`). |
| Demo crashes | Show `dotnet test` (133 green tests) and the screenshots in the slides; walk through the integration test `HubIntegrationTests` which plays a round over real SignalR. |
| Screen sharing too slow for animations | Enable "reduce motion" in the OS; the app disables animations. |

## 6. Likely questions — short, correct answers

### Weeks 1–5 (Student A leads, Student B must also know)

| Question | Answer |
|---|---|
| What is a pure function? Give one from the project. | Output depends only on inputs, no side effects. `WordleEvaluator.Evaluate(secret, guess)`. |
| Is `DuelService.SubmitGuessAsync` pure? | No: it reads the clock, locks, mutates `GameRoom.State` and sends network messages. It *calls* pure functions — that is the imperative shell. |
| Declarative vs imperative here? | `WordleEvaluator` (LINQ: what to compute) vs `ImperativeEvaluator` (loops: how). Both exist and are proven equal by tests. |
| Why records? | Value equality, concise immutable data, `with` for non-destructive updates (`board with { Guesses = … }`). |
| Is `IReadOnlyList` enough for immutability? | No (L33 slide 9): another reference to the list could still change it. We use `ImmutableList`/`ImmutableArray`. |
| Where do you use a higher-order function? | `GuessValidator.Validate(rules, raw)` takes rule functions; `GuessValidator.For` returns a function; `StrategyAnalyzer.AnalyzePlayer` takes the pattern function. |
| What is a closure? | A function plus the variables it captured. `Memoize` returns a lambda that captures its dictionary; `GuessValidator.For` captures the rule list. |
| Delegate vs lambda? | A delegate is the type (`Func<string,bool>`); a lambda is one way to create a value of that type (`w => w.Length > 0`). Method groups (`dictionary.IsAllowed`) are another. |
| Show function composition. | `GuessValidator.Normalize = (null→"").Compose(Trim).Compose(ToUpperInvariant)`. |
| How are duplicates handled correctly? | Greens first; remaining secret letters counted; each yellow consumes one copy (walk through ABBEY/KEBAB). |
| What does the list pattern in `PlayerBoard.Status` mean? | `[.., { IsCorrect: true }]` = "any number of guesses, the last one all green" → Solved. |
| Why `Maybe` instead of null? | Absence is visible in the type; callers must handle `Nothing` (`OrElse`, `Match`, `ToResult`). |

### Weeks 5–10 (Student B leads, Student A must also know)

| Question | Answer |
|---|---|
| What is a discriminated union and how is it closed in C#? | A value that is exactly one of several cases, each with its own data. Abstract record base + sealed records; `private protected` constructor so no other assembly can add cases. |
| Map vs Bind? | Map: `A → B` lifted into Result. Bind: `A → Result<B>` and flatten, avoiding `Result<Result<B>>`. |
| What are the monad laws and where are they tested? | Left identity, right identity, associativity — `FunctionalToolkitTests.Result_*`, `Maybe_obeys_the_three_laws`. |
| Why does your Bind not catch exceptions like the lecture's? | Expected failures are values (`GameError`); exceptions mean bugs and must not be silently converted. Boundaries use `ResultExtensions.Try`. |
| Currying vs partial application — examples? | Currying: `ConsistentWith` turns a 3-argument function into `guess => pattern => candidate => bool`. Partial: `GuessValidator.For` fixes the rule list in one step, leaving `raw → Result`. |
| Where is memoization and why is it correct? | `StrategyAnalyzer.AnalyzeRound` memoizes `PatternOf(candidate, guess)`, which is pure. Cache lives for one round only (bounded memory). Measured 2.9× faster. |
| Would memoizing the hub method be OK? | No — it has side effects; a cache hit would skip them (L8 "Side effects"). |
| Where is an indefinite loop and how is it made safe? | `RoomRegistry.Create`: an infinite lazy sequence of random codes, `Take(25)`, first free → otherwise `ServerFull`. Match length is bounded by `MaxRounds`. |
| How does the engine know the time? | It doesn't. The shell passes `TransitionContext(Now, NextSecret)` and sends `ClockTick` commands each second. |
| Functional core / imperative shell — where is the boundary? | `WordDuel.Core` (pure) vs `DuelService`/`GameHub`/`RoundClockService`. The only mutable game field is `GameRoom.State`, replaced under a lock. |
| How do you stop cheating? | Server computes everything; identity from connection id; token to resume; secret never sent before the round ends; throttle; duplicate rejection. |
| What if both players guess at the same time? | The room's `SemaphoreSlim` serialises commands; each produces a new immutable state. |
| Why is functional code slower in your benchmark? | The evaluator allocates immutable collections per call (3.6 MB per 1 680 evaluations). LINQ itself costs only ~15 %. The absolute cost (0.7 µs per guess) is irrelevant compared to network latency. |
| Why not LanguageExt? | Small scope; our 60-line union matches the lectures; no third-party types in public APIs (L10 "dependency boundaries"). |
| How does reconnection work? | Token in sessionStorage → `Resume(code, token)` after reload or SignalR reconnect; 60 s grace, then `Forfeit`. |

### Cross-training (each student answers about the *other's* module)

**Student A should be able to answer:**
1. What happens, step by step, when Alice presses Enter? (hub → `DuelService.SubmitGuessAsync` → validator → `MatchEngine.Apply` → projection → push)
2. Why can't Bob see Alice's letters even with DevTools open? (projection never copies them)
3. What does `Bind` do when it receives a `Failure`? (returns a new Failure with the same error; skips the lambda)
4. What does the analyzer's "expected ≈ 54" mean? (average pool size after this guess, over all possible answers: Σ size² / N)
5. Why does the round end even if a player walks away? (server deadline + `ClockTick`)

**Student B should be able to answer:**
1. Explain `WordleEvaluator.Evaluate` with EERIE vs CRANE.
2. Why does `KeyboardState.From` use `Max()`? (enum order Absent < Present < Correct)
3. What does `GuessValidator.RulesFor` return and why is the order important? (first failing rule decides the message)
4. What is `ValidGuess` for? (type-level proof of validation; internal constructor)
5. How does `WordBoard` know which row to animate? (`RevealRow` parameter from the page)

## 7. If asked "what would you improve?"

* Move rooms to Redis + SignalR backplane for multiple servers.
* Real accounts (ASP.NET Core Identity) for a trustworthy leaderboard.
* Browser automation tests (Playwright) and component tests (bUnit).
* Analyzer "best next guess" suggestion using the partition metric.
* Thread-safe shared memoization of opener partitions (`ConcurrentDictionary` + `Lazy`, Lecture 8).
