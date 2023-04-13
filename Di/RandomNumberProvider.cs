namespace Di;

public class RandomNumberProvider : INumberProvider
{
    public int GetNumber() => Random.Shared.Next();
}