namespace Ludo;

public sealed class Board
{
    public Board()
    {
        Cells = Enumerable.Range(0, TrackMath.TrackLength)
            .Select(index => new Cell(index, TrackMath.IsSafeZone(index)))
            .ToArray();

        HomeStretches = [];
        foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
        {
            HomeStretches[color] = Enumerable.Range(0, TrackMath.HomeStretchLength)
                .Select(index => new Cell(index, isSafeZone: true, homeColor: color))
                .ToList();
        }
    }

    public Cell[] Cells { get; }

    public Dictionary<PlayerColor, List<Cell>> HomeStretches { get; }

    public bool IsSafeZone(int trackIndex) => TrackMath.IsSafeZone(trackIndex);

    public IReadOnlyList<Token> GetTokensAtCell(int trackIndex)
    {
        if ((uint)trackIndex >= (uint)Cells.Length)
            throw new ArgumentOutOfRangeException(nameof(trackIndex));

        return Cells[trackIndex].Occupants.ToArray();
    }

    /// <summary>
    /// Sends every opposing token on the mover's cell back to its yard.
    /// Safe cells and tokens that are not on the main track are left alone.
    /// The engine grants the capturer one extra roll when the returned list is not empty.
    /// </summary>
    public List<Token> EvaluateCellCollision(Token movedToken)
    {
        ArgumentNullException.ThrowIfNull(movedToken);

        if (movedToken.State != TokenState.OnBoard)
            return [];

        int targetIndex = movedToken.CurrentBoardIndex;
        if (IsSafeZone(targetIndex))
            return [];

        var opponentTokens = GetTokensAtCell(targetIndex)
            .Where(token => token.Color != movedToken.Color)
            .ToList();

        foreach (var enemy in opponentTokens)
        {
            // Detach while the token still records its track cell, then drop it in the yard.
            Detach(enemy);
            enemy.SendToYard();
        }

        return opponentTokens;
    }

    internal void Relocate(Token token, Action applyMove)
    {
        Detach(token);
        try
        {
            applyMove();
        }
        catch
        {
            Attach(token);
            throw;
        }

        Attach(token);
    }

    internal void EnsureConsistent(IEnumerable<Token> tokens)
    {
        var tokenList = tokens.ToList();
        var located = new HashSet<Token>();

        void Account(IEnumerable<Cell> cells)
        {
            foreach (var cell in cells)
            {
                var seenInCell = new HashSet<Token>();
                foreach (var token in cell.Occupants)
                {
                    if (!seenInCell.Add(token))
                        throw new InvalidOperationException($"{token.Label} is listed twice on one cell.");
                    if (!located.Add(token))
                        throw new InvalidOperationException($"{token.Label} occupies more than one cell.");
                }
            }
        }

        Account(Cells);
        foreach (var stretch in HomeStretches.Values)
            Account(stretch);

        foreach (var token in tokenList)
        {
            bool onCell = located.Contains(token);
            if (token.State == TokenState.InYard && onCell)
                throw new InvalidOperationException($"{token.Label} is in the yard but still occupies a cell.");
            if (token.State != TokenState.InYard && !onCell)
                throw new InvalidOperationException($"{token.Label} is {token.State} but occupies no cell.");

            switch (token.State)
            {
                case TokenState.InYard:
                    if (token.StepProgress != 0 || token.CurrentBoardIndex != -1)
                        throw new InvalidOperationException($"{token.Label} has a corrupt yard position.");
                    break;
                case TokenState.OnBoard:
                    if (token.CurrentBoardIndex != TrackMath.TrackIndex(token.Color, token.StepProgress))
                        throw new InvalidOperationException($"{token.Label} track index does not match its progress.");
                    if (!Cells[token.CurrentBoardIndex].Occupants.Contains(token))
                        throw new InvalidOperationException($"{token.Label} is missing from its track cell.");
                    break;
                case TokenState.InHomeStretch:
                case TokenState.Finished:
                    if (token.CurrentBoardIndex != TrackMath.HomeStretchIndex(token.StepProgress))
                        throw new InvalidOperationException($"{token.Label} home index does not match its progress.");
                    if (!HomeStretches[token.Color][token.CurrentBoardIndex].Occupants.Contains(token))
                        throw new InvalidOperationException($"{token.Label} is missing from its home cell.");
                    break;
            }
        }

        int expected = tokenList.Count(token => token.State != TokenState.InYard);
        if (located.Count != expected)
            throw new InvalidOperationException("Cell occupants do not match the tokens in play.");
    }

    private void Detach(Token token)
    {
        if (token.State == TokenState.InYard)
            return;

        if (!Locate(token).Remove(token))
            throw new InvalidOperationException($"{token.Label} was not on its recorded cell.");
    }

    private void Attach(Token token)
    {
        if (token.State == TokenState.InYard)
            return;

        Locate(token).Add(token);
    }

    private Cell Locate(Token token) => token.State switch
    {
        TokenState.OnBoard => Cells[token.CurrentBoardIndex],
        TokenState.InHomeStretch or TokenState.Finished => HomeStretches[token.Color][token.CurrentBoardIndex],
        _ => throw new InvalidOperationException($"{token.Label} is not on a cell.")
    };
}
