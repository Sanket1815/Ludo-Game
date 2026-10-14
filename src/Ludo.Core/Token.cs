namespace Ludo;

public sealed class Token
{
    public Token(int id, PlayerColor color)
    {
        if (id is < 1 or > 4)
            throw new ArgumentOutOfRangeException(nameof(id), "Each player has tokens 1 through 4.");
        if (!Enum.IsDefined(color))
            throw new ArgumentOutOfRangeException(nameof(color));

        Id = id;
        Color = color;
    }

    public int Id { get; }

    public PlayerColor Color { get; }

    public TokenState State { get; private set; } = TokenState.InYard;

    /// <summary>0 in the yard, 1 through 56 on the path, 57 on the home square.</summary>
    public int StepProgress { get; private set; }

    /// <summary>
    /// Track index while <see cref="TokenState.OnBoard"/>, home-stretch index while
    /// approaching or occupying home, and -1 in the yard.
    /// </summary>
    public int CurrentBoardIndex { get; private set; } = -1;

    public string Label => $"{Color.ToString()[0]}{Id}";

    public bool CanMove(int diceRoll) => TrackMath.CanMove(State, StepProgress, diceRoll);

    /// <summary>
    /// A yard token accepts only a 6, which places it on its start cell.
    /// Every other token advances exactly <paramref name="steps"/> and cannot pass home.
    /// </summary>
    public void Move(int steps)
    {
        if (!CanMove(steps))
        {
            throw new InvalidOperationException(
                $"{Label} cannot move {steps} from {State} at step {StepProgress}.");
        }

        ApplyProgress(State == TokenState.InYard ? 1 : StepProgress + steps);
    }

    public void SendToYard()
    {
        State = TokenState.InYard;
        StepProgress = 0;
        CurrentBoardIndex = -1;
    }

    internal void SetProgress(int stepProgress) => ApplyProgress(stepProgress);

    private void ApplyProgress(int stepProgress)
    {
        if (stepProgress < 0 || stepProgress > TrackMath.FinishProgress)
            throw new ArgumentOutOfRangeException(nameof(stepProgress));

        StepProgress = stepProgress;
        State = TrackMath.StateForProgress(stepProgress);
        CurrentBoardIndex = State switch
        {
            TokenState.InYard => -1,
            TokenState.OnBoard => TrackMath.TrackIndex(Color, stepProgress),
            TokenState.InHomeStretch or TokenState.Finished => TrackMath.HomeStretchIndex(stepProgress),
            _ => -1
        };
    }

    public override string ToString() => $"{Label} {State} step {StepProgress}";
}
