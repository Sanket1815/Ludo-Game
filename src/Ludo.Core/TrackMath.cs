namespace Ludo;

/// <summary>
/// Progress-based geometry for the Ludo board. Every color shares one formula.
/// <list type="bullet">
/// <item><description>Step 0: the token is in its yard.</description></item>
/// <item><description>Step 1: the token enters its start cell (Red 0, Green 13, Yellow 26, Blue 39).</description></item>
/// <item><description>Steps 1 through 51: main track index <c>(start + step - 1) % 52</c>.</description></item>
/// <item><description>Steps 52 through 56: home-stretch cells 0 through 4.</description></item>
/// <item><description>Step 57: the home square, which is stretch cell 5. The roll must be exact.</description></item>
/// </list>
/// Safe cells are the four start cells plus the starred cell eight steps ahead of each start.
/// </summary>
public static class TrackMath
{
    public const int TrackLength = 52;
    public const int HomeStretchLength = 6;
    public const int MainTrackSteps = 51;
    public const int HomeStretchStart = 52;
    public const int FinishProgress = 57;
    public const int YardExitRoll = 6;
    public const int MaxConsecutiveSixes = 3;
    public const int StarOffset = 8;

    private static readonly HashSet<int> SafeZones = BuildSafeZones();

    public static int StartOffset(PlayerColor color) => color switch
    {
        PlayerColor.Red => 0,
        PlayerColor.Green => 13,
        PlayerColor.Yellow => 26,
        PlayerColor.Blue => 39,
        _ => throw new ArgumentOutOfRangeException(nameof(color))
    };

    public static IReadOnlyList<PlayerColor> SeatColors(int playerCount) => playerCount switch
    {
        2 => [PlayerColor.Red, PlayerColor.Yellow],
        3 => [PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow],
        4 => [PlayerColor.Red, PlayerColor.Green, PlayerColor.Yellow, PlayerColor.Blue],
        _ => throw new ArgumentOutOfRangeException(nameof(playerCount), "Ludo is played by 2 to 4 players.")
    };

    public static bool IsSafeZone(int trackIndex)
    {
        if ((uint)trackIndex >= TrackLength)
            throw new ArgumentOutOfRangeException(nameof(trackIndex));
        return SafeZones.Contains(trackIndex);
    }

    public static int TrackIndex(PlayerColor color, int stepProgress)
    {
        if (stepProgress < 1 || stepProgress > MainTrackSteps)
            throw new ArgumentOutOfRangeException(nameof(stepProgress));
        return (StartOffset(color) + stepProgress - 1) % TrackLength;
    }

    public static int HomeStretchIndex(int stepProgress)
    {
        if (stepProgress < HomeStretchStart || stepProgress > FinishProgress)
            throw new ArgumentOutOfRangeException(nameof(stepProgress));
        return stepProgress - HomeStretchStart;
    }

    public static TokenState StateForProgress(int stepProgress) => stepProgress switch
    {
        0 => TokenState.InYard,
        >= 1 and <= MainTrackSteps => TokenState.OnBoard,
        >= HomeStretchStart and <= 56 => TokenState.InHomeStretch,
        FinishProgress => TokenState.Finished,
        _ => throw new ArgumentOutOfRangeException(nameof(stepProgress))
    };

    public static bool CanMove(TokenState state, int stepProgress, int diceRoll)
    {
        if (diceRoll is < 1 or > 6)
            return false;

        return state switch
        {
            TokenState.Finished => false,
            TokenState.InYard => diceRoll == YardExitRoll,
            TokenState.OnBoard or TokenState.InHomeStretch => stepProgress + diceRoll <= FinishProgress,
            _ => false
        };
    }

    private static HashSet<int> BuildSafeZones()
    {
        var zones = new HashSet<int>();
        foreach (PlayerColor color in Enum.GetValues<PlayerColor>())
        {
            int start = StartOffset(color);
            zones.Add(start);
            zones.Add((start + StarOffset) % TrackLength);
        }

        return zones;
    }
}
