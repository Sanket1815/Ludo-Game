namespace Ludo.Tests;

public class CollisionTests
{
    [Fact]
    public void LandingOnAnOpponentSendsThemHomeAndGrantsAnotherRoll()
    {
        var game = TestFactory.TwoPlayer(1);
        var red = game.Players[0].Tokens[0];
        var yellow = game.Players[1].Tokens[0];
        game.PlaceToken(red, 3);
        game.PlaceToken(yellow, 30);

        var roll = game.RollDice();
        var move = game.ApplyMove(red);

        Assert.True(move.Success);
        Assert.True(move.Captured);
        Assert.Equal(TokenState.InYard, yellow.State);
        Assert.Equal(0, yellow.StepProgress);
        Assert.Equal(-1, yellow.CurrentBoardIndex);
        Assert.Equal(4, red.StepProgress);
        Assert.Equal(PlayerColor.Red, game.CurrentPlayer.Color);
        Assert.False(game.AwaitingMove);
        Assert.Equal(0, game.ConsecutiveSixes);

        var occupants = game.Board.GetTokensAtCell(3);
        Assert.Single(occupants);
        Assert.Same(red, occupants[0]);
    }

    [Fact]
    public void AStackOfOpponentsIsCapturedTogether()
    {
        var game = TestFactory.TwoPlayer(1);
        var yellow = game.Players[1].Tokens[0];
        var reds = game.Players[0].Tokens.Take(2).ToList();
        game.PlaceToken(yellow, 29);
        foreach (var red in reds)
            game.PlaceToken(red, 4);
        game.SetCurrentPlayer(PlayerColor.Yellow);

        var move = game.ApplyMove(game.RollDice().EligibleTokens.Single());

        Assert.Equal(2, move.CapturedTokens.Count);
        Assert.All(reds, red => Assert.Equal(TokenState.InYard, red.State));
        Assert.True(move.ExtraTurn);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.DoesNotContain(game.Board.GetTokensAtCell(3), token => token.Color == PlayerColor.Red);
    }

    [Fact]
    public void SafeCellsDoNotCapture()
    {
        var game = TestFactory.TwoPlayer(6);
        var red = game.Players[0].Tokens[0];
        var yellow = game.Players[1].Tokens[0];
        game.PlaceToken(red, 3);
        game.PlaceToken(yellow, 35);

        game.RollDice();
        var move = game.ApplyMove(red);

        Assert.False(move.Captured);
        Assert.Equal(TokenState.OnBoard, yellow.State);
        Assert.Equal(35, yellow.StepProgress);
        Assert.Equal(9, red.StepProgress);
        Assert.Equal(8, red.CurrentBoardIndex);
        Assert.True(game.Board.IsSafeZone(8));
        Assert.True(move.ExtraTurn);
        Assert.Contains(yellow, game.Board.GetTokensAtCell(8));
        Assert.Contains(red, game.Board.GetTokensAtCell(8));
    }

    [Fact]
    public void LeavingTheYardOntoAnOccupiedStartDoesNotCapture()
    {
        var game = TestFactory.TwoPlayer(6);
        var red = game.Players[0].Tokens[0];
        var yellow = game.Players[1].Tokens[0];
        game.PlaceToken(yellow, 27);

        var move = game.ApplyMove(game.RollDice().EligibleTokens.Single(token => token == red));

        Assert.Equal(1, red.StepProgress);
        Assert.Equal(0, red.CurrentBoardIndex);
        Assert.False(move.Captured);
        Assert.Equal(27, yellow.StepProgress);
        Assert.Equal(PlayerColor.Red, game.CurrentPlayer.Color);
    }

    [Fact]
    public void SameColorTokensStackWithoutSendingEachOtherHome()
    {
        var game = TestFactory.TwoPlayer(1);
        var first = game.Players[0].Tokens[0];
        var second = game.Players[0].Tokens[1];
        game.PlaceToken(first, 4);
        game.PlaceToken(second, 3);

        game.RollDice();
        var move = game.ApplyMove(second);

        Assert.False(move.Captured);
        Assert.False(move.ExtraTurn);
        Assert.Equal(PlayerColor.Yellow, game.CurrentPlayer.Color);
        Assert.Equal(4, first.StepProgress);
        Assert.Equal(4, second.StepProgress);
        Assert.Equal(2, game.Board.GetTokensAtCell(3).Count);
    }

    [Fact]
    public void HomeStretchMovementDoesNotCapture()
    {
        var game = TestFactory.TwoPlayer(1);
        var red = game.Players[0].Tokens[0];
        var yellow = game.Players[1].Tokens[0];
        game.PlaceToken(red, 54);
        game.PlaceToken(yellow, 30);

        var captured = game.Board.EvaluateCellCollision(red);

        Assert.Empty(captured);
        Assert.Equal(TokenState.OnBoard, yellow.State);
        var move = game.ApplyMove(game.RollDice().EligibleTokens.Single());
        Assert.False(move.Captured);
        Assert.Equal(TokenState.InHomeStretch, red.State);
        Assert.Equal(55, red.StepProgress);
    }
}
