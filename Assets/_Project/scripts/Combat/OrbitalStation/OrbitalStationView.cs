using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalStationView : MonoBehaviour
    {
        public Transform RingsRoot, EffectsRoot, InteractionRoot;
        public OrbitalInteractionController Input;
        public OrbitalInteractionPresentation Presentation;
        public OrbitalRewardFlowController Rewards;
        public OrbitalRelocationController Relocation;
        public OrbitalCenterShift CenterShift;
        public bool IsValid => RingsRoot != null && EffectsRoot != null && InteractionRoot != null &&
            Input != null && Presentation != null && Presentation.HasScreenEffect && Rewards != null && Relocation != null && CenterShift != null;
    }
}
