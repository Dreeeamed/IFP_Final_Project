using WordDuel.Core.Common;
using WordDuel.Core.Functional;
using WordDuel.Core.Players;
using WordDuel.Core.Words;
using static WordDuel.Tests.TestSupport.TestData;

namespace WordDuel.Tests.Core;

/// <summary>Student A — validation rules, immutable boards and the keyboard.</summary>
public class GuessValidatorTests
{
    [Theory]
    [InlineData("crane", "CRANE")]
    [InlineData("  Crane ", "CRANE")]
    public void Valid_words_are_normalised(string raw, string expected) =>
        Assert.Equal(expected, Unwrap(GuessValidator.Validate(Dictionary, raw)).Word);

    [Theory]
    [InlineData("", ErrorCode.InvalidInput)]
    [InlineData(null, ErrorCode.InvalidInput)]
    [InlineData("CR4NE", ErrorCode.InvalidInput)]
    [InlineData("CRAN", ErrorCode.WrongLength)]
    [InlineData("CRANES", ErrorCode.WrongLength)]
    [InlineData("ZZZZZ", ErrorCode.NotInDictionary)]
    public void Invalid_input_is_reported_as_a_failure(string? raw, ErrorCode expected) =>
        Assert.Equal(expected, ErrorOf(GuessValidator.Validate(Dictionary, raw)).Code);

    [Fact]
    public void Rules_are_checked_in_order_so_the_first_broken_rule_wins()
    {
        // "AB1" breaks both "letters only" and "five letters"; letters are checked first.
        Assert.Equal(GameErrors.NotLetters, ErrorOf(GuessValidator.Validate(Dictionary, "AB1")));
    }

    [Fact]
    public void Partially_applied_validator_behaves_like_the_full_function()
    {
        var validate = GuessValidator.For(Dictionary);
        Assert.Equal(GuessValidator.Validate(Dictionary, "slate"), validate("slate"));
        Assert.IsType<Failure<ValidGuess>>(validate("QQQQQ"));
    }

    [Fact]
    public void Real_dictionary_accepts_answers_and_common_guesses()
    {
        var dictionary = EmbeddedWordLists.Load();
        Assert.True(dictionary.Answers.Length > 1000);
        Assert.All(dictionary.Answers, word => Assert.True(dictionary.IsAllowed(word)));
        Assert.True(dictionary.IsAllowed("CARTS"));
        Assert.False(dictionary.IsAllowed("XQZTV"));
    }

    [Theory]
    [InlineData("  Alice  ", "Alice")]
    [InlineData("Player   One", "Player One")]
    [InlineData("Айгерим", "Айгерим")]
    public void Nicknames_are_cleaned(string raw, string expected) =>
        Assert.Equal(expected, Unwrap(Nickname.Validate(raw)));

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<script>")]
    [InlineData("ThisNicknameIsWayTooLong")]
    public void Bad_nicknames_are_rejected(string raw) =>
        Assert.Equal(GameErrors.InvalidNickname, ErrorOf(Nickname.Validate(raw)));
}

public class PlayerBoardTests
{
    [Fact]
    public void Submitting_returns_a_new_board_and_leaves_the_old_one_unchanged()
    {
        var empty = PlayerBoard.Empty();
        var next = Unwrap(empty.Submit("CRANE", Valid("SLATE")));

        Assert.Equal(0, empty.AttemptsUsed);
        Assert.Equal(1, next.AttemptsUsed);
        Assert.Equal(BoardStatus.InProgress, next.Status);
    }

    [Fact]
    public void Correct_guess_solves_the_board()
    {
        var board = Unwrap(PlayerBoard.Empty().Submit("CRANE", Valid("CRANE")));
        Assert.Equal(BoardStatus.Solved, board.Status);
        Assert.True(board.IsFinished);
    }

    [Fact]
    public void Six_wrong_guesses_exhaust_the_board_and_a_seventh_is_refused()
    {
        var words = new[] { "SLATE", "ABBEY", "APPLE", "HELLO", "ROBOT", "SPEED" };
        var board = words.Aggregate(PlayerBoard.Empty(), (b, w) => Unwrap(b.Submit("CRANE", Valid(w))));

        Assert.Equal(BoardStatus.OutOfGuesses, board.Status);
        Assert.Equal(0, board.AttemptsLeft);
        Assert.Equal(GameErrors.BoardFinished, ErrorOf(board.Submit("CRANE", Valid("CRANE"))));
    }

    [Fact]
    public void Guessing_the_same_word_twice_is_refused_without_using_an_attempt()
    {
        var board = Unwrap(PlayerBoard.Empty().Submit("CRANE", Valid("SLATE")));
        var error = ErrorOf(board.Submit("CRANE", Valid("SLATE")));

        Assert.Equal(ErrorCode.AlreadyGuessed, error.Code);
        Assert.Equal(1, board.AttemptsUsed);
    }

    [Fact]
    public void Keyboard_keeps_the_best_state_per_letter()
    {
        var board = Unwrap(Unwrap(PlayerBoard.Empty().Submit("CRANE", Valid("TRACE"))).Submit("CRANE", Valid("CRATE")));
        var keys = KeyboardState.From(board.Guesses);

        Assert.Equal(LetterState.Correct, keys['C']);  // yellow in TRACE, green in CRATE
        Assert.Equal(LetterState.Correct, keys['R']);
        Assert.Equal(LetterState.Absent, keys['T']);
        Assert.False(keys.ContainsKey('Z'));
    }
}
