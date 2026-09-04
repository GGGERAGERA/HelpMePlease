using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalModuleView : MonoBehaviour
    {
        public Transform Body;
        public SpriteRenderer Halo;
        public Transform PulseBody;
        public SpriteRenderer[] Sprites;
        public Animator Animator;
        public ParticleSystem[] Particles;
        public Sprite Icon;
        public Color IconTint = Color.white;
        public bool IsValid => Body != null && Halo != null && Sprites != null && Sprites.Length > 0;
    }
}
