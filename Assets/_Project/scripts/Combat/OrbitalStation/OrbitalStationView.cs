using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalStationView : MonoBehaviour
    {
        public SpriteRenderer Core;
        public Transform RingsRoot, EffectsRoot, InteractionRoot;
        public OrbitalInteractionController Input;
        public OrbitalInteractionPresentation Presentation;
        public OrbitalRewardFlowController Rewards;
        public OrbitalRelocationController Relocation;
        public OrbitalWorldTelekinesisController World;
        public bool IsValid => Core != null && RingsRoot != null && EffectsRoot != null && InteractionRoot != null &&
            Input != null && Presentation != null && Rewards != null && Relocation != null && World != null;
    }
}
