using System.Collections.Immutable;
using WordDuel.Core.Functional;
using static System.FormattableString;

namespace WordDuel.Core.Analysis;

/// <summary>
/// Turns the numbers of an analysis into short, factual sentences.
/// Every message is produced by a small rule function returning Maybe&lt;string&gt;: a rule that does
/// not apply returns Nothing. Adding a new insight means adding one function to a list
/// (open/closed via higher-order functions, Lecture 4).
///
/// Numbers are formatted with the invariant culture: the server's regional settings would otherwise
/// be a hidden input, and the same analysis must always produce the same text (purity).
/// </summary>
public static class Highlights
{
    private delegate Maybe<string> PlayerRule(PlayerStrategy player, int poolSize);

    private delegate Maybe<string> ComparisonRule(PlayerStrategy a, PlayerStrategy b, int poolSize);

    private static readonly ImmutableArray<PlayerRule> PlayerRules = [BestGuess, LuckyGuess, IgnoredClues];

    private static readonly ImmutableArray<ComparisonRule> ComparisonRules = [FewerAttempts, FasterNarrowing];

    public static IReadOnlyList<string> For(PlayerStrategy first, PlayerStrategy second, int poolSize) =>
        ComparisonRules.Select(rule => rule(first, second, poolSize))
            .Concat(PlayerRules.SelectMany(rule => new[] { rule(first, poolSize), rule(second, poolSize) }))
            .Values()
            .ToImmutableArray();

    // ── Comparison rules ────────────────────────────────────────────────────

    private static Maybe<string> FewerAttempts(PlayerStrategy a, PlayerStrategy b, int _) => (a, b) switch
    {
        ({ Solved: true }, { Solved: true }) when a.Attempts != b.Attempts =>
            Some($"{Better(a, b, p => -p.Attempts).Nickname} solved the word using fewer attempts ({Math.Min(a.Attempts, b.Attempts)} vs {Math.Max(a.Attempts, b.Attempts)})."),
        ({ Solved: true }, { Solved: false }) => Some($"{a.Nickname} found the word; {b.Nickname} did not."),
        ({ Solved: false }, { Solved: true }) => Some($"{b.Nickname} found the word; {a.Nickname} did not."),
        _ => None()
    };

    /// <summary>Compares the remaining pools after the same number of guesses.</summary>
    private static Maybe<string> FasterNarrowing(PlayerStrategy a, PlayerStrategy b, int poolSize)
    {
        var step = Math.Min(Math.Min(a.Guesses.Count, b.Guesses.Count), 2);
        if (step == 0) return None();

        var (left, right) = (a.RemainingAfter(step, poolSize), b.RemainingAfter(step, poolSize));
        if (left == right) return None();

        var faster = left < right ? a : b;
        return Some($"{faster.Nickname} narrowed the possibilities faster: {Count(Math.Min(left, right), "candidate")} left after {Count(step, "guess")}, versus {Math.Max(left, right)}.");
    }

    // ── Per-player rules ────────────────────────────────────────────────────

    private static Maybe<string> BestGuess(PlayerStrategy player, int _) =>
        player.BestGuess is { CandidatesBefore: > 1 } best
            ? Some($"{player.Nickname}'s {Ordinal(best.Number)} guess {best.Word} eliminated {best.EliminationPercent:0}% of the remaining candidates ({best.CandidatesBefore} → {best.CandidatesAfter}).")
            : None();

    /// <summary>The actual result was far better than the guess's expected result: luck, not just skill.</summary>
    private static Maybe<string> LuckyGuess(PlayerStrategy player, int _) =>
        player.Guesses.FirstOrNothing(g => g.CandidatesAfter > 0 && g.ExpectedAfter >= 4 * g.CandidatesAfter && g.CandidatesBefore > 10)
            .Map(g => Invariant($"{player.Nickname} got lucky with {g.Word}: it was expected to leave about {g.ExpectedAfter:0.#} words, but left only {g.CandidatesAfter}."));

    /// <summary>A guess that could not be the answer even though few candidates were left.</summary>
    private static Maybe<string> IgnoredClues(PlayerStrategy player, int _) =>
        player.Guesses.FirstOrNothing(g => !g.WasPossibleAnswer && g.CandidatesBefore <= 3 && g.CandidatesBefore > 0)
            .Map(g => Invariant($"{player.Nickname}'s guess {g.Word} could not have been the answer — only {Count(g.CandidatesBefore, "candidate")} remained."));

    private static PlayerStrategy Better(PlayerStrategy a, PlayerStrategy b, Func<PlayerStrategy, int> score) =>
        score(a) >= score(b) ? a : b;

    private static string Count(int n, string noun) => n == 1 ? $"1 {noun}" : $"{n} {noun}{(noun.EndsWith('s') ? "es" : "s")}";

    private static string Ordinal(int n) => n switch
    {
        1 => "1st",
        2 => "2nd",
        3 => "3rd",
        _ => $"{n}th"
    };

    private static Maybe<string> Some(FormattableString text) => new Something<string>(Invariant(text));

    private static Maybe<string> None() => new Nothing<string>();
}
