namespace Ludo.Tests;

public class TrackAndTokenTests
{
    [Theory]
    [InlineData(PlayerColor.Red, 0)]
    [InlineData(PlayerColor.Green, 13)]
    [InlineData(PlayerColor.Yellow, 26)]
    [InlineData(PlayerColor.Blue, 39)]
    public void StartCellsMatchTheColorOffsets(PlayerColor color, int start)
    {
        Assert.Equal(start, TrackMath.StartOffset(color));
        Assert.True(TrackMath.IsSafeZone(start));

        var token = new Token(1, color);
        token.Move(6);
        Assert.Equal(TokenState.OnBoard, token.State);
        Assert.Equal(1, token.StepProgress);
        Assert.Equal(start, token.CurrentBoardIndex);
    }

    [Theory]
    [InlineData(PlayerColor.Red, 1, 0)]
    [InlineData(PlayerColor.Red, 51, 50)]
    [InlineData(PlayerColor.Green, 1, 13)]
    [InlineData(PlayerColor.Green, 51, 11)]
    [InlineData(PlayerColor.Yellow, 1, 26)]
    [InlineData(PlayerColor.Yellow, 51, 24)]
    [InlineData(PlayerColor.Blue, 1, 39)]
    [InlineData(PlayerColor.Blue, 51, 37)]
    public void MainTrackIndexFollowsTheSharedFormula(PlayerColor color, int progress, int expected) =>
        Assert.Equal(expected, TrackMath.TrackIndex(color, progress));

    [Fact]
    public void SafeZonesAreTheStartsAndTheStarredCellsEightAhead()
    {
        var expected = new HashSet<int>();
        foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
        {
            int start = TrackMath.StartOffset(color);
            expected.Add(start);
            expected.Add((start + TrackMath.StarOffset) % TrackMath.TrackLength);
        }

        Assert.Equal(8, expected.Count);
        for (int index = 0; index < TrackMath.TrackLength; index++)
            Assert.Equal(expected.Contains(index), TrackMath.IsSafeZone(index));
    }

    [Fact]
    public void BoardHasAFullTrackAndSixCellHomeStretchPerColor()
    {
        var board = new Board();
        Assert.Equal(52, board.Cells.Length);
        Assert.Equal(4, board.HomeStretches.Count);
        Assert.All(board.HomeStretches, entry => Assert.Equal(6, entry.Value.Count));
        Assert.All(board.Cells, cell => Assert.Equal(TrackMath.IsSafeZone(cell.Index), cell.IsSafeZone));
    }

    [Fact]
    public void TokenWalksTheTrackThenNeedsAnExactRollToFinish()
    {
        var token = new Token(2, PlayerColor.Green);
        Assert.Throws<InvalidOperationException>(() => token.Move(5));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Token(0, PlayerColor.Red));

        token.Move(6);
        token.SetProgress(51);
        Assert.Equal(51, token.StepProgress);
        Assert.Equal(TokenState.OnBoard, token.State);
        Assert.Equal(11, token.CurrentBoardIndex);

        token.Move(1);
        Assert.Equal(TokenState.InHomeStretch, token.State);
        Assert.Equal(0, token.CurrentBoardIndex);

        token.SetProgress(51);
        token.Move(6);
        Assert.Equal(TokenState.Finished, token.State);
        Assert.Equal(57, token.StepProgress);
        Assert.Equal(5, token.CurrentBoardIndex);
        Assert.False(token.CanMove(1));

        token.SetProgress(56);
        Assert.False(token.CanMove(2));
        Assert.Throws<InvalidOperationException>(() => token.Move(2));
        token.Move(1);
        Assert.Equal(TokenState.Finished, token.State);

        token.SendToYard();
        Assert.Equal(TokenState.InYard, token.State);
        Assert.Equal(0, token.StepProgress);
        Assert.Equal(-1, token.CurrentBoardIndex);
    }

    [Fact]
    public void HomeStretchCellsAreSeparateFromTheMainTrack()
    {
        var game = TestFactory.TwoPlayer();
        var token = game.Players[0].Tokens[0];

        game.PlaceToken(token, 52);
        Assert.Equal(TokenState.InHomeStretch, token.State);
        Assert.Contains(token, game.Board.HomeStretches[PlayerColor.Red][0].Occupants);
        Assert.DoesNotContain(game.Board.Cells, cell => cell.Occupants.Contains(token));

        game.PlaceToken(token, 57);
        Assert.Equal(TokenState.Finished, token.State);
        Assert.Contains(token, game.Board.HomeStretches[PlayerColor.Red][5].Occupants);
        Assert.All(
            game.Board.HomeStretches[PlayerColor.Yellow],
            cell => Assert.DoesNotContain(token, cell.Occupants));
    }

    [Theory]
    [InlineData(2, new[] { PlayerColor.Red, PlayerColor.Yellow })]
    [InlineData(3, new[] { PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow })]
    [InlineData(4, new[] { PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Blue })]
    public void SeatingFollowsPlayerCount(int count, PlayerColor[] expected)
    {
        var game = GameEngine.Create(count);
        Assert.Equal(expected, game.Players.Select(player => player.Color));
        Assert.Equal(4, game.Players[0].Tokens.Count);
    }

    [Fact]
    public void TableRejectsAnIllegalLineup()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => GameEngine.Create(1));
        Assert.Throws<ArgumentException>(() => new GameEngine([PlayerColor.Red, PlayerColor.Red]));
        Assert.Throws<ArgumentException>(() =>
            new GameEngine([PlayerColor.Red, PlayerColor.Yellow], new HashSet<PlayerColor> { PlayerColor.Blue }));
    }
}
