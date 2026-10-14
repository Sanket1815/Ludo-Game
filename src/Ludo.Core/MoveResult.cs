namespace Ludo;

public sealed record MoveResult(
    bool Success,
    Player Player,
    Token? Token,
    int DiceRoll,
    TokenState FromState,
    int FromProgress,
    int ToProgress,
    IReadOnlyList<Token> CapturedTokens,
    bool ExtraTurn,
    bool Won,
    string Message)
{
    public bool Captured => CapturedTokens.Count > 0;

    public static MoveResult Illegal(Player player, int diceRoll, string message) =>
        new(false, player, null, diceRoll, TokenState.InYard, 0, 0, [], false, false, message);
}
