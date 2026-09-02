namespace Subject42.Combat.OrbitalStation
{
    public enum CombatMode
    {
        Legacy = 0,
        Orbital = 1
    }

    /// <summary>
    /// Session-only combat selection. SelectedMode is the next/debug choice;
    /// ActiveRunMode is latched by BeginNewRun and never serializes station state.
    /// </summary>
    public static class CombatModeState
    {
        public static CombatMode SelectedMode { get; private set; } =
            CombatMode.Legacy;
        public static CombatMode ActiveRunMode { get; private set; } =
            CombatMode.Legacy;

        public static void Select(CombatMode mode) => SelectedMode = mode;

        public static void LatchForNewRun()
        {
            ActiveRunMode = SelectedMode;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public static void DebugApplyToCurrentRun(CombatMode mode)
        {
            SelectedMode = mode;
            ActiveRunMode = mode;
        }
#endif
    }
}
