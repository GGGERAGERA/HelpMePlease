#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Scene-local orchestration only. All build mutations go through production APIs.
[DefaultExecutionOrder(-300)]
public abstract class OrbitalLabSession : MonoBehaviour
{
    public CharacterData Character;
    public CameraFollow CameraRig;
    public UpgradeManager Rewards;
    public OrbitalStationRuntime Station { get; private set; }
    public PlayerHealth Player { get; private set; }
    public bool Ready => !resetting && Station != null && Station.IsInitialized;
    protected OrbitalRewardProvider Provider;
    protected string Notice = "Ready";
    protected int TargetRing;
    protected bool PanelVisible = true;
    private bool resetting;
    private Vector2 scroll;
    private readonly HashSet<GameObject> authoredRoots = new();
    private RunStateManager manager;
    private bool ownsManager;
    private GUIStyle label, button;
    private RectTransform panelBlocker;
    protected Rect PanelRect => new(8, 8, Mathf.Min(320, Screen.width * .32f), Mathf.Min(780, Screen.height - 16));

    protected virtual void Start()
    {
        foreach (var root in gameObject.scene.GetRootGameObjects()) authoredRoots.Add(root);
        ownsManager = RunStateManager.Instance == null;
        manager = RunStateManager.EnsureExists();
        if (ownsManager) UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(manager.gameObject, gameObject.scene);
        authoredRoots.Add(manager.gameObject);
        CreatePanelBlocker();
        Provider = new OrbitalRewardProvider(Rewards != null ? Rewards.AllUpgrades.ToArray() : Array.Empty<UpgradeData>());
        ResetBuild();
    }

    public void ResetBuild(Action afterReset = null)
    {
        if (resetting) return;
        StartCoroutine(RecreateBuild(afterReset));
    }

    private IEnumerator RecreateBuild(Action afterReset)
    {
        resetting = true;
        // A GUI button can request reset mid-layout; retire the build on the next frame.
        yield return null;
        BeforeBuildReset();
        if (Rewards != null) { Rewards.CancelPendingRewards(); Rewards.enabled = false; }
        if (Station != null) Station.Teardown();
        if (Player != null) { Player.gameObject.SetActive(false); Destroy(Player.gameObject); }
        Station = null; Player = null;
        ClearLooseObjects();
        Time.timeScale = 1f;
        // Let Unity retire old views, coroutines and subscriptions before creating replacements.
        yield return null;
        manager.DebugResetOrbitalRunState();
        manager.ClearUpgradesForDebug(null);
        var prefab = OrbitalPresentationConfig.Active.GetPlayerPrefab(Character.characterPrefab);
        var player = Instantiate(prefab, transform);
        player.name = "Player (production)";
        player.transform.position = Vector3.zero;
        player.SetActive(true);
        // The production player prefab still carries a legacy spawner. Labs never run it.
        foreach (var spawner in player.GetComponentsInChildren<EnemySpawner>(true))
        { spawner.enabled = false; Destroy(spawner); }
        PlayerLoadoutFactory.ApplyCharacterStats(player, Character);
        Player = player.GetComponent<PlayerHealth>();
        Station = OrbitalStationRuntime.Ensure(player, Character);
        CameraRig.target = player.transform;
        CameraRig.transform.position = player.transform.position + CameraRig.offset;
        TargetRing = 0;
        if (Rewards != null) { Rewards.enabled = true; Rewards.BindOrbitalStation(Station); }
        yield return null;
        resetting = false;
        if (!Ready || !Station.ValidateState(out _))
        { Notice = "INIT FAILED — see Console"; Debug.LogError("[OrbitalLab] invalid initial build", this); yield break; }
        Notice = "Baseline: Core 0 / 1 ring / Pistol";
        afterReset?.Invoke();
    }

    protected virtual void BeforeBuildReset() { }

    protected void ClearLooseObjects()
    {
        // Projectiles and hit/death FX instantiate at scene root. Never touch another scene.
        foreach (var root in gameObject.scene.GetRootGameObjects())
            if (!authoredRoots.Contains(root)) { root.SetActive(false); Destroy(root); }
    }

    protected virtual void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1)) PanelVisible = !PanelVisible;
        if (panelBlocker != null)
        {
            panelBlocker.gameObject.SetActive(PanelVisible);
            panelBlocker.anchoredPosition = new Vector2(PanelRect.x, -PanelRect.y);
            panelBlocker.sizeDelta = PanelRect.size;
        }
        if (Station?.InputOwner != null)
            Station.InputOwner.DebugSuppressPlayerInput = PanelVisible &&
                PanelRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)) &&
                !Input.GetKeyDown(KeyCode.Escape);
    }

    private void CreatePanelBlocker()
    {
        if (EventSystem.current == null)
            new GameObject("Lab EventSystem", typeof(EventSystem), typeof(StandaloneInputModule)).transform.SetParent(transform);
        var canvasObject = new GameObject("Lab panel input", typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        canvasObject.transform.SetParent(transform, false);
        var canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 30000;
        var blocker = new GameObject("Panel pointer/scroll blocker", typeof(RectTransform), typeof(Image), typeof(ScrollRect));
        blocker.transform.SetParent(canvasObject.transform, false);
        blocker.GetComponent<Image>().color = Color.clear;
        panelBlocker = blocker.GetComponent<RectTransform>();
        panelBlocker.anchorMin = panelBlocker.anchorMax = panelBlocker.pivot = new Vector2(0, 1);
        // A raycastable ScrollRect lets production reward and camera input recognise the IMGUI panel.
        // It has no content: actual scrolling belongs to the IMGUI scroll view.
    }

    public void ClearModules()
    {
        if (!CanEdit()) return;
        foreach (var module in Station.State.Modules.ToArray()) Station.RemoveModule(module.StableModuleId);
        Notice = "Modules cleared";
    }

    protected bool CanEdit()
    {
        if (!Ready) return false;
        if (Rewards != null && !Rewards.IsRewardQueueIdle || !Station.InputOwner.IsIdle)
        { Notice = "Finish reward or RESET BUILD first"; return false; }
        return true;
    }

    public bool Give(OrbitalRewardKind kind)
    {
        if (!CanEdit()) return false;
        TargetRing = Mathf.Clamp(TargetRing, 0, Station.State.Rings.Count - 1);
        var ring = Station.State.Rings[TargetRing];
        var free = Station.Rings.SelectMany(r => r.Mounts).Where(Station.IsMountFree)
            .OrderBy(m => m.Ring.RingId == ring.StableRingId ? 0 : 1).ToArray();
        bool applied;
        string error = "No eligible target / production cap reached";
        if (Enum.TryParse(kind.ToString(), out OrbitalModuleKind moduleKind) && kind != OrbitalRewardKind.LinkPair)
            applied = free.Length > 0 && Station.InstallModule(moduleKind, free[0].Ring.RingId, free[0].MountIndex, out error);
        else if (kind == OrbitalRewardKind.LinkPair)
            applied = free.Length >= 2 && Station.InstallLinkPair(free[0].Ring.RingId, free[0].MountIndex,
                free[1].Ring.RingId, free[1].MountIndex, out error);
        else applied = kind switch
        {
            OrbitalRewardKind.NewRing => Station.AddRing() != null,
            OrbitalRewardKind.AddMount => Station.AddMount(ring.StableRingId, out error),
            OrbitalRewardKind.RingSpeed => Station.UpgradeRingSpeed(ring.StableRingId),
            OrbitalRewardKind.RingPower => Station.UpgradeRingPower(ring.StableRingId),
            OrbitalRewardKind.RingCapacity => Station.UpgradeRingCapacity(ring.StableRingId),
            OrbitalRewardKind.CoreUpgrade => Station.UpgradeCore(),
            OrbitalRewardKind.LinkMatrix => Station.UpgradeLinkMatrix(),
            OrbitalRewardKind.ModuleDamage => Station.State.Modules.Any(m => Station.UpgradeModuleDamage(m.StableModuleId)),
            _ => Rewards != null && Provider.GetDefinition(kind, Station.State).BodyUpgrade != null &&
                Rewards.TryApplyDebugUpgrade(Provider.GetDefinition(kind, Station.State).BodyUpgrade, out _)
        };
        Notice = applied ? $"Applied {kind}" : $"{kind}: {error}";
        return applied;
    }

    public void ApplyPreset(string preset)
    {
        ResetBuild(() =>
        {
            if (preset == "STARTER") return;
            ClearModules();
            int count = preset == "MANY RINGS" ? OrbitalProgressionConfig.Default.MaxNormalRings : 2;
            while (Station.State.Rings.Count < count && Station.AddRing() != null) { }
            var kinds = preset switch
            {
                "PISTOL" => new[] { OrbitalRewardKind.Pistol },
                "LASER SWORD" => new[] { OrbitalRewardKind.LaserSword },
                "IMPULSE" => new[] { OrbitalRewardKind.ImpulseGun },
                "ARC" => new[] { OrbitalRewardKind.ArcEmitter },
                _ => new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.LaserSword, OrbitalRewardKind.ImpulseGun, OrbitalRewardKind.ArcEmitter }
            };
            for (TargetRing = 0; TargetRing < count; TargetRing++)
            {
                var ring = Station.State.Rings[TargetRing];
                while (ring.MountCount < ring.MountCapacity) Station.AddMount(ring.StableRingId, out _);
                for (int m = 0; m < ring.MountCount; m++) Give(kinds[(TargetRing + m) % kinds.Length]);
            }
            TargetRing = 0;
            Notice = $"Preset {preset}";
        });
    }

    protected void DrawBuildInfo(bool detailed)
    {
        var state = Station.State;
        Text($"Core {state.CoreState.Level} | Rings {state.Rings.Count} | Modules {state.Modules.Count}");
        Text($"Mounts {state.Rings.Sum(r => r.MountCount)} | Free {state.FreeBuiltMounts}");
        if (!detailed) return;
        foreach (var ring in state.Rings)
            Text($"R{ring.Order + 1} [{ring.MountCount}/{ring.MountCapacity}]: " +
                string.Join(", ", state.Modules.Where(m => m.StableRingId == ring.StableRingId).Select(m => m.ModuleType)));
        Text("Reward: " + (Rewards != null && Rewards.IsChoosingUpgrade && Station.RewardFlow.PendingReward == null
            ? "CardSelection" : Station.RewardFlow.CompactStatus));
        Text("Placement: " + Station.InputOwner.Mode);
        Text("Link Pair: " + (Station.RewardFlow.PendingReward == OrbitalRewardKind.LinkPair
            ? Station.RewardFlow.State.ToString() : $"{state.ResolveLinkPairs().Count()} attached"));
    }

    protected void DrawRingTarget()
    {
        TargetRing = Mathf.Clamp(TargetRing, 0, Station.State.Rings.Count - 1);
        Heading($"Direct target: R{TargetRing + 1}");
        TargetRing = GUILayout.SelectionGrid(TargetRing, Station.State.Rings.Select(r => $"R{r.Order + 1}").ToArray(), 4, button);
    }

    protected void DrawOrbitControls()
    {
        Heading("ORBITAL controls");
        Row(("Compress Rings", () => { Station.RightMouseMode = OrbitalStationRuntime.RmbMode.CompressRings; Station.DebugCompressionHeld = true; }),
            ("Expand / Release", () => Station.DebugCompressionHeld = false));
        Button("Reverse Direction", () => Station.DebugReverseRotation());
        Row(("Pause Rotation", () => Station.DebugRotationPaused = true), ("Resume Rotation", () => Station.DebugRotationPaused = false));
        Text($"Compression {Station.DebugCompressionHeld} | Rotation {(Station.DebugRotationPaused ? "paused" : "running")}");
    }

    protected void Button(string text, Action action)
    { if (GUILayout.Button(text, button, GUILayout.MinHeight(27))) action(); }
    protected void Row(params (string label, Action action)[] actions)
    { GUILayout.BeginHorizontal(); foreach (var action in actions) Button(action.label, action.action); GUILayout.EndHorizontal(); }
    protected void Text(string text) => GUILayout.Label(text, label);
    protected void Heading(string text) { GUILayout.Space(6); Text(text); }

    protected abstract void DrawPanel();
    protected virtual void ResetSession() => ResetBuild();
    private void OnGUI()
    {
        if (label == null)
        {
            label = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true, richText = false };
            button = new GUIStyle(GUI.skin.button) { fontSize = 12, wordWrap = true, richText = false };
        }
        if (!PanelVisible) { GUI.Label(new Rect(10, 10, 240, 24), "F1 — show Lab controls"); return; }
        GUILayout.BeginArea(PanelRect, GUI.skin.box);
        Text(GetType().Name.Replace("Controller", "") + "  |  F1 hide");
        Text("WASD move · wheel zoom · Esc cancel placement");
        if (Ready)
        {
            DrawBuildInfo(false);
            scroll = GUILayout.BeginScrollView(scroll);
            DrawPanel();
            GUILayout.EndScrollView();
        }
        else
        {
            Text(resetting ? "Resetting…" : "Player defeated / build unavailable");
            if (!resetting) Button("RESET LAB", ResetSession);
        }
        Text(Notice);
        GUILayout.EndArea();
    }

    protected virtual void OnDestroy()
    {
        if (Rewards != null) Rewards.CancelPendingRewards();
        Station?.Teardown();
        Provider?.Dispose();
        if (ownsManager && manager != null) Destroy(manager.gameObject);
        Time.timeScale = 1f;
    }
}
#endif
