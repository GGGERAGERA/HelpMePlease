#if UNITY_EDITOR || DEVELOPMENT_BUILD
public enum BotSeedMode { Auto, Fixed }

// Dev adapter supplies ordinary seed input to the runtime contract.
public static class BotRunSeed
{
    public const int Version = GameplayRandom.Version;
    public static bool IsActive => GameplayRandom.IsActive;
    public static int Seed => GameplayRandom.Seed;
    public static int NextAuto() => GameplayRandom.NextAuto();
    public static void Begin(int seed) => GameplayRandom.Begin(seed);
    public static void End() => GameplayRandom.End();
}
#endif