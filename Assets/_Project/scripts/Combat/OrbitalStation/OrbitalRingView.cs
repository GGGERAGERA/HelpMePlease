using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalRingView : MonoBehaviour
    {
        public LineRenderer Line;
        public Transform MountsRoot;
        public bool IsValid => Line != null && MountsRoot != null;
    }
}
