#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

public enum BotSeedMode { Auto, Fixed }

// Only these audited gameplay draws opt into a seeded stream. VFX retain Unity Random.
// Streams never read or mutate Unity's global state while a Bot Lab run owns them.
public static class BotRunSeed
{
    public const int Version = 1;
    public static bool IsActive { get; private set; }
    public static int Seed { get; private set; }
    private static int nextAuto = Guid.NewGuid().GetHashCode();
    // Unique within this editor/player session until the full integer range wraps.
    public static int NextAuto() => nextAuto = unchecked(nextAuto + 1);
    private static readonly Stream[] streams = { new(), new(), new(), new(), new(), new() };

    public static void Begin(int seed)
    {
        if (IsActive) throw new InvalidOperationException("A seeded run already owns gameplay RNG.");
        Seed = seed;
        for (int i = 0; i < streams.Length; i++) streams[i].Seed(unchecked(seed * 397 ^ ((i + 1) * 7919)));
        IsActive = true;
    }
    public static void End()
    {
        foreach (var stream in streams) stream.Clear();
        IsActive = false;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset() => End();

    private sealed class Stream
    {
        private System.Random rng;
        public void Seed(int seed) => rng = new System.Random(seed);
        public void Clear() => rng = null;
        public float Value => rng != null ? (float)rng.NextDouble() : UnityEngine.Random.value;
        public int Range(int min, int max) => rng != null ? (min == max ? min : rng.Next(min, max)) : UnityEngine.Random.Range(min, max);
        public float Range(float min, float max) => rng != null ? min + (max - min) * Value : UnityEngine.Random.Range(min, max);
        public Vector2 Circle
        {
            get
            {
                if (rng == null) return UnityEngine.Random.insideUnitCircle;
                float angle = Value * Mathf.PI * 2f, radius = Mathf.Sqrt(Value);
                return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
        }
    }

    public static class WorldRandom
    {
        public static float value => streams[0].Value;
        public static int Range(int min, int max) => streams[0].Range(min, max);
        public static float Range(float min, float max) => streams[0].Range(min, max);
        public static Vector2 insideUnitCircle => streams[0].Circle;
    }

    public static class SpawnRandom
    {
        public static float value => streams[1].Value;
        public static int Range(int min, int max) => streams[1].Range(min, max);
        public static float Range(float min, float max) => streams[1].Range(min, max);
        public static Vector2 insideUnitCircle => streams[1].Circle;
    }

    public static class RewardRandom
    {
        public static float value => streams[2].Value;
        public static int Range(int min, int max) => streams[2].Range(min, max);
        public static float Range(float min, float max) => streams[2].Range(min, max);
        public static Vector2 insideUnitCircle => streams[2].Circle;
    }

    public static class DropRandom
    {
        public static float value => streams[3].Value;
        public static int Range(int min, int max) => streams[3].Range(min, max);
        public static float Range(float min, float max) => streams[3].Range(min, max);
        public static Vector2 insideUnitCircle => streams[3].Circle;
    }

    public static class EventRandom
    {
        public static float value => streams[4].Value;
        public static int Range(int min, int max) => streams[4].Range(min, max);
        public static float Range(float min, float max) => streams[4].Range(min, max);
        public static Vector2 insideUnitCircle => streams[4].Circle;
    }

    public static class RuleRandom
    {
        public static float value => streams[5].Value;
        public static int Range(int min, int max) => streams[5].Range(min, max);
        public static float Range(float min, float max) => streams[5].Range(min, max);
        public static Vector2 insideUnitCircle => streams[5].Circle;
    }
}
#endif
