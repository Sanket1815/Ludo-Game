using System.Text.Json.Serialization;
using Ludo;

var builder = WebApplication.CreateBuilder(args);
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddSingleton<GameRoom>();

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

var rooms = app.Services.GetRequiredService<GameRoom>();

app.MapPost("/api/games", (NewGameRequest request) =>
{
    try
    {
        var humans = ParseHumans(request.Humans);
        var game = GameEngine.Create(request.PlayerCount, humans, request.Seed);
        var id = rooms.Add(game);
        return Results.Ok(GameView.From(id, game));
    }
    catch (ArgumentException ex)
    {
        return Results.BadRequest(new { error = ex.Message });
    }
});

app.MapGet("/api/games/{id}", (string id) =>
    rooms.Try(id, game => Results.Ok(GameView.From(id, game))));

app.MapPost("/api/games/{id}/roll", (string id) =>
    rooms.Try(id, game =>
    {
        var roll = game.RollDice();
        return Results.Ok(GameView.From(id, game, roll.Value, roll.TurnVoided, roll.NoLegalMove));
    }));

app.MapPost("/api/games/{id}/move", (string id, MoveRequest request) =>
    rooms.Try(id, game =>
    {
        if (!Enum.TryParse<PlayerColor>(request.Color, true, out var color) || !Enum.IsDefined(color))
            return Results.BadRequest(new { error = "Unknown color." });

        var token = game.CurrentPlayer.Tokens.FirstOrDefault(candidate => candidate.Id == request.Id && candidate.Color == color);
        if (token is null)
            return Results.BadRequest(new { error = "That token cannot move." });

        var move = game.ApplyMove(token);
        if (!move.Success)
            return Results.BadRequest(new { error = move.Message, state = GameView.From(id, game) });

        return Results.Ok(GameView.From(id, game, game.PendingRoll));
    }));

app.MapPost("/api/games/{id}/ai", (string id) =>
    rooms.Try(id, game =>
    {
        if (game.CurrentPlayer.IsHuman)
            return Results.BadRequest(new { error = "It is a human player's turn." });

        var roll = game.RollDice();
        if (roll.RequiresMove)
        {
            var token = roll.Player.SelectTokenToMove(roll.Value);
            var move = game.ApplyMove(token);
            if (!move.Success)
                return Results.BadRequest(new { error = move.Message });
        }

        return Results.Ok(GameView.From(id, game, roll.Value, roll.TurnVoided, roll.NoLegalMove));
    }));

app.Run();

static HashSet<PlayerColor>? ParseHumans(string[]? humans)
{
    if (humans is null)
        return null;

    var colors = new HashSet<PlayerColor>();
    foreach (var name in humans)
    {
        if (!Enum.TryParse<PlayerColor>(name, true, out var color) || !Enum.IsDefined(color))
            throw new ArgumentException($"Unknown color '{name}'. Use Red, Green, Yellow, or Blue.");
        colors.Add(color);
    }

    return colors;
}

internal sealed record NewGameRequest(int PlayerCount, string[]? Humans, int? Seed);

internal sealed record MoveRequest(string Color, int Id);

internal sealed class GameRoom
{
    private readonly Dictionary<string, GameEngine> _games = [];
    private readonly Dictionary<string, object> _gates = [];

    public string Add(GameEngine game)
    {
        var id = Guid.NewGuid().ToString("N")[..8];
        lock (_games)
        {
            _games[id] = game;
            _gates[id] = new object();
        }

        return id;
    }

    public IResult Try(string id, Func<GameEngine, IResult> action)
    {
        GameEngine? game;
        object? gate;
        lock (_games)
        {
            if (!_games.TryGetValue(id, out game) || !_gates.TryGetValue(id, out gate))
                return Results.NotFound(new { error = "No game with that id. Start a new one." });
        }

        try
        {
            lock (gate)
                return action(game);
        }
        catch (InvalidOperationException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
    }
}

internal sealed record GameView(
    string Id,
    string? Winner,
    string CurrentPlayer,
    bool CurrentIsHuman,
    int ConsecutiveSixes,
    int? PendingRoll,
    bool AwaitingMove,
    int Rolls,
    int Moves,
    int? Roll,
    bool TurnVoided,
    bool NoLegalMove,
    IReadOnlyList<string> Log,
    IReadOnlyList<TokenRef> Eligible,
    IReadOnlyList<PlayerView> Players)
{
    public static GameView From(
        string id,
        GameEngine game,
        int? roll = null,
        bool turnVoided = false,
        bool noLegalMove = false)
    {
        var eligible = game.AwaitingMove && game.PendingRoll is int die
            ? game.CurrentPlayer.GetEligibleTokens(die)
                .Select(token => new TokenRef(token.Color.ToString(), token.Id))
                .ToArray()
            : [];

        return new GameView(
            id,
            game.Winner?.Color.ToString(),
            game.CurrentPlayer.Color.ToString(),
            game.CurrentPlayer.IsHuman,
            game.ConsecutiveSixes,
            game.PendingRoll,
            game.AwaitingMove,
            game.Rolls,
            game.Moves,
            roll ?? game.PendingRoll,
            turnVoided,
            noLegalMove,
            game.Log.TakeLast(8).ToArray(),
            eligible,
            game.Players.Select(player => new PlayerView(
                player.Color.ToString(),
                player.IsHuman,
                player.IsFinished,
                player.Tokens.Select(token => new TokenView(
                    token.Id,
                    token.Color.ToString(),
                    token.State.ToString(),
                    token.StepProgress,
                    token.CurrentBoardIndex)).ToArray())).ToArray());
    }
}

internal sealed record PlayerView(
    string Color,
    bool IsHuman,
    bool IsFinished,
    IReadOnlyList<TokenView> Tokens);

internal sealed record TokenView(
    int Id,
    string Color,
    string State,
    int Step,
    int Index);

internal sealed record TokenRef(string Color, int Id);
