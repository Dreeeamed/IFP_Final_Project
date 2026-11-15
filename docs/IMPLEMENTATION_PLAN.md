# Implementation plan (Phase 1 — requirements analysis)

This plan was written after reading all ten lecture decks (L1, L2, L33, L4–L10) and the
*Final Project Requirements* document. It records what was confirmed, what was assumed and how
the work was organised.

## 1. Confirmed requirements (from "Final Requirements CSh.docx")

| Area | Requirement |
|---|---|
| Team | max. 2 students; **all students must program**; obligatory 15–20 min defense; any member may be asked about any part |
| Presentation | team members (full names), topic, aim, goals, plan; analysis of existing technologies + comparison; architecture (UML/MVC/…); relevance/practical significance; methodology/mockups |
| Grading | 20 criteria × 5 %: code structure, component reusability, cross-platform, state management, UI, UX, responsiveness, core functionality, advanced features (e.g. SignalR), error handling, efficiency, scalability, resource utilisation, testing coverage, QA, documentation, innovation, creative use of Blazor/C#/.NET, professionalism, presentation |

## 2. Lecture content actually covered (from the PDFs)

The decks follow Simon J. Painter, *Functional Programming with C#*.

| Deck | Title in the PDF | Key techniques used in this project |
|---|---|---|
| L1 | Functional Programming in C# | imperative vs declarative, first-class functions, immutability, purity, expressions, recursion & state as `Update(old, command)`, functional core / imperative shell |
| L2 | Functions and LINQ in C# | method/delegate/lambda/dictionary as functions, `Where`/`Select`/`Aggregate`, deferred execution, higher-order functions, making dependencies explicit |
| L33 | Modern Functional C# | tuples, switch expressions, type/property/relational/list patterns, records, `with`, `ImmutableList`, nullable analysis |
| L4 | Higher-Order Functions | Func/Action/Predicate, closures, thunks, Map, Fork, Alt, Compose, Transduce, Tap, MapWithTryCatch, open/closed via delegates |
| L5 | Discriminated Unions | class-hierarchy unions, case + payload, Maybe (Something/Nothing), Result (Success/Failure), Either, input classification, pattern matching consumers |
| L6 | Functional Flow (monads) | Bind/Map, railway-oriented programming, OnSomething/OnNothing/OnError, BindAsync, nested flattening, monad laws, Reader, State |
| L7 | Currying, Partial Application, Indefinite Loops | `Curry`, `Partial`, specialised functions, recursion vs trampolining (`IterateUntil`), custom iterators, max-step policies |
| L8 | Memoization | `Memoize` with a closure dictionary, `TryGetValue`, tuple keys, key selectors, purity requirement, thread safety, local lifetime, measuring hit/miss |
| L9 | Functional C# Performance | BenchmarkDotNet, imperative baseline vs LINQ, allocation, "compare implementations that produce the same result", functional libraries |
| L10 | Functional Libraries and Game Architecture | OneOf, LanguageExt, Functional.Maybe, CSharpFunctionalExtensions, Martian Trail: GameState record, phases, command union, injected clock/randomness, smart constructors, core/shell |

## 3. Week mapping — an explicit assumption

The documents do not state which lecture belongs to which week. **We assume lecture *n* = week *n***
(L33 is the third lecture). Therefore:

* **Student A — Weeks 1–5:** L1, L2, L33, L4, L5 (basic discriminated unions).
* **Student B — Weeks 5–10:** L5 (Maybe/Result pipelines), L6, L7, L8, L9, L10.
* **Week 5 is shared:** A defines the union *types* (`Maybe`, `Result`, `RoundOutcome`); B builds the
  monadic *operations* on them (`Map`, `Bind`, railway pipelines) and the match state machine.

## 4. Architecture decision summary

* **Blazor WebAssembly + ASP.NET Core SignalR hub** (instead of Blazor Server): the browser runs the UI,
  the server runs only authoritative game logic. This gives an explicit SignalR boundary, real
  separate client connections, and lets integration tests drive the hub with real SignalR clients.
* **Functional core (`WordDuel.Core`)** with no ASP.NET/Blazor/SignalR/EF dependency; it runs on the
  server *and* in the browser (practice mode).
* **Imperative shell (`WordDuel.Infrastructure`, `WordDuel.Web`)**: rooms, tokens, clock, randomness,
  persistence, logging, SignalR.
* **Own small `Maybe`/`Result` implementation** instead of LanguageExt/OneOf: follows the lectures'
  class-based emulation exactly, keeps the public API dependency-free (L10 "Dependency boundaries").

## 5. Minimum viable feature set (all implemented)

Nickname → create room → code → join → Best-of-3 Blind Duel with server-side validation,
feedback, scoring and timers → round reveal → strategy analysis → match result → rematch;
disconnect/reconnect; error messages; responsive UI; automated tests.

## 6. Optional features (implemented where cheap and safe)

Practice mode (client-side), colour-blind palette, generated sound effects, copy code/link,
match history + leaderboard (SQLite), physical keyboard, animations, reduced-motion support.

Not implemented: accounts/profiles, additional game modes, deployment to a cloud host.

## 7. Technical risks identified up front

| Risk | Mitigation |
|---|---|
| Secret leaking to the browser | the secret only exists in `ActiveRound`; a single pure projection decides what each player sees; tests serialise views and search for the secret |
| Cheating via client messages | identity from the SignalR connection, server-side validation, server clock, throttling, duplicate rejection |
| Concurrency (two guesses at once) | per-room `SemaphoreSlim`; immutable state replaced atomically |
| Stalled rounds / endless draws | 5-minute round deadline, 45 s break auto-start, `MaxRounds = 5` |
| Disconnects | 60 s grace period, token-based resume, forfeit afterwards |
| Word list licensing/quality | allowed-guess list generated from the SCOWL/Hunspell en_US dictionary (permissive licence); 1 680 hand-picked answers checked against it |

## 8. Uncertain points

* Week mapping (see §3).
* "End-to-end tests" in the rubric: implemented as in-memory host + real SignalR clients; no browser
  automation framework (Playwright needs browser downloads). Manual browser testing is documented.
* Exact presentation template: the outline in `PRESENTATION_OUTLINE.md` follows the bullet list in
  the requirements document.
