namespace Ludo;

public sealed class Cell
{
    private readonly List<Token> _occupants = [];

    public Cell(int index, bool isSafeZone, PlayerColor? homeColor = null)
    {
        Index = index;
        IsSafeZone = isSafeZone;
        HomeColor = homeColor;
    }

    public int Index { get; }

    public bool IsSafeZone { get; }

    public PlayerColor? HomeColor { get; }

    public bool IsHomeStretch => HomeColor.HasValue;

    public IReadOnlyList<Token> Occupants => _occupants;

    internal void Add(Token token) => _occupants.Add(token);

    internal bool Remove(Token token) => _occupants.Remove(token);
}
