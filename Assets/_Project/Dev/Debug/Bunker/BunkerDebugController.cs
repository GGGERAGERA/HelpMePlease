#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.EventSystems;
using Object = UnityEngine.Object;

/// <summary>Scene-local developer controls. MVP owns its own independent debug UI.</summary>
[DefaultExecutionOrder(-20000)]
public sealed class BunkerDebugController : MonoBehaviour
{
    public static BunkerDebugController Instance { get; private set; }
    public bool Visible { get; private set; }
    public bool Busy { get; private set; }
    public bool CanMutate => PlayerPrefs.IsSandboxActive && !Busy && SceneManager.GetActiveScene().name == "MainMenu";
    public string Notice { get; private set; } = "F9 · изменения доступны в тестовой копии";
    public BunkerGateVisual[] Gates { get; private set; } = Array.Empty<BunkerGateVisual>();
    public BunkerRoomAccess[] Rooms { get; private set; } = Array.Empty<BunkerRoomAccess>();
    public BunkerStation[] Stations { get; private set; } = Array.Empty<BunkerStation>();
    public MonoBehaviour[] OtherInteractions { get; private set; } = Array.Empty<MonoBehaviour>();
    public BunkerSelectionCatalog Catalog { get; private set; }
    public BunkerAmbientBarks Barks { get; private set; }
    private readonly List<Behaviour> blockedInput = new();
    private Behaviour[] inputSources = Array.Empty<Behaviour>();
    private BunkerDebugView view;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        SceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
    }
    private static void SceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "MainMenu" || Instance != null) return;
        new GameObject("[Dev] Bunker debug · F9").AddComponent<BunkerDebugController>();
    }
    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        view = new BunkerDebugView(this);
        RefreshTargets();
    }
    private void OnEnable()
    {
        Instance = this;
        view ??= new BunkerDebugView(this);
        SceneManager.sceneLoaded -= SceneLoaded;
        SceneManager.sceneLoaded += SceneLoaded;
        RefreshTargets();
    }
    private void Start() => RefreshTargets();
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F9) && !Busy) SetVisible(!Visible);
        if (Visible) BlockInput();
    }
    private void LateUpdate() { if (Visible) BlockInput(); }
    private void OnGUI() { if (Visible) view.Draw(); }
    private void OnDisable()
    {
        Visible = false; ReleaseInput(); view?.Dispose(); view = null;
    }
    private void OnDestroy()
    {
        ReleaseInput(); view?.Dispose();
        if (Instance == this) Instance = null;
    }
    public void RefreshTargets()
    {
        Gates = InScene<BunkerGateVisual>(); Rooms = InScene<BunkerRoomAccess>(); Stations = InScene<BunkerStation>();
        OtherInteractions = InScene<MonoBehaviour>().Where(c => c is IBunkerInteractable && c is not BunkerStation).ToArray();
        inputSources = InScene<Behaviour>().Where(input => input is BunkerCursorInteractor || input is PlayerInteractor ||
            input is CharacterMovement2D || input is BaseInputModule).ToArray();
        Catalog = FindFirstObjectByType<BunkerSelectionSourceHub>()?.Catalog;
        Barks = FindFirstObjectByType<BunkerAmbientBarks>();
    }
    private static T[] InScene<T>() where T : Component => Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None)
        .Where(c => c.gameObject.scene == SceneManager.GetActiveScene()).OrderBy(c => HierarchyPath(c.transform)).ToArray();
    public static string HierarchyPath(Transform target) => target.parent == null ? target.name : HierarchyPath(target.parent) + "/" + target.name;

    public void SetVisible(bool visible)
    {
        if (Busy && visible) return;
        if (Visible == visible) return;
        Visible = visible;
        if (visible)
        {
            RefreshTargets();
            BlockInput();
        }
        else ReleaseInput();
    }
    private void BlockInput()
    {
        foreach (var input in inputSources)
            if (input != null && input.enabled)
            {
                if (!blockedInput.Contains(input)) blockedInput.Add(input);
                input.enabled = false;
            }
    }
    private void ReleaseInput()
    {
        foreach (var input in blockedInput) if (input != null) input.enabled = true;
        blockedInput.Clear();
    }
    public void SelectTab(int tab) => view.SelectTab(tab);
    public void SetNotice(string notice) => Notice = notice;
    private bool Guard()
    {
        if (CanMutate) return true;
        Notice = "Обычное сохранение: только просмотр. Включите тестовую копию.";
        return false;
    }
    public bool SetGold(int amount)
    {
        if (!Guard() || CurrencyManager.Instance == null) return false;
        return CurrencyManager.Instance.TryChangeGoldExact(Mathf.Max(0, amount) - CurrencyManager.Instance.TotalGold, () => { });
    }
    public bool AdjustGold(int delta) => SetGold((int)Math.Clamp((long)(CurrencyManager.Instance?.TotalGold ?? 0) + delta, 0L, int.MaxValue));
    public bool SetCores(int amount) => Guard() && CurrencyManager.Instance != null && CurrencyManager.Instance.DebugSetCores(amount);
    public bool AdjustCores(int delta) => SetCores((int)Math.Clamp((long)(CurrencyManager.Instance?.TotalCores ?? 0) + delta, 0L, int.MaxValue));
    public bool SetMetaLevel(MetaUpgradeType type, int level) => Guard() && MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.DebugSetLevel(type, level);
    public bool SetStationLevel(BunkerStationId id, int level)
    {
        if (!Guard() || BunkerStationProgressionService.Instance == null) return false;
        BunkerStationProgressionService.Instance.DebugSetStationLevel(id, level); return true;
    }
    public bool SetStationInvestment(BunkerStationId id, int amount)
    {
        if (!Guard() || BunkerStationProgressionService.Instance == null) return false;
        BunkerStationProgressionService.Instance.DebugSetStationInvestment(id, amount); return true;
    }
    public bool SetModuleLevel(Subject42.Combat.OrbitalStation.OrbitalModuleKind kind, int level) =>
        Guard() && MetaProgressionManager.Instance != null && MetaProgressionManager.Instance.DebugSetModuleLevel(kind, level);
    public bool SetAnomalyLevel(AnomalyStabilizerData anomaly, int level) => Guard() && AnomalyMetaProgressionManager.EnsureExists().DebugSetLevel(anomaly, level);
    public bool SetUnlock(UnlockableContentData content, bool unlocked)
    {
        if (!Guard() || content == null || UnlockProgressService.Instance == null) return false;
        if (unlocked) UnlockProgressService.Instance.Unlock(content); else UnlockProgressService.Instance.ResetUnlock(content);
        Notice = "Доступ пересчитан. Default/station-зависимые unlock зависят от каталога и уровня станции.";
        return true;
    }
    public bool SetGateOpen(BunkerGateVisual gate, bool open)
    { if (!Guard() || gate == null) return false; gate.DebugSetOpen(open); return true; }
    public bool SetGateLocked(BunkerGateVisual gate, bool locked)
    { if (!Guard() || gate == null) return false; gate.DebugSetLocked(locked); return true; }
    public bool ResetGate(BunkerGateVisual gate)
    { if (!Guard() || gate == null) return false; gate.DebugResetOverride(); return true; }
    public bool SetRoomAccess(BunkerRoomAccess room, bool unlocked)
    { if (!Guard() || room == null) return false; room.SetUnlocked(unlocked); return true; }
    public bool SetStationInteraction(BunkerStation station, bool available)
    { if (!Guard() || station == null) return false; station.SetInteractionEnabled(available); return true; }
    public bool InspectStation(BunkerStation station)
    {
        if (!Guard() || station == null || !station.CanInteract) return false;
        SetVisible(false); station.Interact(); return true;
    }
    public bool InspectInteraction(MonoBehaviour source)
    {
        if (!Guard() || source == null || !source.isActiveAndEnabled || source is not IBunkerInteractable target || !target.CanInteract) return false;
        SetVisible(false); target.Interact(); return true;
    }
    public bool PlayBark(BunkerBarkCategory category, string key)
    {
        if (!Guard() || Barks == null) return false;
        bool played = Barks.DebugTryPlay(category, key); Notice = Barks.DebugLastAttempt; return played;
    }
    public bool StopBark()
    { if (!Guard() || Barks == null) return false; Barks.DebugStop(); return true; }
    public bool AdvanceMission(string id)
    { return Guard() && MetaProgressionManager.Instance?.Missions?.DebugAdvanceTestStage(id) == true; }
    public bool SelectSector(string id) => Guard() && MetaProgressionManager.Instance?.SurfaceMap?.TrySelect(id) == true;
    public bool SelectTask(string id) => Guard() && MetaProgressionManager.Instance?.SurfaceMap?.TrySelectTask(id) == true;
    public bool CompleteMapSector(string id)
    {
        if (!Guard()) return false;
        var map = MetaProgressionManager.Instance?.SurfaceMap;
        var sector = map?.Definition.Find(id);
        if (sector == null || sector.IsEscapeFinal) return false;
        bool completed = map.CompleteVictory(sector.BuildRunConfig(map.Definition.Id), RunEndReason.Victory, true);
        Notice = "Тестовая победа меняет только карту; награды и финал проверяйте обычным забегом.";
        return completed;
    }
    public bool FundEscapeStage()
    {
        if (!Guard()) return false;
        var meta = MetaProgressionManager.Instance;
        if (meta == null || !meta.HasEscapeProtocol || meta.EscapeProtocolFunded) return false;
        var stage = meta.EscapeProtocol.Stages[meta.CurrentEscapeStage];
        return meta.TryInvestEscapeCores(stage.id, stage.cost, out _);
    }
    public bool StartNormalRun()
    {
        if (!Guard()) return false;
        if (!CanSwitch(out string reason)) { Notice = reason; return false; }
        var starter = FindFirstObjectByType<BunkerRunStarter>();
        if (starter == null || RunSelectionManager.Instance?.SelectedCharacter == null) { Notice = "Нет выбранного персонажа/production-пути."; return false; }
        var map = MetaProgressionManager.Instance?.SurfaceMap;
        if (map != null && map.SelectedSectorId == null)
            foreach (var id in map.Definition.StartingSectors) if (map.TrySelect(id)) break;
        if (map == null || !map.TryBuildRunConfig(out _)) { Notice = "Выберите доступный сектор карты."; return false; }
        SetVisible(false);
        starter.StartSurfaceRun(starter.transform);
        return starter.IsTransitioning || SceneTransitionOverlay.IsTransitioning;
    }

    public bool BeginTestCopy() => !PlayerPrefs.IsSandboxActive && QueueSwitch(() => PlayerPrefs.BeginSandbox(false));
    public bool BeginFreshTest() => QueueSwitch(() => { PlayerPrefs.EndSandbox(); PlayerPrefs.BeginSandbox(true); BunkerDebugDisplayState.Restore(); });
    public bool RestoreTestStart() => PlayerPrefs.IsSandboxActive && QueueSwitch(() => { PlayerPrefs.ResetSandbox(); BunkerDebugDisplayState.Restore(); });
    public bool RestoreMainSave() => PlayerPrefs.IsSandboxActive && QueueSwitch(() => { PlayerPrefs.EndSandbox(); BunkerDebugDisplayState.Restore(); BunkerDebugDisplayState.Forget(); });
    private bool CanSwitch(out string reason)
    {
        reason = null;
        if (Busy || SceneManager.GetActiveScene().name != "MainMenu" || SceneTransitionOverlay.IsTransitioning ||
            RunStateManager.Instance is { IsRunEnded: false }) reason = "Дождитесь завершения перехода и возврата в бункер.";
        else if (FindObjectsByType<BunkerMinigame>(FindObjectsSortMode.None).Any(g => g.IsRunning) ||
            BunkerContext.Instance?.Panels?.IsAnyPanelOpen == true || FindFirstObjectByType<BunkerIntroController>() is { IsPlaying: true })
            reason = "Сначала завершите мини-игру/интро и закройте игровые окна.";
        return reason == null;
    }
    private bool QueueSwitch(Action changeStorage)
    {
        if (!CanSwitch(out string reason)) { Notice = reason; return false; }
        if (!PlayerPrefs.IsSandboxActive) BunkerDebugDisplayState.Capture();
        StartCoroutine(SwitchProfile(changeStorage)); return true;
    }
    private IEnumerator SwitchProfile(Action changeStorage)
    {
        SetVisible(false); Busy = true;
        DontDestroyOnLoad(gameObject);
        // Tear down all old save consumers while the old storage is still selected.
        // No scene callback can serialize a cached test value into the main profile.
        Scene oldScene = SceneManager.GetActiveScene();
        Scene staging = SceneManager.CreateScene("[Dev] Save switch");
        SceneManager.SetActiveScene(staging);
        yield return SceneManager.UnloadSceneAsync(oldScene);
        // Search typed owners as well as initialized singletons: script reload may
        // clear static Instance fields while their persistent objects still exist.
        foreach (var owner in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (owner is CurrencyManager || owner is MetaProgressionManager || owner is RunSelectionManager ||
                owner is RunStateManager || owner is AnomalyMetaProgressionManager || owner is AudioService ||
                owner is LocalizationService || owner is UnlockProgressService)
                Destroy(owner.gameObject);
        yield return null;
        changeStorage();
        WeaponControlSettings.DebugReloadFromStorage();
        yield return SceneManager.LoadSceneAsync("MainMenu", LoadSceneMode.Single);
        SceneManager.MoveGameObjectToScene(gameObject, SceneManager.GetActiveScene());
        RefreshTargets(); Busy = false; SetVisible(true);
        Notice = PlayerPrefs.IsSandboxActive ? "Тестовое состояние загружено; основное сохранение изолировано." : "Обычное сохранение восстановлено; debug только для просмотра.";
    }
}
#endif
