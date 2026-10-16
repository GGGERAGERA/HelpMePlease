using UnityEngine;
using UnityEngine.Rendering;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalMountView : MonoBehaviour
    {
        public SpriteRenderer Marker, Halo;
        [Tooltip("Nested in the player's SortingGroup; includes mounted weapon sprites and particles.")]
        public SortingGroup DepthGroup;
        [SerializeField] private int backSortingOrder = -5;
        [SerializeField] private int frontSortingOrder = 8;
        public bool IsValid => Marker != null && Halo != null && Marker.sprite != null &&
            Halo.sprite != null && DepthGroup != null;

        public void UpdateDepth(float localY, bool depthAware = true)
        {
            // Keep artist groups inside one explicit mount order even on continuous rings.
            DepthGroup.enabled = true;
            if (!depthAware) { DepthGroup.sortingOrder = frontSortingOrder; return; }
            DepthGroup.sortingOrder = localY > 0f ? backSortingOrder : frontSortingOrder;
        }

        public void UpdateFigureEightDepth(bool behind)
        {
            DepthGroup.enabled = true;
            DepthGroup.sortingOrder = behind ? backSortingOrder : frontSortingOrder;
        }
    }
}
