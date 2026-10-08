using System;
using System.Collections;
using Subject42.Combat.OrbitalStation;
using UnityEngine;

// Scene-authored wiring only: no lookup API, registry, asset paths or runtime discovery.
// Each service reference may be a local scene component or a reusable authored prefab.
[DefaultExecutionOrder(-32700)]
[DisallowMultipleComponent]
public sealed class ProductionSceneComposition : MonoBehaviour
{
    public enum SceneRole { ServicesOnly, StartScreen, Bunker, Gameplay }
    public static ProductionSceneComposition Active { get; private set; }
    public bool IsReady { get; private set; }
    public void PrepareForTransition() => characters?.PrepareForSceneTransition();
    [SerializeField] private SceneRole role;
    [SerializeField] private SurfaceMapDefinition surfaceMap;
    [SerializeField] private MissionCatalog missions;
    [SerializeField] private LocalizationService localization;
    [SerializeField] private AudioService audio;
    [SerializeField] private UnlockProgressService unlocks;
    [SerializeField] private BunkerStationProgressionService bunkerProgression;
    [SerializeField] private SceneTransitionOverlay transition;
    [SerializeField] private OrbitalPresentationConfig orbital;
    [SerializeField] private VisualTuningPreset visualPreset;
    [SerializeField] private AnomalyItemData[] anomalyItems;
    [SerializeField] private EvolutionRecipe[] evolutionRecipes;
    [SerializeField] private CurrencyManager currency;
    [SerializeField] private RunSelectionManager selection;
    [SerializeField] private MetaProgressionManager metaProgression;

    [Header("Scene-owned subsystem bindings")]
    [SerializeField] private CharacterSpawner characters;
    [SerializeField] private EnemySpawner enemies;
    [SerializeField] private GameplayAreaService gameplayArea;
    [SerializeField] private LevelModifiersApplier levelModifiers;
    [SerializeField] private HUDManager hud;
    [SerializeField] private UpgradeManager rewards;
    [SerializeField] private CameraFollow cameraFollow;
    [SerializeField] private BunkerPlayerLoadoutController bunkerLoadout;
    [SerializeField] private BunkerSelectionSourceHub bunkerSelections;

    private void Awake()
    {
        if (localization == null || audio == null || unlocks == null || bunkerProgression == null ||
            transition == null || orbital == null || visualPreset == null || currency == null ||
            selection == null || metaProgression == null)
            throw new InvalidOperationException("ProductionSceneComposition has missing Inspector dependencies.");
        Active = this;
        RunStateManager.EnsureExists();
        if (CurrencyManager.Instance == null) Instantiate(currency).InitializeAuthored();
        if (RunSelectionManager.Instance == null) Instantiate(selection).InitializeAuthored();
        if (MetaProgressionManager.Instance == null) Instantiate(metaProgression).InitializeAuthored();
        if (surfaceMap != null)
        {
            var meta = MetaProgressionManager.Instance;
            meta.ConfigureSurfaceMap(surfaceMap); meta.ConfigureMissions(missions);
        }
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
        if (characters != null) characters.BindScene(hud, rewards);
        if (role == SceneRole.Gameplay)
        {
            if (characters == null || enemies == null || levelModifiers == null || hud == null ||
                cameraFollow == null || gameplayArea == null || rewards == null || hud.OrbitalCursor == null)
                throw new InvalidOperationException("Gameplay composition has missing scene bindings.");
            levelModifiers.PrepareDirectRun(characters.DefaultCharacter);
            enemies.BindScene(characters, gameplayArea);
            levelModifiers.BindScene(characters, enemies);
            characters.CharacterSpawned += BindGameplayPlayer;
        }
        else if (role == SceneRole.Bunker)
        {
            if (bunkerLoadout == null || bunkerSelections == null || cameraFollow == null)
                throw new InvalidOperationException("Bunker composition has missing scene bindings.");
            bunkerLoadout.BindScene(RunSelectionManager.Instance, bunkerSelections);
        }
    }

    private void BindGameplayPlayer(GameObject player)
    {
        cameraFollow.target = player.transform;
        characters.Station?.Interaction?.BindCursor(hud.OrbitalCursor, cameraFollow.ControlledCamera);
    }

    private IEnumerator Start()
    {
        while (!SubsystemsReady()) yield return null;
        // Scene activation and Start callbacks finish before the transition reveals it.
        yield return null;
        IsReady = true;
    }

    private bool SubsystemsReady() => role switch
    {
        SceneRole.Gameplay => characters.SpawnedPlayer != null && levelModifiers.IsInitialized &&
            characters.Station is { IsInitialized: true } &&
            hud.IsPlayerBound && cameraFollow.target != null && cameraFollow.ControlledCamera != null,
        SceneRole.Bunker => bunkerLoadout.IsReady && cameraFollow.target != null && cameraFollow.ControlledCamera != null,
        _ => true
    };

    private void OnDestroy()
    {
        if (characters != null) characters.CharacterSpawned -= BindGameplayPlayer;
        if (Active == this) Active = null;
    }
}
