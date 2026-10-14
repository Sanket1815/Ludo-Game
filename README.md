# Ludo

A 2 to 4 player Ludo game. The rules engine is plain C# with no UI dependencies, and the console app is one binding on top of it. A Unity view can call the same `RollDice` / `ApplyMove` methods and listen to the engine events.

## Run

Requires the .NET 8 SDK. If `dotnet` is not on your PATH, the SDK installed for this machine lives in `~/.dotnet`:

```bash
export PATH="$HOME/.dotnet:$PATH"
export DOTNET_ROOT="$HOME/.dotnet"
```

```bash
dotnet test
dotnet run --project src/Ludo.Web --urls http://localhost:5088
dotnet run --project src/Ludo.ConsoleApp
dotnet run --project src/Ludo.ConsoleApp -- --demo --seed 42
dotnet run --project src/Ludo.ConsoleApp -- --players 2 --humans Red
```

Open [http://localhost:5088](http://localhost:5088) for the board. The console app is the same game without the picture.

Two players sit as Red and Yellow. Three players sit as Red, Green, and Yellow.

## Rules implemented

- Each player has four tokens, all starting in the yard.
- A roll of 6 moves a yard token onto that color's start cell. It does not travel six spaces. A 6 also grants another roll.
- Three 6s in a row forfeit the rest of the turn. Moves already taken on that turn stand.
- The main track is 52 cells, traveled clockwise. Progress steps 1 through 51 use `(start + step - 1) % 52`.
- Start cells are Red 0, Green 13, Yellow 26, and Blue 39. Those four cells, plus the cell eight steps ahead of each, are safe.
- Landing on an opponent sends every opposing token on that cell back to its yard and grants another roll. Safe cells block captures. Tokens of the same color stack and do not capture each other. Passing a token does not capture it.
- Steps 52 through 56 are the five home-stretch cells. Step 57 is the home square. Reaching it requires an exact roll.
- The first player to get all four tokens home wins.

## Layout

- `src/Ludo.Core` — board, tokens, players, dice, and `GameEngine`
- `src/Ludo.Web` — browser board
- `src/Ludo.ConsoleApp` — colored console board
- `tests/Ludo.Tests` — movement, capture, turn, and full-game tests
