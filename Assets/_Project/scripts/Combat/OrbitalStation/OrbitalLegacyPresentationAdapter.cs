using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalLegacyPresentationAdapter : MonoBehaviour
    {
        private readonly Dictionary<GameObject, bool> previousStates = new();

        public static OrbitalLegacyPresentationAdapter Ensure(GameObject player)
        {
            OrbitalLegacyPresentationAdapter adapter =
                player.GetComponent<OrbitalLegacyPresentationAdapter>();
            return adapter != null ? adapter :
                player.AddComponent<OrbitalLegacyPresentationAdapter>();
        }

        public void EnterOrbital()
        {
            BaseWeapon[] weapons = GetComponentsInChildren<BaseWeapon>(true);
            for (int i = 0; i < weapons.Length; i++)
            {
                GameObject visualRoot = weapons[i].gameObject;
                if (!previousStates.ContainsKey(visualRoot))
                    previousStates.Add(visualRoot, visualRoot.activeSelf);
                visualRoot.SetActive(false);
            }
            PlayerWeaponOrbitVisual[] orbitVisuals =
                GetComponentsInChildren<PlayerWeaponOrbitVisual>(true);
            for (int i = 0; i < orbitVisuals.Length; i++)
                orbitVisuals[i].enabled = false;
        }

        public void EnterLegacy()
        {
            foreach (KeyValuePair<GameObject, bool> pair in previousStates)
                if (pair.Key != null)
                    pair.Key.SetActive(pair.Value);
            previousStates.Clear();
        }
    }
}
