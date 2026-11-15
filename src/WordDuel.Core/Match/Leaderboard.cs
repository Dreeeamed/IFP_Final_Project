using System.Collections.Immutable;

namespace WordDuel.Core.Match;

/// <summary>A finished match as stored in the history (public data only: nicknames and scores).</summary>
public sealed record FinishedMatch(
    DateTime FinishedAtUtc,
    string FirstNickname,
    string SecondNickname,
    int FirstWins,
    int SecondWins,
    int Draws,
    string? WinnerNickname,
    string EndReason);

public sealed record LeaderboardEntry(string Nickname, int Matches, int Wins, int RoundWins, double WinRate);

/// <summary>Pure LINQ aggregation over match history (Lecture 2: SelectMany, GroupBy, Select, OrderBy).</summary>
public static class Leaderboard
{
    public static ImmutableArray<LeaderboardEntry> Compute(IEnumerable<FinishedMatch> matches, int top) =>
        matches
            // Each match contributes one row per player …
            .SelectMany(match => new[]
            {
                (Name: match.FirstNickname, Won: match.WinnerNickname == match.FirstNickname, Rounds: match.FirstWins),
                (Name: match.SecondNickname, Won: match.WinnerNickname == match.SecondNickname, Rounds: match.SecondWins)
            })
            // … rows are grouped per nickname (case-insensitive) …
            .GroupBy(row => row.Name, StringComparer.OrdinalIgnoreCase)
            // … and each group is reduced to one leaderboard line.
            .Select(group => new LeaderboardEntry(
                Nickname: group.First().Name,
                Matches: group.Count(),
                Wins: group.Count(row => row.Won),
                RoundWins: group.Sum(row => row.Rounds),
                WinRate: Math.Round(100.0 * group.Count(row => row.Won) / group.Count(), 1)))
            .OrderByDescending(entry => entry.Wins)
            .ThenByDescending(entry => entry.WinRate)
            .ThenBy(entry => entry.Nickname, StringComparer.OrdinalIgnoreCase)
            .Take(top)
            .ToImmutableArray();
}
