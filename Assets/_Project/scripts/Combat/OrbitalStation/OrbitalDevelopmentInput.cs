namespace Subject42.Combat.OrbitalStation
{
    // P1-07: only the player-local interaction owner consumes this release-safe adapter.
    // Remove when debug UI registration can bind directly to that scene-local owner.
    internal static class OrbitalDevelopmentInput
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        internal static System.Func<bool> DevelopmentBlocker { private get; set; }

        public static bool IsGameplayInputBlocked => DevelopmentBlocker?.Invoke() ?? false;
#else
        public static bool IsGameplayInputBlocked => false;
#endif
    }
}
