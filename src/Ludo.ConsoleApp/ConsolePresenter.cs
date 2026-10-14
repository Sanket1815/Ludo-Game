using System.Text;
using Ludo;

namespace Ludo.ConsoleApp;

internal static class ConsolePresenter
{
    private static readonly bool UseColor =
        !Console.IsOutputRedirected && Environment.GetEnvironmentVariable("NO_COLOR") is null;

    public static void Draw(GameEngine game)
    {
        Clear();
        Console.WriteLine("LUDO");

        if (game.IsGameOver && game.Winner is not null)
        {
            Console.WriteLine($"{Paint(game.Winner.Color, game.Winner.Color.ToString())} wins.");
        }
        else
        {
            var current = game.CurrentPlayer;
            string who = $"{current.Color} ({(current.IsHuman ? "human" : "AI")})";
            string streak = game.ConsecutiveSixes > 0 ? $"    sixes this turn: {game.ConsecutiveSixes}" : "";
            string pending = game.PendingRoll is int die ? $"    rolled {die} {Die(die)}" : "";
            Console.WriteLine($"To play: {Paint(current.Color, who)}{streak}{pending}");
        }

        Console.WriteLine();
        WriteTrack(game.Board);
        Console.WriteLine("Clockwise from Red's start at cell 0. A star marks a safe cell.");
        Console.WriteLine();

        foreach (var player in game.Players)
            WriteStretch(player.Color, game.Board.HomeStretches[player.Color]);

        Console.WriteLine();
        foreach (var player in game.Players)
            WritePlayer(player, !game.IsGameOver && player == game.CurrentPlayer);

        Console.WriteLine();
        Console.WriteLine("A 6 leaves the yard and grants another roll. Three 6s in a row forfeits the turn.");
        Console.WriteLine("Land on an opponent to send them home, unless that cell is safe. Home needs an exact roll.");
        Console.WriteLine();

        foreach (var line in game.Log.TakeLast(8))
            Console.WriteLine($"  {line}");
    }

    public static Token? PromptToken(RollResult roll)
    {
        Console.WriteLine("Tokens that can move:");
        for (int i = 0; i < roll.EligibleTokens.Count; i++)
        {
            var token = roll.EligibleTokens[i];
            string label = Paint(token.Color, token.Label);
            Console.WriteLine($"  {i + 1}) {label}  {Describe(token)}  ->  {Preview(token, roll.Value)}");
        }

        while (true)
        {
            Console.Write("Select a token (q quits): ");
            string? line = Console.ReadLine();
            if (line is null || IsQuit(line))
                return null;

            if (int.TryParse(line.Trim(), out int choice) && choice >= 1 && choice <= roll.EligibleTokens.Count)
                return roll.EligibleTokens[choice - 1];

            Console.WriteLine($"Enter a number from 1 to {roll.EligibleTokens.Count}.");
        }
    }

    public static string Preview(Token token, int roll)
    {
        if (token.State == TokenState.InYard)
            return $"enter on cell {TrackMath.StartOffset(token.Color)}";

        int next = token.StepProgress + roll;
        if (next == TrackMath.FinishProgress)
            return "home";
        if (next >= TrackMath.HomeStretchStart)
            return $"home stretch cell {TrackMath.HomeStretchIndex(next) + 1}";

        return $"track cell {TrackMath.TrackIndex(token.Color, next)} (step {next})";
    }

    public static string Describe(Token token) => token.State switch
    {
        TokenState.InYard => "yard",
        TokenState.OnBoard => $"track cell {token.CurrentBoardIndex}, step {token.StepProgress}/57",
        TokenState.InHomeStretch => $"home stretch {token.CurrentBoardIndex + 1}/5, step {token.StepProgress}/57",
        TokenState.Finished => "home",
        _ => token.State.ToString()
    };

    public static void AnnounceWinner(GameEngine game)
    {
        if (game.Winner is not Player winner)
            return;

        Console.WriteLine();
        Console.WriteLine($"{Paint(winner.Color, winner.Color.ToString())} wins after {game.Rolls} rolls and {game.Moves} moves.");
    }

    public static bool IsQuit(string? line) =>
        line is null
        || line.Trim().Equals("q", StringComparison.OrdinalIgnoreCase)
        || line.Trim().Equals("quit", StringComparison.OrdinalIgnoreCase);

    private static void WriteTrack(Board board)
    {
        for (int row = 0; row < 4; row++)
        {
            var line = new StringBuilder();
            for (int col = 0; col < 13; col++)
            {
                if (col > 0)
                    line.Append(' ');
                line.Append(FormatCell(board.Cells[(row * 13) + col]));
            }

            Console.WriteLine(line.ToString());
        }

        var stacks = board.Cells.Where(cell => cell.Occupants.Count > 1).ToList();
        if (stacks.Count == 0)
            return;

        Console.WriteLine("Stacks:");
        foreach (var cell in stacks)
        {
            string names = string.Join(" ", cell.Occupants.Select(token => Paint(token.Color, token.Label)));
            Console.WriteLine($"  cell {cell.Index}: {names}");
        }
    }

    private static string FormatCell(Cell cell)
    {
        string index = cell.Index.ToString("00");
        string mark = cell.IsSafeZone ? "*" : " ";
        var occupants = cell.Occupants;

        if (occupants.Count == 0)
            return $"{index} ..{mark}";

        if (occupants.Count == 1)
            return $"{index} {Paint(occupants[0].Color, occupants[0].Label)}{mark}";

        bool sameColor = occupants.All(token => token.Color == occupants[0].Color);
        string body = sameColor ? $"{occupants[0].Label[0]}+" : "++";
        string painted = sameColor ? Paint(occupants[0].Color, body) : body;
        return $"{index} {painted}{mark}";
    }

    private static void WriteStretch(PlayerColor color, IReadOnlyList<Cell> cells)
    {
        string path = string.Join(" ", cells.Take(5).Select(Mark));
        string name = color.ToString().PadRight(8);
        Console.WriteLine($"{Paint(color, name)} {path}  > {Mark(cells[5])}");
    }

    private static string Mark(Cell cell)
    {
        if (cell.Occupants.Count == 0)
            return "..";

        return string.Join(",", cell.Occupants.Select(token => Paint(token.Color, token.Label)));
    }

    private static void WritePlayer(Player player, bool active)
    {
        string name = player.Color.ToString().PadRight(8);
        string role = player.IsHuman ? "human" : "AI";
        int home = player.Tokens.Count(token => token.State == TokenState.Finished);
        string status = player.IsFinished ? "winner" : $"{home}/4 home";
        Console.WriteLine($"{(active ? ">" : " ")} {Paint(player.Color, name)} {role,-6} {status}");

        foreach (var token in player.Tokens)
            Console.WriteLine($"    {Paint(token.Color, token.Label)}  {Describe(token)}");
    }

    private static string Die(int value) => value switch
    {
        1 => "⚀",
        2 => "⚁",
        3 => "⚂",
        4 => "⚃",
        5 => "⚄",
        6 => "⚅",
        _ => string.Empty
    };

    private static string Paint(PlayerColor color, string text)
    {
        if (!UseColor || text.Length == 0)
            return text;

        string code = color switch
        {
            PlayerColor.Red => "91",
            PlayerColor.Green => "32",
            PlayerColor.Yellow => "93",
            PlayerColor.Blue => "94",
            _ => "0"
        };

        return $"\u001b[{code}m{text}\u001b[0m";
    }

    private static void Clear()
    {
        try
        {
            if (!Console.IsOutputRedirected)
                Console.Clear();
        }
        catch (IOException)
        {
            // A redirected or non-interactive console has nothing to clear.
        }
    }
}
