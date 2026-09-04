using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalMountView : MonoBehaviour
    {
        public SpriteRenderer Marker, Halo;
        public bool IsValid => Marker != null && Halo != null && Marker.sprite != null && Halo.sprite != null;
    }
}
