namespace Ludo.Tests;

internal static class TestFactory
{
    public static GameEngine TwoPlayer(params int[] rolls) =>
        new(
            [PlayerColor.Red, PlayerColor.Yellow],
            new HashSet<PlayerColor>(),
            new ScriptedDice(rolls));
}
