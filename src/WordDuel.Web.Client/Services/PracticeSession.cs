using WordDuel.Core.Analysis;
using WordDuel.Core.Functional;
using WordDuel.Core.Match;
using WordDuel.Core.Players;
using WordDuel.Core.Words;

namespace WordDuel.Web.Client.Services;

/// <summary>
/// Single-player practice that runs entirely in the browser. It reuses exactly the same pure
/// functions as the server (validator, evaluator, board, analyzer) — evidence that the functional
/// core is independent of networking and UI. Practice is not competitive, so a client-side secret is fine.
/// </summary>
public sealed class PracticeSession
{
    private static readonly PlayerInfo You = new(new PlayerId("you"), "You");

    private readonly WordDictionary _dictionary;
    private readonly Func<string?, Result<ValidGuess>> _validate;

    public PracticeSession(WordDictionary dictionary)
    {
        _dictionary = dictionary;
        _validate = GuessValidator.For(dictionary);
        NewGame();
    }

    public string Secret { get; private set; } = "";

    public PlayerBoard Board { get; private set; } = PlayerBoard.Empty();

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset? FinishedAt { get; private set; }

    public int GamesPlayed { get; private set; }

    public int GamesWon { get; private set; }

    public void NewGame()
    {
        Secret = _dictionary.Answers[Random.Shared.Next(_dictionary.Answers.Length)];
        Board = PlayerBoard.Empty();
        StartedAt = DateTimeOffset.UtcNow;
        FinishedAt = null;
    }

    /// <summary>validate → submit on the immutable board → store the new board (only here is state replaced).</summary>
    public Result<PlayerBoard> Submit(string raw) =>
        _validate(raw)
            .Bind(guess => Board.Submit(Secret, guess))
            .OnSuccess(board =>
            {
                Board = board;
                if (!board.IsFinished) return;
                FinishedAt = DateTimeOffset.UtcNow;
                GamesPlayed++;
                if (board.Status == BoardStatus.Solved) GamesWon++;
            });

    public PlayerStrategy Analyze()
    {
        var solved = Board.Status == BoardStatus.Solved;
        var record = new CompletionRecord(You.Id, solved, Board.AttemptsUsed, solved ? FinishedAt - StartedAt : null);
        return StrategyAnalyzer.AnalyzePlayer(_dictionary.Answers, You, record, Board.Guesses, StrategyAnalyzer.PatternOf.Memoize());
    }

    public int PoolSize => _dictionary.Answers.Length;
}
