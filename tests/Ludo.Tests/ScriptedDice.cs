namespace Ludo.Tests;

internal sealed class ScriptedDice : IDice
{
    private readonly Queue<int> _rolls;

    public ScriptedDice(params int[] rolls) => _rolls = new Queue<int>(rolls);

    public int Roll()
    {
        if (_rolls.Count == 0)
            throw new InvalidOperationException("The scripted dice have no rolls left.");
        return _rolls.Dequeue();
    }
}
