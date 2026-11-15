using WordDuel.Core.Match;
using WordDuel.Core.Players;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student A — pure round outcome rules and tie-breaking.</summary>
public class RoundScoringTests
{
    private static CompletionRecord Solved(PlayerInfo player, int attempts, double seconds) =>
        new(player.Id, true, attempts, TimeSpan.FromSeconds(seconds));

    private static CompletionRecord Unsolved(PlayerInfo player, int attempts) => new(player.Id, false, attempts, null);

    [Fact]
    public void Only_solver_wins_even_with_more_guesses() =>
        Assert.Equal(new RoundWon(Bob.Id, WinReason.OnlySolver), RoundScoring.Decide(Unsolved(Alice, 3), Solved(Bob, 6, 200)));

    [Fact]
    public void Fewer_guesses_beats_faster_time() =>
        Assert.Equal(new RoundWon(Alice.Id, WinReason.FewerGuesses), RoundScoring.Decide(Solved(Alice, 3, 90), Solved(Bob, 4, 20)));

    [Fact]
    public void Equal_guesses_are_broken_by_server_time() =>
        Assert.Equal(new RoundWon(Bob.Id, WinReason.FasterSolve), RoundScoring.Decide(Solved(Alice, 4, 61), Solved(Bob, 4, 60)));

    [Fact]
    public void Nobody_solving_is_a_draw() =>
        Assert.Equal(new RoundDrawn(DrawReason.NobodySolved), RoundScoring.Decide(Unsolved(Alice, 6), Unsolved(Bob, 2)));

    [Fact]
    public void Identical_result_is_a_draw() =>
        Assert.Equal(new RoundDrawn(DrawReason.IdenticalResult), RoundScoring.Decide(Solved(Alice, 3, 40), Solved(Bob, 3, 40)));

    [Fact]
    public void Decision_is_symmetric()
    {
        var a = Solved(Alice, 3, 50);
        var b = Solved(Bob, 5, 10);
        Assert.Equal(RoundScoring.Decide(a, b), RoundScoring.Decide(b, a));
    }
}
