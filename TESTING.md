# Testing

## 1. Automated tests

```bash
dotnet test WordDuel.sln
```

Last verified run (this repository, .NET 10.0.401 SDK, Windows 11): **133 passed, 0 failed, 0 skipped** (~1–2 s).
Stop a running `WordDuel.Web` instance first — otherwise Windows locks its DLLs and the build fails (MSB3027).

The suite uses **xUnit**. Three layers, matching the architecture:

| Layer | What runs | Files | Cases |
|---|---|---|---:|
| Functional core (pure unit tests) | plain function calls, no fakes needed | `tests/WordDuel.Tests/Core/*.cs` | 106 |
| Imperative shell (service tests) | `DuelService` with a fake clock, fixed secret, recording notifier, in-memory history | `Infrastructure/DuelServiceTests.cs` | 21 |
| Integration (in-memory host) | the real ASP.NET Core app via `WebApplicationFactory<Program>` + **real SignalR clients** | `Integration/HubIntegrationTests.cs` | 6 |

### Test inventory

| File | Cases | Covers |
|---|---:|---|
| `WordleEvaluatorTests` | 15 | basic feedback, **7 repeated-letter cases** (EERIE, LLAMA, PAPPY, KEBAB, OOOOO, ERASE, SASSY), determinism, property check over 22 500 real pairs (coloured tiles ≤ letter count), length mismatch, pattern encoding round-trip |
| `ValidationAndBoardTests` | 23 | normalisation, empty/non-letter/wrong-length/unknown words, rule order, partial application, real dictionary, nicknames (incl. Cyrillic, too long, HTML), immutable board updates, solving, **6-guess limit**, duplicate guesses, keyboard colours |
| `RoundScoringTests` | 6 | only solver wins, fewer guesses, **server-time tie-break**, nobody solved, identical result draw, symmetry |
| `MatchEngineTests` | 21 | match start, room capacity, non-member guesses, guesses before start / after deadline, round completion, all tie-break paths through the engine, draws, **deadline via `ClockTick`**, ready-up, auto-start, **best of 3**, round limit (draw and leader), rematch, forfeit, invalid actions as values, `Replay` fold, used secrets |
| `PlayerViewProjectionTests` | 6 | **Blind Duel**: serialised views contain neither the opponent's words nor the secret during a round, also after the opponent finished; own board visible; reveal after the round; waiting room; rematch availability |
| `StrategyAnalyzerTests` | 9 | filtering = evaluator-consistent, **duplicate letters in filtering** (LABEL vs KEBAB, EERIE), currying, partition maths, before/after counts, memoized = plain results, **memoization halves evaluations** (invocation counter), highlights from real numbers |
| `FunctionalToolkitTests` | 20 | **monad laws** for Result and Maybe, short-circuiting, Map vs Bind, Ensure/observers, `Try`, `BindAsync`, Compose, Fork, Pipe/Tap, Curry, Partial, Scan, lazy Generate, Memoize (per key, per delegate, tuple keys), statistics, safe lookup |
| `PerformanceBaselineTests` | 6 | imperative and functional evaluators agree on ~57 000 pairs and on repeated-letter cases; imperative and LINQ filters keep the same words |
| `DuelServiceTests` | 21 | room creation, invalid nicknames/codes, unknown room, **room capacity**, **unauthorised guesses**, identity bound to connection, token required to resume, resume moves the seat, **throttling**, invalid/duplicate guesses free, pushed views never leak words, round completion with analysis, **stalled round ended by server clock**, **disconnect → grace → forfeit → history saved**, full best-of-3 + rematch, closing the waiting room, server capacity |
| `HubIntegrationTests` | 6 | two real SignalR connections create/join and receive the start, attempt counts sync but words stay blind (checked on the received JSON), unauthorised/invalid hub calls, round reveal + analysis to both players, resume on a new connection with the token, `/health` and `/api/history` |

### Requirement → test traceability

| Requirement | Test(s) |
|---|---|
| Correct letter feedback | `WordleEvaluatorTests.Basic_feedback` |
| Repeated-letter feedback | `WordleEvaluatorTests.Repeated_letters_follow_the_official_rules`, `Number_of_coloured_tiles_never_exceeds_letter_count_in_secret` |
| Invalid word length | `GuessValidatorTests.Invalid_input_is_reported_as_a_failure` (CRAN, CRANES), `HubIntegrationTests.Server_rejects_unauthorised_and_invalid_actions` |
| Words not in dictionary | same as above (ZZZZZ / QQQQQ) |
| Maximum guess attempts | `PlayerBoardTests.Six_wrong_guesses_exhaust_the_board_and_a_seventh_is_refused` |
| Game state transitions | `MatchEngineTests.*` |
| Round scoring / tie-breaking / draws | `RoundScoringTests.*`, `MatchEngineTests.Equal_guess_count_is_decided_by_server_measured_time`, `Nobody_solving_is_a_draw_and_awards_no_point` |
| Candidate filtering | `StrategyAnalyzerTests.Filter_*`, `Filtering_respects_repeated_letters_like_the_game` |
| Analyzer calculations | `StrategyAnalyzerTests.Partition_computes_expected_remaining_candidates`, `Player_analysis_tracks_candidates_before_and_after_each_guess` |
| Room creation / joining / capacity / invalid codes | `DuelServiceTests.Creating_a_room_*`, `Joining_starts_*`, `A_room_holds_exactly_two_players`, `Malformed_room_codes_are_rejected`, `Unknown_room_codes_are_reported` |
| Unauthorised guess submissions | `DuelServiceTests.A_connection_without_a_seat_cannot_submit_guesses`, `Identity_comes_from_the_connection_*`, `Room_code_alone_does_not_grant_access_to_a_seat` |
| Duplicate actions | `PlayerBoardTests.Guessing_the_same_word_twice_*`, `DuelServiceTests.Rapid_repeated_actions_are_throttled`, `Invalid_and_duplicate_guesses_do_not_use_attempts` |
| Round completion | `MatchEngineTests.Only_solver_wins_the_round`, `DuelServiceTests.Round_completes_when_both_finish_and_includes_the_analysis` |
| Match completion | `MatchEngineTests.First_to_two_round_wins_takes_the_best_of_three`, `DuelServiceTests.Full_best_of_three_and_rematch` |
| Multiplayer synchronisation | `HubIntegrationTests.*` (real SignalR) |
| Secret never sent early | `PlayerViewProjectionTests.*`, `DuelServiceTests.Pushed_views_never_contain_*`, `HubIntegrationTests.Guess_counts_are_synchronised_but_the_words_stay_blind` |

### Why the core is easy to test

Pure functions need no mocks: `MatchEngine.Apply(state, command, new TransitionContext(anyTime, anySecret))`
returns a value we can assert on. Time and randomness are injected in the shell (`IClock`, `ISecretWordPicker`),
so even the 60-second reconnect grace period is tested in microseconds by advancing a `FakeClock`.

## 2. Manual testing checklist

Legend: ✅ verified during development in this repository (Chromium-based browser pane, two tabs, desktop and
375 × 812 phone emulation); ☐ still to be done by the team before the defense (needs real devices/browsers).

### Two browser windows (same machine)

| # | Scenario | Expected | Status |
|---|---|---|---|
| 1 | Tab 1 creates a room | lobby with 6-character code, copy buttons, waiting animation | ✅ |
| 2 | Tab 2 opens `/join/CODE`, enters nickname, joins | both tabs switch to the duel within ~1 s; timer starts at 5:00 | ✅ |
| 3 | Physical keyboard typing + Enter | letters appear, guess is scored, keyboard keys recoloured | ✅ |
| 4 | On-screen keyboard | same as physical keyboard; Enter/Backspace keys work | ☐ (click-through on a touch device) |
| 5 | Invalid word (ZZZZZ) | toast "ZZZZZ is not in the word list.", row shakes, no attempt used | ✅ |
| 6 | Fewer than 5 letters + Enter | toast "Not enough letters" | ✅ |
| 7 | Same word twice | toast "You already tried …", no attempt used | ✅ |
| 8 | Opponent's view after my guess | shows "1 / 6 attempts", never my letters | ✅ |
| 9 | I solve first | my board shows "Solved in N! Waiting for …"; opponent sees only "Finished in N attempts" | ✅ |
| 10 | Both finish | both see the verdict, the secret, both boards and the strategy analysis | ✅ |
| 11 | Ready for next round (both) | round 2 starts with empty boards | ✅ |
| 12 | Break timer expires without "ready" | next round starts after 45 s | ☐ |
| 13 | Round timer expires | round ends; unfinished player counts as unsolved | ✅ (automated); ☐ manual |
| 14 | Best of 3 completes | match result with statistics; rematch button | ☐ manual (✅ automated) |
| 15 | Rematch (both click) | new match, score 0:0, round 1 | ☐ manual (✅ automated) |
| 16 | Leave match → confirm | leaving player returns home; opponent sees "Victory! Your opponent left", rematch unavailable | ✅ |
| 17 | History page | finished match and leaderboard appear | ✅ |
| 18 | Practice mode | solo game, reveal, analysis | ✅ |

### Disconnects and reconnection

| # | Scenario | Expected | Status |
|---|---|---|---|
| 19 | Refresh a player's tab mid-round | same board restored, connection pill "Live" | ✅ |
| 20 | Close a tab mid-round | opponent sees "… lost connection" banner; after 60 s opponent wins by forfeit | ☐ manual (✅ automated) |
| 21 | Stop and restart Wi-Fi on a phone for < 60 s | "Reconnecting" banner, then the game continues | ☐ |
| 22 | Stop the server | clients show "Connection lost — reconnecting"; after restart the room is gone → error card with "Join with this code" / "Back to menu" | ☐ |
| 23 | Open `/room/CODE` in a fresh tab without a seat | redirected to the join screen with the code pre-filled | ✅ |

### Browsers and devices

| # | Scenario | Status |
|---|---|---|
| 24 | Chrome / Edge (desktop) | ✅ Chromium |
| 25 | Firefox (desktop) | ☐ |
| 26 | Safari (macOS or iOS) | ☐ |
| 27 | Android phone over LAN (`--launch-profile lan`) | ☐ |
| 28 | Phone layout 375 × 812 (board + keyboard fit without scrolling) | ✅ emulated |
| 29 | Tablet layout 768 × 1024 | ☐ |
| 30 | Colour-blind mode toggle | ☐ visual check |
| 31 | `prefers-reduced-motion` (OS setting) disables animations | ☐ |
| 32 | Keyboard-only navigation (Tab through header, home form, keys) | ☐ |

## 3. Important edge cases (and where they are handled)

* Repeated letters in guess and/or secret — `WordleEvaluator` (two-pass algorithm), 7 dedicated tests.
* Guess arriving after the deadline but before the next tick — refused by `MatchEngine.ApplyGuess` (`Ensure`).
* Both players submitting at the same moment — serialised by the per-room `SemaphoreSlim`.
* Double-click / network retry — duplicate word refused; 200 ms throttle.
* Player finishes early — opponent sees only `Finished`; secret withheld until both are done.
* Endless draws — `MaxRounds = 5`; equal wins after the cap → match draw.
* Host leaves the waiting room — room closed; the code becomes invalid.
* Forfeit, then rematch request — refused by the engine.
* Same browser, two tabs — separate seats because tokens are in `sessionStorage`.
* Stale or out-of-order pushes — client ignores views older than the current one.
* Database unavailable — history save failure is logged; the match continues.

## 4. Reproducing scenarios quickly

* **Short rounds for demos:** set `"RoundSeconds": 60` and `"BreakSeconds": 10` in `appsettings.Development.json`.
* **Know the answer while testing locally:** in tests, `FixedSecretPicker` is injected. For a manual demo you can
  temporarily register it in `ServiceRegistration.cs` (never in production).
* **Simulate a disconnect:** close the tab, or use the browser's DevTools → Network → Offline.
* **Inspect what the server sends:** DevTools → Network → WS → `/hubs/duel` → Messages. During a round the
  opponent's words never appear in these frames.

## 5. Known testing limitations

* No automated browser/UI tests (e.g. Playwright/bUnit); UI flows were verified manually (table above).
* Load testing with many concurrent rooms was not performed; the per-room cost was benchmarked instead
  (`docs/PERFORMANCE.md`).
* Code-coverage percentage is not reported by default (`coverlet.collector` is referenced:
  `dotnet test --collect:"XPlat Code Coverage"` produces a Cobertura report).
