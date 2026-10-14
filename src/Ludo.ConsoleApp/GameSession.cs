using Ludo;

namespace Ludo.ConsoleApp;

internal static class GameSession
{
    public static GameEngine ConfigureInteractively()
    {
        Console.WriteLine("LUDO");
        Console.WriteLine("Two players sit opposite each other, as Red and Yellow.");
        int count = AskInt("How many players? (2-4) ", 2, 4);
        var colors = TrackMath.SeatColors(count);
        Console.WriteLine($"Seated: {string.Join(", ", colors)}");

        var humans = new HashSet<PlayerColor>();
        foreach (var color in colors)
        {
            while (true)
            {
                Console.Write($"{color}: (h)uman or (a)i? ");
                string answer = Console.ReadLine()?.Trim().ToLowerInvariant() ?? "";
                if (answer is "h" or "human")
                {
                    humans.Add(color);
                    break;
                }

                if (answer is "a" or "ai")
                    break;

                Console.WriteLine("Type h or a.");
            }
        }

        return GameEngine.Create(count, humans);
    }

    public static void PlayInteractive(GameEngine game)
    {
        bool anyHuman = game.Players.Any(player => player.IsHuman);
        if (!anyHuman)
        {
            Console.Write("All players are AI. Step through each roll? [y/N] ");
            if (!IsYes(Console.ReadLine()))
            {
                Console.WriteLine("Playing...");
                Pump(game);
                ConsolePresenter.Draw(game);
                ConsolePresenter.AnnounceWinner(game);
                WaitToExit();
                return;
            }
        }

        int guard = 0;
        while (!game.IsGameOver)
        {
            if (++guard > 20000)
                throw new InvalidOperationException($"The game did not finish after {game.Rolls} rolls.");

            bool human = game.CurrentPlayer.IsHuman;
            bool pause = human || !anyHuman;
            if (pause)
            {
                ConsolePresenter.Draw(game);
                Console.Write(human
                    ? "Press Enter to roll (q quits): "
                    : "Press Enter for the next roll (q quits): ");
                if (ConsolePresenter.IsQuit(Console.ReadLine()))
                    return;
            }

            var roll = game.RollDice();
            if (!roll.RequiresMove)
                continue;

            Token? token;
            if (human)
            {
                ConsolePresenter.Draw(game);
                if (roll.EligibleTokens.Count == 1)
                {
                    token = roll.EligibleTokens[0];
                    Console.WriteLine($"Only {token.Label} can move ({ConsolePresenter.Preview(token, roll.Value)}).");
                    Console.Write("Press Enter to move (q quits): ");
                    if (ConsolePresenter.IsQuit(Console.ReadLine()))
                        return;
                }
                else
                {
                    token = ConsolePresenter.PromptToken(roll);
                    if (token is null)
                        return;
                }
            }
            else
            {
                token = roll.Player.SelectTokenToMove(roll.Value);
            }

            var move = game.ApplyMove(token);
            if (!move.Success)
                Console.WriteLine(move.Message);
        }

        ConsolePresenter.Draw(game);
        ConsolePresenter.AnnounceWinner(game);
        WaitToExit();
    }

    public static void PlayTranscript(GameEngine game)
    {
        int shown = 0;
        Pump(game, () =>
        {
            for (; shown < game.Log.Count; shown++)
                Console.WriteLine(game.Log[shown]);
        });

        Console.WriteLine();
        ConsolePresenter.AnnounceWinner(game);
    }

    private static void Pump(GameEngine game, Action? afterAction = null)
    {
        int guard = 0;
        while (!game.IsGameOver)
        {
            if (++guard > 20000)
                throw new InvalidOperationException($"The game did not finish after {game.Rolls} rolls.");

            var roll = game.RollDice();
            if (roll.RequiresMove)
            {
                var token = roll.Player.SelectTokenToMove(roll.Value);
                var move = game.ApplyMove(token);
                if (!move.Success)
                    throw new InvalidOperationException(move.Message);
            }

            afterAction?.Invoke();
        }
    }

    private static int AskInt(string prompt, int min, int max)
    {
        while (true)
        {
            Console.Write(prompt);
            if (int.TryParse(Console.ReadLine(), out int value) && value >= min && value <= max)
                return value;

            Console.WriteLine($"Enter a number from {min} to {max}.");
        }
    }

    private static bool IsYes(string? line) =>
        line is not null && line.Trim().ToLowerInvariant() is "y" or "yes";

    private static void WaitToExit()
    {
        if (Console.IsInputRedirected)
            return;

        Console.Write("Press Enter to exit. ");
        Console.ReadLine();
    }
}
