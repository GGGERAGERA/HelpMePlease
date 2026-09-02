using System;

namespace Subject42.Combat.OrbitalStation
{
    /// <summary>
    /// Small production balance surface for the first orbital reward slice.
    /// It deliberately stays code-owned until the reward set is proven in play.
    /// </summary>
    [Serializable]
    public sealed class OrbitalProgressionConfig
    {
        public static OrbitalProgressionConfig Default { get; } = new();

        public int[] RingMilestoneLevels = { 2, 3, 4, 6, 8, 10, 13 };
        public int MaxNormalRings = 8;
        public int MaxMountsPerRing = 6;
        public int MaxSpeedUpgradeLevel = 4;
        public int MaxPowerUpgradeLevel = 4;
        public int MaxCoreLevel = 3;
        public int MaxLinkMatrixLevel = 3;
        public float SpeedIncrement = 0.25f;
        public float PowerIncrement = 0.25f;

        public float ModuleWeight = 1f;
        public float LinkPairWeight = 0.65f;
        public float RingWeight = 0.9f;
        public float CoreWeight = 0.7f;
        public float SubjectWeight = 0.8f;

        public int GetNextRingMilestone(int playerLevel)
        {
            for (int i = 0; i < RingMilestoneLevels.Length; i++)
                if (RingMilestoneLevels[i] > playerLevel)
                    return RingMilestoneLevels[i];
            return -1;
        }

        public bool IsRingMilestone(int playerLevel)
        {
            for (int i = 0; i < RingMilestoneLevels.Length; i++)
                if (RingMilestoneLevels[i] == playerLevel)
                    return true;
            return false;
        }
    }
}
