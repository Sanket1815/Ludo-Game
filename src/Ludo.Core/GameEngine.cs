namespace Ludo;

/// <summary>
/// Turn loop for Ludo.
/// A 6 brings a yard token onto its start cell (it does not move six spaces)
/// and grants another roll. Three 6s in a row forfeit the rest of the turn;
/// moves already taken stand. Landing on an opponent on a non-safe cell
/// sends every enemy on that cell back to the yard and grants another roll.
/// The home square requires an exact roll. The first player with four tokens
/// home wins.
/// </summary>
public sealed class GameEngine
{
    private readonly IDice _dice;
    private readonly List<string> _log = [];
    private int _currentIndex;
    private int _consecutiveSixes;
    private int? _pendingRoll;

    public GameEngine(
        IReadOnlyList<PlayerColor> colors,
        IReadOnlySet<PlayerColor>? humanColors = null,
        IDice? dice = null)
    {
        ArgumentNullException.ThrowIfNull(colors);
        if (colors.Count is < 2 or > 4)
            throw new ArgumentException("Ludo is played by 2 to 4 players.", nameof(colors));
        if (colors.Distinct().Count() != colors.Count)
            throw new ArgumentException("Player colors must be unique.", nameof(colors));

        foreach (var color in colors)
        {
            if (!Enum.IsDefined(color))
                throw new ArgumentOutOfRangeException(nameof(colors), $"Unknown color {color}.");
        }

        humanColors ??= new HashSet<PlayerColor>();
        foreach (var human in humanColors)
        {
            if (!colors.Contains(human))
                throw new ArgumentException($"{human} is marked human but is not seated.");
        }

        Board = new Board();
        Players = colors.Select(color => new Player(color, humanColors.Contains(color), Board)).ToArray();
        _dice = dice ?? new RandomDice();
        AppendLog($"{CurrentPlayer.Color} to play.");
    }

    public Board Board { get; }

    public IReadOnlyList<Player> Players { get; }

    public Player CurrentPlayer => Players[_currentIndex];

    public int ConsecutiveSixes => _consecutiveSixes;

    public Player? Winner { get; private set; }

    public bool IsGameOver => Winner is not null;

    public bool AwaitingMove => _pendingRoll.HasValue;

    public int? PendingRoll => _pendingRoll;

    public int Rolls { get; private set; }

    public int Moves { get; private set; }

    public IReadOnlyList<string> Log => _log;

    public event Action<RollResult>? DiceRolled;

    public event Action<MoveResult>? MoveApplied;

    public event Action<Player>? TurnStarted;

    public event Action<Player>? GameWon;

    public static GameEngine Create(int playerCount, IEnumerable<PlayerColor>? humanColors = null, int? seed = null)
    {
        var colors = TrackMath.SeatColors(playerCount);
        return new GameEngine(colors, humanColors?.ToHashSet(), new RandomDice(seed));
    }

    public RollResult RollDice()
    {
        EnsureInProgress();
        if (_pendingRoll.HasValue)
            throw new InvalidOperationException("The current roll still needs a token.");

        var player = CurrentPlayer;
        int value = _dice.Roll();
        Rolls++;
        if (value is < 1 or > 6)
            throw new InvalidOperationException($"Dice returned {value}; expected a roll from 1 to 6.");

        if (value == TrackMath.YardExitRoll)
        {
            _consecutiveSixes++;
            if (_consecutiveSixes >= TrackMath.MaxConsecutiveSixes)
            {
                AppendLog($"{player.Color} rolled a third 6 in a row. The turn is forfeited.");
                AdvanceTurn();
                return Publish(new RollResult(player, value, TurnVoided: true, NoLegalMove: false, EligibleTokens: []));
            }
        }
        else
        {
            _consecutiveSixes = 0;
        }

        var eligible = player.GetEligibleTokens(value);
        if (eligible.Count == 0)
        {
            AppendLog($"{player.Color} rolled {value} but has no legal move.");
            AdvanceTurn();
            return Publish(new RollResult(player, value, TurnVoided: false, NoLegalMove: true, eligible));
        }

        _pendingRoll = value;
        AppendLog($"{player.Color} rolled {value}.");
        return Publish(new RollResult(player, value, TurnVoided: false, NoLegalMove: false, eligible));
    }

    public MoveResult ApplyMove(Token token)
    {
        ArgumentNullException.ThrowIfNull(token);
        EnsureInProgress();
        if (_pendingRoll is not int diceRoll)
            throw new InvalidOperationException("Roll the dice before moving a token.");

        var player = CurrentPlayer;
        if (!player.Tokens.Contains(token))
            return MoveResult.Illegal(player, diceRoll, "That token does not belong to the current player.");

        if (!token.CanMove(diceRoll))
            return MoveResult.Illegal(player, diceRoll, $"{token.Label} cannot use a roll of {diceRoll}.");

        var fromState = token.State;
        int fromProgress = token.StepProgress;
        int fromIndex = token.CurrentBoardIndex;

        Board.Relocate(token, () => token.Move(diceRoll));
        var captured = Board.EvaluateCellCollision(token);
        Board.EnsureConsistent(AllTokens);

        bool won = false;
        if (token.State == TokenState.Finished && player.Tokens.TrueForAll(candidate => candidate.State == TokenState.Finished))
        {
            player.IsFinished = true;
            Winner = player;
            won = true;
        }

        bool extraTurn = !won && (diceRoll == TrackMath.YardExitRoll || captured.Count > 0);
        _pendingRoll = null;
        Moves++;

        string message = BuildMoveMessage(player, token, fromState, fromProgress, fromIndex, captured, extraTurn, won);
        AppendLog(message);

        var result = new MoveResult(
            true,
            player,
            token,
            diceRoll,
            fromState,
            fromProgress,
            token.StepProgress,
            captured,
            extraTurn,
            won,
            message);

        if (!won && !extraTurn)
            AdvanceTurn();

        MoveApplied?.Invoke(result);
        if (won)
            GameWon?.Invoke(player);

        return result;
    }

    internal void PlaceToken(Token token, int stepProgress)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (AllTokens.All(candidate => !ReferenceEquals(candidate, token)))
            throw new ArgumentException("Token is not in this game.", nameof(token));
        if (_pendingRoll.HasValue)
            throw new InvalidOperationException("Resolve the pending roll before repositioning a token.");

        Board.Relocate(token, () => token.SetProgress(stepProgress));
        Board.EnsureConsistent(AllTokens);
    }

    internal void SetCurrentPlayer(PlayerColor color)
    {
        if (_pendingRoll.HasValue)
            throw new InvalidOperationException("Resolve the pending roll before changing the active player.");

        for (int i = 0; i < Players.Count; i++)
        {
            if (Players[i].Color != color)
                continue;

            _currentIndex = i;
            _consecutiveSixes = 0;
            return;
        }

        throw new ArgumentException($"{color} is not seated.", nameof(color));
    }

    private IEnumerable<Token> AllTokens => Players.SelectMany(player => player.Tokens);

    private RollResult Publish(RollResult result)
    {
        DiceRolled?.Invoke(result);
        return result;
    }

    private void AdvanceTurn()
    {
        _consecutiveSixes = 0;
        _pendingRoll = null;
        _currentIndex = (_currentIndex + 1) % Players.Count;
        TurnStarted?.Invoke(CurrentPlayer);
    }

    private void EnsureInProgress()
    {
        if (IsGameOver)
            throw new InvalidOperationException("The game is already over.");
    }

    private void AppendLog(string message) => _log.Add(message);

    private static string BuildMoveMessage(
        Player player,
        Token token,
        TokenState fromState,
        int fromProgress,
        int fromIndex,
        IReadOnlyList<Token> captured,
        bool extraTurn,
        bool won)
    {
        var parts = new List<string>
        {
            $"{player.Color} moved {token.Label} from {Describe(fromState, fromProgress, fromIndex)} to {Describe(token.State, token.StepProgress, token.CurrentBoardIndex)}."
        };

        if (captured.Count > 0)
            parts.Add($"Captured {string.Join(" and ", captured.Select(victim => victim.Label))}.");
        if (won)
            parts.Add($"{player.Color} wins.");
        else if (extraTurn)
            parts.Add("Rolls again.");

        return string.Join(' ', parts);
    }

    private static string Describe(TokenState state, int progress, int index) => state switch
    {
        TokenState.InYard => "the yard",
        TokenState.OnBoard => $"track cell {index} (step {progress})",
        TokenState.InHomeStretch => $"home stretch cell {index + 1} (step {progress})",
        TokenState.Finished => "home",
        _ => state.ToString()
    };
}
