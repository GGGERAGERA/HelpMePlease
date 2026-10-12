#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

namespace Subject42.DebugLabs.EliteOrbit
{
    public enum EliteModuleKind { ArmorPlate, ContactBlade, ProtectionNode }
    public enum EliteFeedback { Block, BodyHit, NodeHit, NodeDestroyed }
    [Serializable]
    public struct EliteSlot
    {
        public int MountIndex;
        public EliteModuleKind Kind;
    }
    public sealed class EliteOrbitPreset : ScriptableObject
    {
        public string DisplayName;
        public string Hint;
        public float BodyHealth=160, Radius=2, RotationSpeed=50, MovementMultiplier=.35f;
        public int MountCount=6;
        public EliteSlot[] Slots;
        public float ShieldArc=85, ShieldThickness=.25f;
        public float BladeDamage=12, BladeRepeatSeconds=.65f;
        public float NodeHealth=22, ReductionPerNode=.18f;
    }
}
#endif
