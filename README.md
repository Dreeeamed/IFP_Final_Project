# Word Duel — Blind Arena

A real-time, two-player word-guessing duel built with **C#, .NET 10, Blazor WebAssembly and ASP.NET Core SignalR**,
designed around the functional-programming techniques of the *Functional Programming in C#* course.

Two players join a private room and race to find **the same secret five-letter word** on separate boards.
During a round you only see your opponent's *attempt count* and whether they have finished — never their words
or colours (**Blind Duel**). When both are done, the word and both boards are revealed, followed by an
explainable **Strategy Analyzer** that shows how many candidate answers each guess eliminated.

> Ardak and Bolatbek worked here: See [CONTRIBUTIONS.md](CONTRIBUTIONS.md) for the work split.

---

## Features

| Gameplay | Multiplayer & security | Analysis & UX |
|---|---|---|
| Official Wordle feedback incl. repeated letters | Private rooms with 6-character codes | Post-round candidate-elimination analysis |
| 6 guesses, 1 680 answers, 5 151 allowed words | Server-authoritative rules, scoring and timing | "Expected remaining" and luck detection per guess |
| Best of 3, draws extend the match (max 5 rounds) | Identity bound to the SignalR connection + 256-bit resume token | Match statistics and history / leaderboard (SQLite) |
| 5-minute round timer, 45 s break with ready-up | Reconnect with token, 60 s grace period, forfeit | Practice mode (runs entirely in the browser) |
| Duplicate/invalid guesses never cost an attempt | Throttling, room capacity, server capacity limits | Responsive dark UI, animations, sounds, colour-blind mode, keyboard support |

## Technology stack

* **.NET 10** (LTS), C# 14, nullable reference types
* **Blazor WebAssembly** UI (`WordDuel.Web.Client`) hosted by an **ASP.NET Core** server (`WordDuel.Web`)
* **SignalR** for real-time communication (`/hubs/duel`)
* **Entity Framework Core + SQLite** for match history
* **xUnit** (+ `Microsoft.AspNetCore.Mvc.Testing`, SignalR client) for unit and integration tests
* **BenchmarkDotNet** for performance comparisons
* No JavaScript framework, no external services, no AI APIs. One small JS file for clipboard, storage, keyboard and sound.

## Solution layout

```
WordDuel.sln
├─ src/
│  ├─ WordDuel.Core/            functional core: rules, state machine, scoring, analyzer, views (no web dependencies)
│  ├─ WordDuel.Infrastructure/  imperative shell: rooms, tokens, clock, randomness, orchestration, SQLite history
│  ├─ WordDuel.Web/             ASP.NET Core host: SignalR hub, round clock, HTTP endpoints, static assets
│  └─ WordDuel.Web.Client/      Blazor WebAssembly UI: pages, reusable components, client services
├─ tests/WordDuel.Tests/        unit, service and SignalR integration tests (xUnit)
├─ benchmarks/WordDuel.Benchmarks/  BenchmarkDotNet: imperative vs LINQ vs memoized
└─ docs/                        implementation plan, presentation outline, performance results
```

## Requirements

* [.NET SDK 10.0](https://dotnet.microsoft.com/download) (check with `dotnet --list-sdks`)
* A modern browser (Chrome, Edge, Firefox, Safari). Phones and tablets work too.
* Internet access only for the first `dotnet restore` (NuGet packages). The game itself runs offline on a LAN.

## Run locally

```bash
dotnet restore WordDuel.sln
```

```bash
dotnet run --project src/WordDuel.Web --launch-profile http
```

Open <http://localhost:5031>. The first start creates `src/WordDuel.Web/App_Data/wordduel.db` for match history.

In Visual Studio / Rider: open `WordDuel.sln`, set **WordDuel.Web** as the startup project and press F5.

## Start a two-player match

**Same computer (quickest):** open <http://localhost:5031> in two browser tabs or windows.
Each tab is its own player (the seat token lives in `sessionStorage`, which is per tab).

1. Tab 1: enter a nickname → **Create private room** → copy the code.
2. Tab 2: enter another nickname → **Join with code** → paste → **Join duel**. The first round starts immediately.

**Two devices on the same Wi-Fi:**

```bash
dotnet run --project src/WordDuel.Web --launch-profile lan
```

Find the computer's local IP (`ipconfig` on Windows, `ip addr` / `ifconfig` on Linux/macOS), then open
`http://<that-ip>:5031` on the phone or second laptop. Allow the app through the firewall if prompted.

## Run the tests

```bash
dotnet test WordDuel.sln
```

The suite (133 tests) covers the evaluator, validation, boards, scoring, the match state machine, Blind-Duel
projection, the analyzer, memoization, monad laws, the orchestration service (rooms, tokens, throttling,
timers, disconnects, persistence) and real SignalR connections against an in-memory server.
Details: [TESTING.md](TESTING.md).

## Run the benchmarks

```bash
dotnet run -c Release --project benchmarks/WordDuel.Benchmarks -- --filter * --job short
```

Results from our machine and their interpretation: [docs/PERFORMANCE.md](docs/PERFORMANCE.md).

## Configuration

`src/WordDuel.Web/appsettings.json`, section `WordDuel`:

| Key | Default | Meaning |
|---|---|---|
| `WinsNeeded` | 2 | round wins to take the match (2 = best of 3) |
| `MaxRounds` | 5 | hard cap on rounds when draws happen |
| `RoundSeconds` | 300 | round time limit (server clock) |
| `BreakSeconds` | 45 | pause between rounds unless both players are ready |
| `ReconnectGraceSeconds` | 60 | time a disconnected player has to come back |
| `IdleRoomMinutes` | 30 | inactive rooms are removed |
| `MaxRooms` | 2000 | capacity limit |
| `MinActionIntervalMs` | 200 | anti-spam throttle per player |

## Documentation

| Document | Content |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | functional core / imperative shell, domain model, state machine, SignalR flow, diagrams |
| [FUNCTIONAL_PROGRAMMING.md](FUNCTIONAL_PROGRAMMING.md) | lecture → concept → exact file/function → student |
| [CONTRIBUTIONS.md](CONTRIBUTIONS.md) | planned split, shared parts, work-log template, Git workflow |
| [TESTING.md](TESTING.md) | automated tests, manual checklist, edge cases |
| [DEFENSE_GUIDE.md](DEFENSE_GUIDE.md) | speaking plan, demo script, likely questions and answers |
| [RUBRIC_CHECKLIST.md](RUBRIC_CHECKLIST.md) | all 20 grading criteria with evidence and limitations |
| [docs/PRESENTATION_OUTLINE.md](docs/PRESENTATION_OUTLINE.md) | slide-by-slide outline |
| [docs/IMPLEMENTATION_PLAN.md](docs/IMPLEMENTATION_PLAN.md) | requirements analysis, week mapping assumption, risks |
| [docs/PERFORMANCE.md](docs/PERFORMANCE.md) | measured benchmark results |

## Known limitations

* **Rooms live in server memory.** Restarting the server ends running matches (history is kept in SQLite).
  Scaling to several servers would need a shared store (e.g. Redis) and a SignalR backplane — see ARCHITECTURE.md.
* **Nicknames are not accounts.** The leaderboard groups by nickname; anyone can use any nickname.
* **Practice mode keeps its secret in the browser** (it is non-competitive). Duel secrets never leave the server
  before a round ends.
* **English words only.** The allowed list (5 151 words) is large but not exhaustive; rare valid words may be refused.
* **No automated browser (UI) tests.** UI behaviour was verified manually; see TESTING.md.
* Served over plain HTTP in the `http`/`lan` profiles. Use the `https` profile or a reverse proxy for real deployment.

## Data and licences

* `src/WordDuel.Core/Data/allowed.txt` was generated from the Hunspell **en_US** dictionary (SCOWL, © Kevin Atkinson
  and contributors, permissive licence allowing redistribution with attribution) by expanding affixes and keeping
  five-letter lowercase words, plus every answer word.
* `src/WordDuel.Core/Data/answers.txt` is a hand-curated list of 1 680 common five-letter words.
* Sound effects are generated at runtime with the Web Audio API (no audio files).
