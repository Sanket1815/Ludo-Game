namespace Ludo.Tests;

public class TurnRulesTests
{
    [Fact]
    public void ASixBringsATokenOutAndGrantsAnotherRoll()
    {
        var game = TestFactory.TwoPlayer(6, 4, 2);
        var roll = game.RollDice();
        var token = roll.EligibleTokens[0];

        var move = game.ApplyMove(token);

        Assert.Equal(1, token.StepProgress);
        Assert.Equal(0, token.CurrentBoardIndex);
        Assert.True(move.ExtraTurn);
        Assert.Equal(1, game.ConsecutiveSixes);
        Assert.Equal(PlayerColor.Red, game.CurrentPlayer.Color);

        roll = game.RollDice();
        Assert.Equal(4, roll.Value);
        Assert.Equal(0, game.ConsecutiveSixes);
        move = game.ApplyMove(roll.EligibleTokens.Single());
        Assert.Equal(5, token.StepProgress);
        Assert.False(move.ExtraTurn);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);

        roll = game.RollDice();
        Assert.True(roll.NoLegalMove);
        Assert.Equal(PlayerColor.Red, game.CurrentPlayer.Color);
    }

    [Fact]
    public void ThreeConsecutiveSixesForfeitTheTurnAndKeepEarlierMoves()
    {
        var game = TestFactory.TwoPlayer(6, 6, 6);
        var red = game.Players[0].Tokens[0];
        var yellow = game.Players[1].Tokens[0];
        game.PlaceToken(yellow, 39);

        game.ApplyMove(game.RollDice().EligibleTokens.Single(token => token == red));
        Assert.Equal(1, red.StepProgress);
        Assert.Equal(1, game.ConsecutiveSixes);

        game.ApplyMove(game.RollDice().EligibleTokens.Single(token => token == red));
        Assert.Equal(7, red.StepProgress);
        Assert.Equal(2, game.ConsecutiveSixes);

        var forfeited = game.RollDice();

        Assert.True(forfeited.TurnVoided);
        Assert.False(forfeited.RequiresMove);
        Assert.Equal(7, red.StepProgress);
        Assert.Equal(39, yellow.StepProgress);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.Equal(0, game.ConsecutiveSixes);
        Assert.False(game.AwaitingMove);
        Assert.Contains(game.Log, line => line.Contains("forfeited", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void OpeningRollWithoutASixPassesTheTurn()
    {
        var game = TestFactory.TwoPlayer(4);
        var roll = game.RollDice();

        Assert.True(roll.NoLegalMove);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.All(game.Players[0].Tokens, token => Assert.Equal(TokenState.InYard, token.State));
    }

    [Fact]
    public void ASixThatCannotBePlayedPassesTheTurn()
    {
        var game = TestFactory.TwoPlayer(6);
        foreach (var token in game.Players[0].Tokens)
            game.PlaceToken(token, 56);

        var roll = game.RollDice();

        Assert.True(roll.NoLegalMove);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.Equal(0, game.ConsecutiveSixes);
        Assert.All(game.Players[0].Tokens, token => Assert.Equal(56, token.StepProgress));
    }

    [Fact]
    public void OvershootingHomeIsNotALegalMoveAndAnExactFinishWins()
    {
        var game = TestFactory.TwoPlayer(3, 2);
        var reds = game.Players[0].Tokens;
        game.PlaceToken(reds[0], 55);
        for (int i = 1; i < reds.Count; i++)
            game.PlaceToken(reds[i], 57);

        var blocked = game.RollDice();
        Assert.True(blocked.NoLegalMove);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.Null(game.Winner);

        game.SetCurrentPlayer(PlayerColor.Red);
        var finishing = game.RollDice();
        var move = game.ApplyMove(finishing.EligibleTokens.Single());

        Assert.True(move.Won);
        Assert.Equal(PlayerColor.Red, game.Winner?.Color);
        Assert.True(game.Players[0].IsFinished);
        Assert.True(game.IsGameOver);
        Assert.All(reds, token => Assert.Equal(TokenState.Finished, token.State));
        Assert.Throws<InvalidOperationException>(() => game.RollDice());
    }

    [Fact]
    public void IllegalSelectionDoesNotConsumeTheRoll()
    {
        var game = TestFactory.TwoPlayer(6);
        game.RollDice();
        var yellow = game.Players[1].Tokens[0];

        var rejected = game.ApplyMove(yellow);

        Assert.False(rejected.Success);
        Assert.True(game.AwaitingMove);
        var accepted = game.ApplyMove(game.Players[0].Tokens[0]);
        Assert.True(accepted.Success);
        Assert.Equal(1, accepted.ToProgress);
    }

    [Fact]
    public void MovesRequireAPendingRollAndASecondRollIsRejected()
    {
        var game = TestFactory.TwoPlayer(6, 1);
        Assert.Throws<InvalidOperationException>(() => game.ApplyMove(game.Players[0].Tokens[0]));

        game.RollDice();
        Assert.Throws<InvalidOperationException>(() => game.RollDice());
        Assert.Throws<InvalidOperationException>(() => new GameEngine(
            [PlayerColor.Red, PlayerColor.Yellow],
            dice: new ScriptedDice(0)).RollDice());
    }

    [Fact]
    public void AiPrefersACaptureAndPrefersFinishingOverLeavingTheYard()
    {
        var captureGame = TestFactory.TwoPlayer();
        var hunter = captureGame.Players[0].Tokens[0];
        captureGame.PlaceToken(hunter, 1);
        captureGame.PlaceToken(captureGame.Players[1].Tokens[0], 33);
        Assert.Same(hunter, captureGame.Players[0].SelectTokenToMove(6));

        var finishGame = TestFactory.TwoPlayer();
        var leader = finishGame.Players[0].Tokens[0];
        finishGame.PlaceToken(leader, 51);
        Assert.Same(leader, finishGame.Players[0].SelectTokenToMove(6));
        Assert.Throws<InvalidOperationException>(() => finishGame.Players[1].SelectTokenToMove(3));
    }
}
