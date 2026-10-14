using Ludo;
using Ludo.ConsoleApp;

if (Has(args, "--help") || Has(args, "-h"))
{
    PrintHelp();
    return 0;
}

try
{
    bool demo = Has(args, "--demo");
    int? seed = ReadInt(args, "--seed");
    int? playerOption = ReadInt(args, "--players");
    string? humansOption = ReadString(args, "--humans");

    if (demo)
    {
        int count = playerOption ?? 4;
        GameSession.PlayTranscript(GameEngine.Create(count, humanColors: null, seed: seed));
        return 0;
    }

    GameEngine game;
    if (playerOption is null && humansOption is null)
    {
        if (Console.IsInputRedirected)
        {
            Console.Error.WriteLine("No interactive terminal. Re-run with --demo or --players.");
            return 1;
        }

        game = GameSession.ConfigureInteractively();
    }
    else
    {
        int count = playerOption ?? 2;
        var colors = TrackMath.SeatColors(count);
        var humans = humansOption is null
            ? colors.ToHashSet()
            : ParseHumans(humansOption);
        game = GameEngine.Create(count, humans, seed);
    }

    GameSession.PlayInteractive(game);
    return 0;
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

static bool Has(string[] args, string name) =>
    args.Any(arg => string.Equals(arg, name, StringComparison.OrdinalIgnoreCase));

static int? ReadInt(string[] args, string name)
{
    string? text = ReadString(args, name);
    if (text is null)
        return null;
    if (!int.TryParse(text, out int value))
        throw new ArgumentException($"{name} expects a number.");
    return value;
}

static string? ReadString(string[] args, string name)
{
    for (int i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
            continue;
        if (i + 1 >= args.Length || args[i + 1].StartsWith('-'))
            throw new ArgumentException($"{name} expects a value.");
        return args[i + 1];
    }

    return null;
}

static HashSet<PlayerColor> ParseHumans(string text)
{
    if (text.Equals("none", StringComparison.OrdinalIgnoreCase))
        return [];

    var humans = new HashSet<PlayerColor>();
    foreach (string part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
    {
        if (!Enum.TryParse(part, ignoreCase: true, out PlayerColor color) || !Enum.IsDefined(color))
            throw new ArgumentException($"Unknown color '{part}'. Use Red, Green, Yellow, or Blue.");
        humans.Add(color);
    }

    return humans;
}

static void PrintHelp()
{
    Console.WriteLine(
        """
        Ludo

          dotnet run --project src/Ludo.ConsoleApp
          dotnet run --project src/Ludo.ConsoleApp -- --demo
          dotnet run --project src/Ludo.ConsoleApp -- --demo --players 2 --seed 42
          dotnet run --project src/Ludo.ConsoleApp -- --players 2 --humans Red

        Options
          --players N     2, 3, or 4. Two players sit as Red and Yellow.
          --humans LIST   Comma-separated colors, or "none". Default: every seated player.
          --seed N        Repeatable dice.
          --demo          All AI. Print every roll and stop when someone wins.
          --help          Show this help.
        """);
}
