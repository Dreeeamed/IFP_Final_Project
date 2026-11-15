using WordDuel.Core.Functional;
using WordDuel.Core.Players;

namespace WordDuel.Core.Match;

/// <summary>Per-player statistics for a match, shown on the match result screen.</summary>
public sealed record PlayerMatchStats(
    int RoundsWon,
    int RoundsSolved,
    int TotalGuesses,
    double AverageGuessesWhenSolved,
    double? FastestSolveSeconds);

/// <summary>Pure LINQ statistics over the immutable round history.</summary>
public static class MatchStatistics
{
    public static PlayerMatchStats For(Duel duel, PlayerId player)
    {
        var records = duel.History.Select(round => round.Results[player]).ToList();
        var solved = records.Where(record => record.Solved).ToList();

        return new PlayerMatchStats(
            RoundsWon: duel.WinsOf(player),
            RoundsSolved: solved.Count,
            TotalGuesses: records.Sum(record => record.Attempts),
            // Fork (Lecture 4): one input, two prongs (sum and count), joined into an average.
            AverageGuessesWhenSolved: solved
                .Select(record => record.Attempts)
                .Fork(attempts => attempts.Sum(), attempts => attempts.Count(), (sum, count) => count == 0 ? 0 : (double)sum / count),
            FastestSolveSeconds: solved.Min(record => record.SolveTime)?.TotalSeconds);
    }
}
