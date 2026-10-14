namespace Ludo;

public interface IDice
{
    int Roll();
}

public sealed class RandomDice : IDice
{
    private readonly Random _random;

    public RandomDice(int? seed = null)
    {
        _random = seed.HasValue ? new Random(seed.Value) : new Random();
    }

    public int Roll() => _random.Next(1, 7);
}
