# Presentation outline (15–20 minutes)

The university checklist requires: team members with full names, topic, aim, goals, plan; analysis of
existing technologies with comparison; architecture (UML/diagrams); relevance / practical significance;
methodology / mockups. The outline below covers all of these, plus the live demo.

Suggested timing: slides ≈ 11 min, live demo ≈ 5 min, buffer ≈ 2 min (questions come afterwards).

| # | Slide | Speaker | Time | Content |
|---|---|---|---|---|
| 1 | **Title** | A | 0:30 | "Word Duel — Blind Arena". **Full names** of Student A and Student B, group, course "Functional Programming in C#", date |
| 2 | **Problem & aim** | A | 0:45 | Word games are usually solo; multiplayer clones show the opponent's board (copying) or trust the client (cheating). **Aim:** a fair, real-time 1-v-1 word duel that also teaches strategy. **Goals:** authoritative server, Blind Duel, best-of-3, explainable analysis, FP architecture |
| 3 | **Plan / methodology** | A | 0:45 | Phases: requirements → functional core + tests → UI → multiplayer → analyzer → polish → docs. Work split by lecture weeks (A: W1–5, B: W5–10). Test-first for the core |
| 4 | **Existing technologies & comparison** | A | 1:00 | Table: Blazor Server vs **Blazor WebAssembly + SignalR** vs React + Node vs Unity. Criteria: language (C# end-to-end), real-time support, cheat resistance, hosting, learning value. Libraries: LanguageExt / OneOf / CSharpFunctionalExtensions vs our own 60-line Maybe/Result |
| 5 | **Why C#, .NET, Blazor** | A | 0:30 | One language on server and browser; the *same* pure core runs in both; SignalR built in; strong typing + records + pattern matching |
| 6 | **Relevance / practical significance** | A | 0:30 | Fair online play, learning tool (analysis teaches information-gain thinking), demonstrates FP in a realistic real-time system |
| 7 | **Mockups / UI** | A | 0:45 | Screenshots: home, lobby, duel (desktop + phone), round reveal, analysis, match result. Design principles: dark theme, Wordle colours + colour-blind mode, responsive grid |
| 8 | **Feature tour** | A | 0:30 | Rooms & codes, physical/on-screen keyboard, timers, reconnection, practice, history/leaderboard |
| 9 | **Student A — FP weeks 1–5** | A | 1:30 | `WordleEvaluator.Evaluate` (pure, Zip/GroupBy/Aggregate, ABBEY/KEBAB example); `PlayerBoard` (records, `with`, list pattern); `RoundScoring.Decide` (tuple switch); `GuessValidator` (rules as Func, Compose); `Maybe`/`Result` as closed unions |
| 10 | **Architecture** | B | 1:15 | Project diagram + functional core / imperative shell (from ARCHITECTURE.md §1–2). Only one mutable field per room |
| 11 | **Blind Duel (innovation 1)** | B | 0:45 | `PlayerViewProjection.Project`: one pure function decides what each player sees; server-authoritative security table |
| 12 | **State machine** | B | 0:45 | `MatchState` union + state diagram; `MatchEngine.Apply` switch over `(state, command)`; `TransitionContext` for time/secret |
| 13 | **Student B — FP weeks 5–10** | B | 1:30 | Railway pipeline in `ApplyGuess` and `SubmitGuessAsync`; Map vs Bind; monad-law tests; currying (`ConsistentWith`) & partial application (`GuessValidator.For`); iterators (`Scan`, `Generate`) |
| 14 | **Strategy Analyzer (innovation 2)** | B | 0:45 | Candidate elimination with the game's own evaluator; expected remaining; highlights as rule functions; memoization |
| 15 | **Performance** | B | 0:45 | Benchmark tables from `docs/PERFORMANCE.md`: imperative evaluator 54× faster, LINQ overhead 15 %, memoization 2.9× — and why the readable version is still the right choice |
| 16 | **Testing & QA** | B | 0:45 | 133 automated tests in 3 layers (pure, service with fake clock, real SignalR); traceability; manual checklist |
| 17 | **Live demo** | A + B | 5:00 | See DEFENSE_GUIDE.md "Live demonstration script" |
| 18 | **Limitations & future work** | B | 0:30 | in-memory rooms (Redis + backplane to scale out), nickname-only identity, no browser automation tests; ideas: hard mode, "best next guess" hint, ranked matchmaking |
| 19 | **Conclusion** | A | 0:20 | Goals met; what we learned about FP (testability, explicit state, measuring performance); thank you |

## Diagrams to put on slides

Copy the Mermaid diagrams from ARCHITECTURE.md (render them at <https://mermaid.live> and export PNG/SVG):

1. Projects and dependencies (§1)
2. Functional core vs imperative shell (§2)
3. State transitions (§4)
4. Guess submission sequence (§5)

## Screenshots to capture before the defense

Run the app, open two windows side by side, and capture: home page, lobby with code, duel (both windows
showing the blind opponent card), finished-early state, round reveal, strategy analysis, match result,
phone layout (DevTools device mode, 375 × 812), colour-blind mode.
