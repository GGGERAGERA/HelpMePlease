using System;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Scene-authored wiring only: no lookup API, registry, asset paths or runtime discovery.
// Each service reference may be a local scene component or a reusable authored prefab.
[DefaultExecutionOrder(-32700)]
[DisallowMultipleComponent]
public sealed class ProductionSceneComposition : MonoBehaviour
{
    [SerializeField] private LocalizationService localization;
    [SerializeField] private AudioService audio;
    [SerializeField] private UnlockProgressService unlocks;
    [SerializeField] private BunkerStationProgressionService bunkerProgression;
    [SerializeField] private SceneTransitionOverlay transition;
    [SerializeField] private OrbitalPresentationConfig orbital;
    [SerializeField] private VisualTuningPreset visualPreset;
    [SerializeField] private AnomalyItemData[] anomalyItems;
    [SerializeField] private EvolutionRecipe[] evolutionRecipes;

    private void Awake()
    {
        if (localization == null || audio == null || unlocks == null || bunkerProgression == null ||
            transition == null || orbital == null || visualPreset == null)
            throw new InvalidOperationException("ProductionSceneComposition has missing Inspector dependencies.");
        OrbitalPresentationConfig.Configure(orbital);
        VisualTuningPresetStorage.Configure(visualPreset);
        AnomalyItemCatalog.Configure(anomalyItems);
        EvolutionRecipeCatalog.Configure(evolutionRecipes);

        if (LocalizationService.Instance == null)
        {
            var owner = localization.gameObject.scene.IsValid() ? localization : Instantiate(localization);
            owner.InitializeAuthored();
        }
        if (AudioService.Instance == null)
        {
            var owner = audio.gameObject.scene.IsValid() ? audio : Instantiate(audio);
            owner.InitializeAuthored();
        }
        if (UnlockProgressService.Instance == null)
        {
            var owner = unlocks.gameObject.scene.IsValid() ? unlocks : Instantiate(unlocks);
            owner.InitializeAuthored();
        }
        if (BunkerStationProgressionService.Instance == null)
        {
            var owner = bunkerProgression.gameObject.scene.IsValid() ? bunkerProgression : Instantiate(bunkerProgression);
            owner.InitializeAuthored();
        }
        if (SceneTransitionOverlay.Instance == null)
        {
            var owner = transition.gameObject.scene.IsValid() ? transition : Instantiate(transition);
            owner.InitializeAuthored();
        }
    }
}
