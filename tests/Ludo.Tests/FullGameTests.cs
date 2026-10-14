namespace Ludo.Tests;

public class FullGameTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(99)]
    public void FourAiPlayersPlayToAWinner(int seed)
    {
        var game = Play(seed);

        Assert.NotNull(game.Winner);
        Assert.True(game.Winner.IsFinished);
        Assert.All(game.Winner.Tokens, token =>
        {
            Assert.Equal(TokenState.Finished, token.State);
            Assert.Contains(token, game.Board.HomeStretches[token.Color][5].Occupants);
        });
        Assert.All(game.Players.Where(player => player != game.Winner), player => Assert.False(player.IsFinished));
        Assert.True(game.Rolls > 0);
        Assert.True(game.Moves > 0);
        game.Board.EnsureConsistent(game.Players.SelectMany(player => player.Tokens));
    }

    [Fact]
    public void TheSameSeedRepeatsTheSameGame()
    {
        var first = Play(7);
        var second = Play(7);

        Assert.Equal(first.Winner!.Color, second.Winner!.Color);
        Assert.Equal(first.Rolls, second.Rolls);
        Assert.Equal(first.Moves, second.Moves);
    }

    private static GameEngine Play(int seed)
    {
        var game = GameEngine.Create(4, seed: seed);
        int guard = 0;
        while (!game.IsGameOver)
        {
            Assert.True(++guard < 20000, $"Game did not finish after {game.Rolls} rolls.");
            var roll = game.RollDice();
            if (!roll.RequiresMove)
                continue;

            var move = game.ApplyMove(roll.Player.SelectTokenToMove(roll.Value));
            Assert.True(move.Success, move.Message);
        }

        return game;
    }
}
