namespace Ludo;

public sealed record RollResult(
    Player Player,
    int Value,
    bool TurnVoided,
    bool NoLegalMove,
    IReadOnlyList<Token> EligibleTokens)
{
    public bool RequiresMove => !TurnVoided && !NoLegalMove;
}
