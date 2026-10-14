namespace Ludo;

public sealed class Player
{
    private readonly Board _board;

    public Player(PlayerColor color, bool isHuman, Board board)
    {
        if (!Enum.IsDefined(color))
            throw new ArgumentOutOfRangeException(nameof(color));

        Color = color;
        IsHuman = isHuman;
        _board = board ?? throw new ArgumentNullException(nameof(board));
        Tokens = Enumerable.Range(1, 4).Select(id => new Token(id, color)).ToList();
    }

    public PlayerColor Color { get; }

    public bool IsHuman { get; }

    public bool IsFinished { get; internal set; }

    public List<Token> Tokens { get; }

    public List<Token> GetEligibleTokens(int diceRoll) =>
        Tokens.Where(token => token.CanMove(diceRoll)).ToList();

    /// <summary>
    /// Chooses a move for an AI player. A capture outranks finishing, which outranks
    /// leaving the yard, which outranks plain progress. Passing over a token never counts;
    /// only the cell where the token stops can capture.
    /// </summary>
    public Token SelectTokenToMove(int diceRoll)
    {
        var eligible = GetEligibleTokens(diceRoll);
        if (eligible.Count == 0)
            throw new InvalidOperationException($"{Color} has no legal move for a roll of {diceRoll}.");

        return eligible
            .Select(token => (token, score: ScoreMove(token, diceRoll)))
            .OrderByDescending(choice => choice.score)
            .ThenByDescending(choice => choice.token.StepProgress)
            .ThenBy(choice => choice.token.Id)
            .First()
            .token;
    }

    public override string ToString() => $"{Color} ({(IsHuman ? "human" : "AI")})";

    private int ScoreMove(Token token, int diceRoll)
    {
        int progressAfter = token.State == TokenState.InYard ? 1 : token.StepProgress + diceRoll;
        int score = progressAfter;

        if (token.State == TokenState.InYard)
            score += 45;

        if (progressAfter == TrackMath.FinishProgress)
            score += 120;

        if (token.StepProgress <= TrackMath.MainTrackSteps && progressAfter >= TrackMath.HomeStretchStart)
            score += 30;

        if (CountCaptures(token, progressAfter) is int captures and > 0)
            score += captures * 90;

        return score;
    }

    private int CountCaptures(Token token, int progressAfter)
    {
        if (progressAfter is < 1 or > TrackMath.MainTrackSteps)
            return 0;

        int index = TrackMath.TrackIndex(token.Color, progressAfter);
        if (_board.IsSafeZone(index))
            return 0;

        return _board.GetTokensAtCell(index).Count(other => other != token && other.Color != token.Color);
    }
}
