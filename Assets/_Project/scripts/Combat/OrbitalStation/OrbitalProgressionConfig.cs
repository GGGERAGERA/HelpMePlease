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

        public float BaseRingOfferChance = 0.05f;
        public float RingOfferChanceStep = 0.02f;
        public float MaxRingOfferChance = 0.45f;
        public int MaxNormalRings = 8;
        public int MaxMountsPerRing = 6;
        public int MaxSpeedUpgradeLevel = 4;
        public int MaxPowerUpgradeLevel = 4;
        public int MaxCoreLevel = 3;
        public float CoreIntervalI = 7.5f;
        public float CoreIntervalII = 6.5f;
        public float CoreIntervalIII = 5.5f;
        public float CoreChargeDuration = .35f;
        public float CoreRingDelay = .12f;
        public float CoreWaveSpacing = .35f;
        public float GetCoreInterval(int level) => level switch
        { 1 => CoreIntervalI, 2 => CoreIntervalII, _ => CoreIntervalIII };
        public int MaxLinkMatrixLevel = 3;
        public float SpeedIncrement = 0.25f;
        public float PowerIncrement = 0.25f;

        public float ModuleWeight = 1f;
        public float LinkPairWeight = 0.65f;
        public float RingWeight = 0.9f;
        public float CoreWeight = 0.10f;
        public int MinRingsForCoreOffer = 2;
        public float SubjectWeight = 0.8f;

        public float GetRingOfferChance(int missedOpportunities) =>
            UnityEngine.Mathf.Clamp(BaseRingOfferChance +
                missedOpportunities * RingOfferChanceStep,
                BaseRingOfferChance, MaxRingOfferChance);
    }
}
