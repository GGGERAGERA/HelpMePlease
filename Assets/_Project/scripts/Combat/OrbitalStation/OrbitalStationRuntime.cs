using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Subject42.Combat.OrbitalStation
{
    [DisallowMultipleComponent]
    public sealed class OrbitalStationRuntime : MonoBehaviour,
        IOrbitalProgressionAdapter
    {
        private enum PlacementStep { None, Ring, Mount }

        private readonly List<OrbitalRingRuntime> rings = new();
        private readonly List<OrbitalModuleRuntime> modules = new();
        private readonly List<(LineRenderer line, float life)> flashes = new();
        private readonly Dictionary<(int First, int Second), LineRenderer> linkLines = new();
        private readonly HashSet<(int First, int Second)> activeLinkPairs = new();
        private readonly List<(int First, int Second)> staleLinkPairs = new();
        private Transform runtimeRoot;
        private OrbitalStationView authoredView;
        private GameObject boundPlayer;
        private Material lineMaterial;
        private Sprite sharedSprite;
        private Sprite sharedCircleSprite;
        private SpriteRenderer coreVisual;
        private float coreFlash;
        private RunStateManager runStateManager;
        private PlacementStep placementStep;
        private OrbitalModuleKind pendingKind;
        private OrbitalRingRuntime placementRing;
        private OrbitalRingRuntime selectedRing;
        private bool initialized;
        private bool tearingDown;
        private bool restoreFailed;
        private OrbitalRelocationController relocation;
        private OrbitalWorldTelekinesisController worldTelekinesis;

        public IOrbitalOwnerAdapter Owner { get; private set; }
        public IOrbitalCombatAdapter Combat { get; private set; }
        public OrbitalCoreRuntime Core { get; private set; }
        public OrbitalRunState State { get; private set; }
        public OrbitalRewardFlowController RewardFlow { get; private set; }
        public OrbitalInteractionPresentation Interaction { get; private set; }
        public IReadOnlyList<OrbitalRingRuntime> Rings => rings;
        public IReadOnlyList<OrbitalModuleRuntime> Modules => modules;
        public OrbitalRingRuntime SelectedRing => selectedRing;
        public bool IsInitialized => initialized;
        // Transient module previews, drag and interaction lines share this authored parent.
        internal Transform RuntimeRoot => authoredView.InteractionRoot;
        internal Material VisualMaterial => lineMaterial;
        internal bool IsDebugPlacementActive => placementStep != PlacementStep.None;
        internal bool IsRelocating => relocation != null && relocation.IsDragging;
        public string PlacementStatus => placementStep switch
        {
            PlacementStep.Ring => $"{pendingKind}: CLICK RING",
            PlacementStep.Mount => $"{pendingKind}: CLICK FREE MOUNT",
            _ => "READY"
        };

        public static OrbitalStationRuntime Ensure(GameObject player)
        {
            if (player == null)
                return null;
            OrbitalStationRuntime station = player.GetComponentInChildren<OrbitalStationRuntime>(true);
            if (station == null)
            {
                var config = OrbitalPresentationConfig.Active;
                if (config == null || config.StationPrefab == null)
                { Debug.LogError("[OrbitalStation] required authored station prefab is missing", player); return null; }
                station = Instantiate(config.StationPrefab, player.transform, false).GetComponent<OrbitalStationRuntime>();
                if (station == null) { Debug.LogError("[OrbitalStation] authored station owner is missing", player); return null; }
            }
            station.boundPlayer = player;
            station.Initialize();
            return station;
        }

        public static OrbitalStationRuntime CreateFromState(
            GameObject player, OrbitalRunState state)
        {
            if (player == null || state == null)
                return null;
            RunStateManager manager = RunStateManager.EnsureExists();
            if (!ReferenceEquals(manager.OrbitalStationState, state))
            {
                Debug.LogWarning(
                    "[OrbitalStation] Restore rejected: state is not owned by current RunStateManager.");
                return null;
            }
            return Ensure(player);
        }

        public void Initialize()
        {
            if (initialized || restoreFailed)
                return;
            runStateManager = RunStateManager.EnsureExists();
            if (!runStateManager.TryGetOrbitalRunState(out OrbitalRunState state, out string error))
            {
                FailRestore(error);
                return;
            }
            State = state;
            try
            {
                if (!OrbitalPresentationConfig.TryGetRequired(out var config, out error))
                {
                    FailRestore(error);
                    return;
                }
                authoredView = GetComponent<OrbitalStationView>();
                if (authoredView == null || !authoredView.IsValid)
                { FailRestore("required authored station references are missing"); return; }
                tearingDown = false;
                runtimeRoot = authoredView.transform;
                sharedSprite = config.PixelSprite;
                sharedCircleSprite = config.CircleSprite;
                lineMaterial = config.VisualMaterial;
                coreVisual = authoredView.Core;
                coreVisual.enabled = true;
                Owner = new ProductionOrbitalOwnerAdapter(boundPlayer != null ? boundPlayer : gameObject);
                Combat = new ProductionOrbitalCombatAdapter(authoredView.EffectsRoot, sharedSprite);
                Core = new OrbitalCoreRuntime(State.CoreState);
                InputOwner = authoredView.Input;
                InputOwner.Bind(this);
                Interaction = authoredView.Presentation;
                Interaction.Bind(this);
                RewardFlow = authoredView.Rewards;
                RewardFlow.Bind(this);
                relocation = authoredView.Relocation;
                relocation.Bind(this);
                worldTelekinesis = authoredView.World;
                worldTelekinesis.Bind(this);
                if (!BuildPresentationFromState(out string restoreError))
                {
                    FailRestore(restoreError);
                    return;
                }
                initialized = true;
                enabled = true;
                InputOwner.enabled = true;
                Interaction.enabled = true;
                RewardFlow.enabled = true;
                relocation.enabled = true;
                worldTelekinesis.enabled = true;
                gameObject.SetActive(true);
            }
            catch (System.Exception exception)
            {
                FailRestore($"presentation exception {exception.GetType().Name}: {exception.Message}");
            }
        }

        private void FailRestore(string reason)
        {
            if (restoreFailed)
                return;
            restoreFailed = true;
            initialized = false;
            enabled = false;
            StopAllCoroutines();
            // Cancel staged presentation before disabling; cancellation never mutates run state.
            OrbitalRewardFlowController reward = GetComponent<OrbitalRewardFlowController>();
            reward?.AbortForRestoreFailure();
            if (reward != null) reward.enabled = false;
            relocation = GetComponent<OrbitalRelocationController>();
            worldTelekinesis = GetComponent<OrbitalWorldTelekinesisController>();
            Interaction = GetComponent<OrbitalInteractionPresentation>();
            if (relocation != null) relocation.enabled = false;
            if (worldTelekinesis != null) worldTelekinesis.enabled = false;
            if (Interaction != null) Interaction.enabled = false;
            Teardown();
            gameObject.SetActive(false);
            // Retain the authoritative reference for diagnostics and existing operation callers.
            State = runStateManager?.OrbitalStationState;
            Debug.LogError($"[OrbitalStation] operation=restore RunId={runStateManager?.OrbitalStationState?.RunId.ToString() ?? "missing"} " +
                $"scene={gameObject.scene.name} component={nameof(OrbitalStationRuntime)} reason={reason}", this);
        }

        private void Update()
        {
            if (!initialized || tearingDown)
                return;
            // Interface-backed adapters are intentionally scene-local and are
            // not serialized. A development hot reload must fail closed.
            if (Owner == null || Core == null || Combat == null)
            {
                Teardown();
                return;
            }
            if (Owner.IsDead)
            {
                Teardown();
                return;
            }
            float deltaTime = Time.deltaTime;
            Core.Tick(deltaTime, rings);
            for (int i = 0; i < rings.Count; i++)
                rings[i].Tick(deltaTime);
            for (int i = 0; i < modules.Count; i++)
                modules[i].Tick(deltaTime);
            Combat.Tick(deltaTime);
            UpdateLinkNodes(deltaTime);
            UpdateFlashes(Time.unscaledDeltaTime);
            coreFlash = Mathf.MoveTowards(coreFlash, 0f, deltaTime * 5f);
            if (coreVisual != null)
            {
                coreVisual.transform.localScale = Vector3.one *
                    Mathf.Lerp(0.34f, 0.48f, coreFlash);
                coreVisual.color = Color.Lerp(new Color(0.72f, 0.25f, 1f),
                    Color.white, coreFlash);
            }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            HandlePlacementInput();
#endif
        }

        public OrbitalRingState AddRing()
        {
            OrbitalRingState ring = State?.AddRing();
            if (ring == null) return null;
            SyncCommitted("AddRing", ring.StableRingId, 0, () => SelectRing(CreateRingPresentation(ring, true)));
            return ring;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public OrbitalRingState DebugAddRingBeyondCap()
        {
            OrbitalRingState ring = State?.DebugAddRingBeyondCap();
            if (ring == null) return null;
            SyncCommitted("DebugAddRingBeyondCap", ring.StableRingId, 0, () => SelectRing(CreateRingPresentation(ring, true)));
            return ring;
        }
#endif

        public void BeginModulePlacement(OrbitalModuleKind kind)
        {
            if (!initialized || InputOwner == null || !InputOwner.CanQueueDebugPlacement)
                return;
            pendingKind = kind;
            placementStep = PlacementStep.Ring;
            placementRing = null;
            Debug.Log($"[OrbitalStation] Placement {kind}: click a ring, then a free mount.", this);
        }

        public void UpgradeSelectedRingSpeed()
        {
            if (selectedRing != null)
                UpgradeRingSpeed(selectedRing.RingId);
        }

        public void UpgradeSelectedRingPower()
        {
            if (selectedRing != null)
                UpgradeRingPower(selectedRing.RingId);
        }

        public bool UpgradeRingSpeed(int stableRingId)
        {
            if (State == null || !State.UpgradeRingSpeed(stableRingId)) return false;
            SyncCommitted("RingSpeed", stableRingId, 0, () => SelectRing(RequireRing(stableRingId)));
            return true;
        }

        public bool UpgradeRingPower(int stableRingId)
        {
            if (State == null || !State.UpgradeRingPower(stableRingId)) return false;
            SyncCommitted("RingPower", stableRingId, 0, () => SelectRing(RequireRing(stableRingId)));
            return true;
        }

        public void AddMount()
        {
            if (selectedRing == null)
                return;
            if (!AddMount(selectedRing.RingId, out string error))
            {
                Debug.LogWarning($"[OrbitalRunState] AddMount: {error}", this);
            }
        }

        public bool AddMount(int stableRingId, out string error)
        {
            error = null;
            if (State == null)
            {
                error = "state is missing";
                return false;
            }
            if (!State.AddMount(stableRingId, out error))
                return false;
            SyncCommitted("AddMount", stableRingId, 0, () =>
            {
                OrbitalRingRuntime ring = RequireRing(stableRingId);
                if (ring.Mounts.Count != ring.State.MountCapacity - 1)
                    throw new System.InvalidOperationException("mount cache does not match pre-commit capacity");
                ring.AddMount(runtimeRoot, sharedCircleSprite);
            });
            return true;
        }

        public bool UpgradeCore()
        {
            if (State == null || !State.UpgradeCore()) return false;
            SyncCommitted("CoreUpgrade", 0, 0, () => FlashCore(new Color(0.85f, 0.3f, 1f)));
            return true;
        }

        public bool UpgradeLinkMatrix()
        {
            if (State == null || !State.UpgradeLinkMatrix())
                return false;
            SyncCommitted("LinkMatrix", 0, 0, () => FlashCore(new Color(0.9f, 0.2f, 1f)));
            return true;
        }

        public bool ProcessPlayerLevelMilestone(int playerLevel)
        {
            if (State == null || !State.ProcessPlayerLevelMilestone(playerLevel, out OrbitalRingState ring)) return false;
            if (ring == null) return true; // The level marker was committed without a ring milestone.
            SyncCommitted("PlayerLevelMilestone", ring.StableRingId, 0, () =>
            {
                SelectRing(CreateRingPresentation(ring, true));
                FlashCore(new Color(0.75f, 0.25f, 1f));
                RunMessageService.Instance?.ShowCustom(string.Empty,
                    $"ТЕЛЕКИНЕТИЧЕСКИЙ УРОВЕНЬ: {State.Rings.Count}", 1.35f);
                StartCoroutine(CompensateCameraForRadius(ring.Radius));
            });
            return true;
        }


#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public void ApplyPresetStart()
        {
            if (runStateManager == null)
                return;
            State = runStateManager.DebugResetOrbitalRunState();
            RebuildRuntimeFromState();
        }

        public void ApplyPresetMid()
        {
            ResetStation();
            OrbitalRingState second = AddRing();
            InstallFirstFree(rings[0].State, OrbitalModuleKind.LaserSword);
            InstallFirstFree(second, OrbitalModuleKind.ArcEmitter);
            UpgradeCore();
        }

        public void ApplyPresetFinal()
        {
            ApplyPresetMid();
            OrbitalRingState third = AddRing();
            InstallFirstFree(rings[0].State, OrbitalModuleKind.LinkNode);
            InstallFirstFree(rings[1].State, OrbitalModuleKind.LinkNode);
            InstallFirstFree(third, OrbitalModuleKind.ImpulseGun);
            SelectRing(rings[0]);
            UpgradeSelectedRingPower();
            SelectRing(rings[1]);
            UpgradeSelectedRingSpeed();
            UpgradeCore();
        }

        public void ApplyReadabilityTestPreset()
        {
            ApplyPresetStart();
            OrbitalRingState second = AddRing();
            OrbitalRingState third = AddRing();
            InstallModule(OrbitalModuleKind.LinkNode, rings[0].RingId, 1, out _);
            InstallModule(OrbitalModuleKind.LinkNode, second.StableRingId, 0, out _);
            InstallModule(OrbitalModuleKind.LaserSword, second.StableRingId, 1, out _);
            InstallModule(OrbitalModuleKind.ImpulseGun, third.StableRingId, 0, out _);
            InstallModule(OrbitalModuleKind.ArcEmitter, third.StableRingId, 1, out _);
            if (Camera.main != null && Camera.main.orthographic)
                Camera.main.orthographicSize = 8.1f;
        }

#endif

        internal GameObject CreatePixelVisual(string name, Color color,
            Vector2 scale, int sortingOrder)
            => CreateSpriteVisual(name, color, scale, sortingOrder, sharedSprite);

        internal GameObject CreateCircleVisual(string name, Color color,
            Vector2 scale, int sortingOrder)
            => CreateSpriteVisual(name, color, scale, sortingOrder, sharedCircleSprite);

        private GameObject CreateSpriteVisual(string name, Color color,
            Vector2 scale, int sortingOrder, Sprite sprite)
        {
            GameObject visual = new(name);
            visual.transform.SetParent(runtimeRoot != null ? runtimeRoot : transform, false);
            visual.transform.localScale = new Vector3(scale.x, scale.y, 1f);
            SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.color = color;
            renderer.sortingLayerName = "Player";
            renderer.sortingOrder = sortingOrder;
            return visual;
        }

        public GameObject CreateModuleVisual(string name, Color color, Vector2 scale) =>
            CreatePixelVisual(name, color, scale, 14);

        public void FlashCore(Color color)
        {
            coreFlash = 1f;
            if (coreVisual != null)
                coreVisual.color = color;
        }

        public void FlashLink(Vector2 from, Vector2 to, Color color, float life)
        {
            if (!initialized)
                return;
            GameObject gameObject = new("Orbital Arc Flash");
            gameObject.transform.SetParent(authoredView.EffectsRoot, true);
            LineRenderer line = gameObject.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.widthMultiplier = 0.045f;
            line.sharedMaterial = lineMaterial;
            line.sortingLayerName = "Player";
            line.sortingOrder = 13;
            line.startColor = line.endColor = color;
            flashes.Add((line, life));
        }

        public OrbitalInteractionController InputOwner { get; private set; }

        public void Teardown()
        {
            if (tearingDown)
                return;
            tearingDown = true;
            RewardFlow?.CancelForSceneTransition();
            initialized = false;
            worldTelekinesis?.CancelInteraction();
            relocation?.CancelDrag("station teardown");
            Interaction?.Release();
            placementStep = PlacementStep.None;
            for (int i = 0; i < rings.Count; i++)
                rings[i].Teardown();
            rings.Clear();
            modules.Clear();
            Combat?.Teardown();
            for (int i = 0; i < flashes.Count; i++)
                if (flashes[i].line != null)
                    Destroy(flashes[i].line.gameObject);
            flashes.Clear();
            foreach (LineRenderer line in linkLines.Values)
                if (line != null) Destroy(line.gameObject);
            linkLines.Clear();
            activeLinkPairs.Clear();
            staleLinkPairs.Clear();
            if (coreVisual != null) coreVisual.enabled = false;
            runtimeRoot = null;
            lineMaterial = null;
            sharedSprite = null;
            sharedCircleSprite = null;
            coreVisual = null;
            Combat = null;
            Owner = null;
            Core = null;
            RewardFlow = null;
            Interaction = null;
            relocation = null;
            worldTelekinesis = null;
            State = null;
            initialized = false;
            tearingDown = false;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void ResetStation()
        {
            ApplyPresetStart();
        }

#endif

        private void InstallFirstFree(OrbitalRingState ring, OrbitalModuleKind kind)
        {
            if (ring == null) return;
            for (int i = 0; i < ring.MountCapacity; i++)
                if (State.CanInstallModule(kind, ring.StableRingId, i, out _))
                {
                    InstallModule(kind, ring.StableRingId, i, out _);
                    return;
                }
        }

        private bool InstallModule(OrbitalMountRuntime mount, OrbitalModuleKind kind)
        {
            if (mount == null)
                return false;
            return InstallModule(kind, mount.Ring.RingId, mount.MountIndex,
                out _);
        }

        public bool InstallModule(OrbitalModuleKind kind, int stableRingId,
            int mountIndex, out string error)
        {
            error = "state is missing";
            if (State == null || !State.CanInstallModule(kind, stableRingId, mountIndex, out error)) return false;
            if (!State.InstallModule(kind, stableRingId, mountIndex, out OrbitalModuleState module)) return false;
            error = null;
            SyncCommitted("Install", stableRingId, module.StableModuleId, () =>
            {
                if (!OrbitalPresentationConfig.TryGetRequired(out _, out string reason))
                    throw new System.InvalidOperationException(reason);
                if (!InstallModulePresentation(RequireMount(stableRingId, mountIndex), module))
                    throw new System.InvalidOperationException("module visual attach failed");
            });
            return true;
        }

        public bool InstallLinkPair(int ringA, int mountA, int ringB, int mountB, out string error)
        {
            error = "state is missing";
            if (State == null || !State.InstallLinkPair(ringA, mountA, ringB, mountB,
                out var first, out var second, out error)) return false;
            SyncCommitted("InstallLinkPair", ringA, first.StableModuleId, () =>
            {
                if (!OrbitalPresentationConfig.TryGetRequired(out _, out string reason))
                    throw new System.InvalidOperationException(reason);
                if (!InstallModulePresentation(RequireMount(ringA, mountA), first) ||
                    !InstallModulePresentation(RequireMount(ringB, mountB), second))
                    throw new System.InvalidOperationException("Link pair visual attach failed");
            });
            return true;
        }

        private bool InstallModulePresentation(OrbitalMountRuntime mount,
            OrbitalModuleState moduleState)
        {
            OrbitalModuleKind kind = moduleState.ModuleType;
            OrbitalModuleRuntime module = kind switch
            {
                OrbitalModuleKind.Pistol => new OrbitalPistolModule(this, moduleState.StableModuleId),
                OrbitalModuleKind.LaserSword => new OrbitalLaserSwordModule(this, moduleState.StableModuleId),
                OrbitalModuleKind.ImpulseGun => new OrbitalImpulseGunModule(this, moduleState.StableModuleId),
                OrbitalModuleKind.ArcEmitter => new OrbitalArcEmitterModule(this, moduleState.StableModuleId),
                OrbitalModuleKind.LinkNode => new OrbitalLinkNodeModule(this, moduleState.StableModuleId),
                _ => null
            };
            if (module == null || !mount.Attach(module))
            {
                module?.Teardown();
                return false;
            }
            modules.Add(module);
            return true;
        }

        public bool MoveModule(int stableModuleId, int targetRingId,
            int targetMountIndex, out string error)
        {
            error = "state is missing";
            if (State == null || !State.MoveModule(stableModuleId, targetRingId, targetMountIndex, out error)) return false;
            SyncCommitted("Move", targetRingId, stableModuleId, () =>
            {
                OrbitalModuleRuntime module = RequireModule(stableModuleId);
                OrbitalMountRuntime target = RequireMount(targetRingId, targetMountIndex);
                if (module.CurrentMount == null) throw new System.InvalidOperationException("source mount cache missing");
                module.CurrentMount.Detach();
                if (!target.Attach(module)) throw new System.InvalidOperationException("target mount cache rejected attach");
                module.CancelPresentationDrag();
            });
            return true;
        }

        public bool RemoveModule(int stableModuleId)
        {
            if (State == null || !State.RemoveModule(stableModuleId)) return false;
            SyncCommitted("RemoveModule", 0, stableModuleId, () => RemoveModulePresentation(RequireModule(stableModuleId)));
            return true;
        }

        public float GetModuleDamageMultiplier(int stableModuleId)
        {
            OrbitalModuleState module = State?.Modules.Find(value =>
                value.StableModuleId == stableModuleId);
            return 1f + Mathf.Max(0, module?.DamageLevel ?? 0) * 0.25f;
        }

        public float GetModuleMetaDamageMultiplier(OrbitalModuleKind kind)
        {
            return MetaProgressionManager
                .GetStoredOrbitalModuleDamageMultiplier(kind);
        }

        public bool UpgradeModuleDamage(int stableModuleId)
        {
            if (State == null || !State.UpgradeModuleDamage(stableModuleId)) return false;
            SyncCommitted("ModuleUpgrade", 0, stableModuleId, () => RequireModule(stableModuleId).TriggerUpgradePresentation());
            return true;
        }

        public bool SetModuleRewardPresentationVisible(int stableModuleId, bool visible)
        {
            OrbitalModuleRuntime module = modules.Find(value => value.StableModuleId == stableModuleId);
            if (module == null) return false;
            module.SetRewardPresentationVisible(visible);
            return true;
        }

        public bool RemoveRing(int stableRingId, out string error)
        {
            error = "state is missing";
            if (State == null || !State.RemoveRing(stableRingId, out error)) return false;
            SyncCommitted("RemoveRing", stableRingId, 0, () =>
            {
                OrbitalRingRuntime ring = RequireRing(stableRingId);
                foreach (OrbitalModuleRuntime module in modules.Where(m => m.CurrentMount?.Ring.RingId == stableRingId).ToArray())
                    RemoveModulePresentation(module);
                ring.Teardown();
                rings.Remove(ring);
                if (selectedRing == ring) SelectRing(rings.FirstOrDefault());
            });
            return true;
        }

        public bool IsMountFree(OrbitalMountRuntime mount) => mount != null && State != null &&
            State.IsMountFree(mount.Ring.RingId, mount.MountIndex);

        private OrbitalRingRuntime RequireRing(int id) => rings.Find(r => r.RingId == id) ??
            throw new System.InvalidOperationException($"ring runtime {id} missing");

        private OrbitalModuleRuntime RequireModule(int id) => modules.Find(m => m.StableModuleId == id) ??
            throw new System.InvalidOperationException($"module runtime {id} missing");

        private OrbitalMountRuntime RequireMount(int ringId, int index)
        {
            OrbitalMountRuntime mount = RequireRing(ringId).Mounts.Find(m => m.MountIndex == index);
            if (mount == null || mount.Transform == null)
                throw new System.InvalidOperationException($"mount runtime {ringId}:{index} missing");
            return mount;
        }

        private void RemoveModulePresentation(OrbitalModuleRuntime module)
        {
            module.CurrentMount?.Detach();
            module.Teardown();
            modules.Remove(module);
        }

        private void SyncCommitted(string operation, int ringId, int moduleId, System.Action sync)
        {
            try
            {
                if (!initialized) throw new System.InvalidOperationException("station presentation is not initialized");
                sync();
            }
            catch (System.Exception exception)
            {
                // Keep the committed data and runtime references. Explicit restore owns full cleanup.
                // Do not disable/cancel an in-flight reward here: its caller must observe the commit.
                restoreFailed = true;
                initialized = false;
                enabled = false;
                string reason = exception.Message;
                try
                {
                    relocation?.CancelDrag("presentation failed");
                    worldTelekinesis?.CancelInteraction();
                    Interaction?.Release();
                }
                catch (System.Exception cleanupError)
                {
                    reason += $"; interaction cleanup failed: {cleanupError.Message}";
                }
                Debug.LogError($"[OrbitalStation] operation={operation} RunId={State?.RunId} ring={ringId} module={moduleId} " +
                    $"state committed; incremental presentation sync failed: {reason}", this);
            }
        }

        public OrbitalRunState CaptureState()
        {
            return State;
        }

        public bool ValidateState(out string error)
        {
            if (State == null)
            {
                error = "state is missing";
                return false;
            }
            return State.Validate(out error);
        }

        public bool RebuildRuntimeFromState(int preferredRingId = 0)
        {
            if (preferredRingId == 0 && selectedRing != null)
                preferredRingId = selectedRing.RingId;
            Teardown();
            restoreFailed = false; // Explicit retry only; Ensure never retries a failed station.
            Initialize();
            OrbitalRingRuntime preferred = rings.Find(value =>
                value.RingId == preferredRingId);
            if (preferred != null)
                SelectRing(preferred);
            return initialized;
        }

        public bool SimulateSectorRestore()
        {
            return RebuildRuntimeFromState();
        }

        private bool BuildPresentationFromState(out string error)
        {
            error = null;
            if (State == null || !State.Validate(out error))
                return false;
            List<OrbitalRingState> ordered = State.Rings
                .OrderBy(value => value.Order).ToList();
            for (int i = 0; i < ordered.Count; i++)
                CreateRingPresentation(ordered[i]);
            for (int i = 0; i < State.Modules.Count; i++)
            {
                OrbitalModuleState moduleState = State.Modules[i];
                OrbitalRingRuntime ring = rings.Find(value =>
                    value.RingId == moduleState.StableRingId);
                if (ring == null || moduleState.MountIndex < 0 ||
                    moduleState.MountIndex >= ring.Mounts.Count ||
                    !InstallModulePresentation(
                        ring.Mounts[moduleState.MountIndex], moduleState))
                {
                    error = $"module {moduleState.StableModuleId} presentation failed";
                    return false;
                }
            }
            SelectRing(rings.Count > 0 ? rings[0] : null);
            error = "OK";
            return true;
        }

        private OrbitalRingRuntime CreateRingPresentation(
            OrbitalRingState ringState, bool animateSpawn = false)
        {
            OrbitalRingRuntime ring = new(ringState, authoredView.RingsRoot,
                lineMaterial, sharedCircleSprite, animateSpawn);
            rings.Add(ring);
            return ring;
        }

        private System.Collections.IEnumerator CompensateCameraForRadius(
            float radius)
        {
            Camera camera = Camera.main;
            if (camera == null || !camera.orthographic)
                yield break;
            float start = camera.orthographicSize;
            float target = Mathf.Max(start, radius + 1.6f);
            float elapsed = 0f;
            const float duration = 0.45f;
            while (elapsed < duration && camera != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                camera.orthographicSize = Mathf.Lerp(start, target,
                    1f - Mathf.Pow(1f - t, 3f));
                yield return null;
            }
        }

        private void SelectRing(OrbitalRingRuntime ring)
        {
            selectedRing?.SetSelected(false);
            selectedRing = ring;
            selectedRing?.SetSelected(true);
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private void HandlePlacementInput()
        {
            for (int i = 0; i < Mathf.Min(9, rings.Count); i++)
                if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    SelectRing(rings[i]);
            if (placementStep == PlacementStep.None || Time.timeScale <= 0f ||
                !Input.GetMouseButtonDown(0) || Camera.main == null ||
                (InputOwner == null || !InputOwner.CanUseDebugPlacement))
                return;
            Vector3 world3 = Camera.main.ScreenToWorldPoint(Input.mousePosition);
            Vector2 local = transform.InverseTransformPoint(world3);
            if (placementStep == PlacementStep.Ring)
            {
                placementRing = FindRingAt(local);
                if (placementRing == null)
                    return;
                SelectRing(placementRing);
                placementStep = PlacementStep.Mount;
                return;
            }
            OrbitalMountRuntime mount = FindMountAt(placementRing, world3);
            if (mount == null || !InstallModule(mount, pendingKind))
                return;
            placementStep = PlacementStep.None;
            placementRing = null;
        }
#endif

        private OrbitalRingRuntime FindRingAt(Vector2 local)
        {
            OrbitalRingRuntime best = null;
            float bestDelta = 0.3f;
            for (int i = 0; i < rings.Count; i++)
            {
                float delta = Mathf.Abs(local.magnitude - rings[i].Radius);
                if (delta < bestDelta)
                {
                    bestDelta = delta;
                    best = rings[i];
                }
            }
            return best;
        }

        private OrbitalMountRuntime FindMountAt(OrbitalRingRuntime ring,
            Vector2 world)
        {
            if (ring == null)
                return null;
            OrbitalMountRuntime best = null;
            float bestDistance = 0.35f * 0.35f;
            for (int i = 0; i < ring.Mounts.Count; i++)
            {
                OrbitalMountRuntime mount = ring.Mounts[i];
                if (!IsMountFree(mount) || mount.Transform == null)
                    continue;
                float distance = ((Vector2)mount.Transform.position - world).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = mount;
                }
            }
            return best;
        }

        private void UpdateLinkNodes(float deltaTime)
        {
            activeLinkPairs.Clear();
            foreach (var pair in State.ResolveLinkPairs())
            {
                var first = modules.Find(m => m.StableModuleId == pair.First) as OrbitalLinkNodeModule;
                var second = modules.Find(m => m.StableModuleId == pair.Second) as OrbitalLinkNodeModule;
                if (first?.CurrentMount == null || second?.CurrentMount == null) continue;
                Vector2 from = first.CurrentMount.Transform.position;
                Vector2 to = second.CurrentMount.Transform.position;
                activeLinkPairs.Add(pair);
                UpdateLinkLine(pair, from, to);
                if (first.RuntimeCooldown > 0f)
                    continue;
                EnemyHealth target = Combat.FindNearest((from + to) * 0.5f, 2f);
                if (target != null && DistanceToSegment(target.transform.position,
                    from, to) < 0.4f)
                {
                    float linkPower = (first.CurrentMount.Ring.PowerMultiplier +
                        second.CurrentMount.Ring.PowerMultiplier) * 0.5f;
                    float matrixPower = 1f + State.CoreState.LinkMatrixUpgradeLevel * 0.25f;
                    Combat.ApplyDamage(target, 5f * linkPower * matrixPower *
                        Core.DamageMultiplier, target.transform.position);
                    first.RuntimeCooldown = 0.55f;
                }
            }
            ReleaseInactiveLinkLines();
        }

        private void UpdateLinkLine((int First, int Second) pair, Vector2 from, Vector2 to)
        {
            if (!linkLines.TryGetValue(pair, out LineRenderer line) || line == null)
            {
                GameObject linkObject = new("Orbital Link");
                linkObject.transform.SetParent(authoredView.EffectsRoot, true);
                line = linkObject.AddComponent<LineRenderer>();
                line.useWorldSpace = true;
                line.positionCount = 2;
                line.widthMultiplier = 0.045f;
                line.sharedMaterial = lineMaterial;
                line.sortingLayerName = "Player";
                line.sortingOrder = 13;
                line.startColor = line.endColor = new Color(0.7f, 0.2f, 1f, 0.35f);
                linkLines[pair] = line;
            }
            line.SetPosition(0, from);
            line.SetPosition(1, to);
        }

        private void ReleaseInactiveLinkLines()
        {
            staleLinkPairs.Clear();
            foreach (var entry in linkLines)
                if (!activeLinkPairs.Contains(entry.Key)) staleLinkPairs.Add(entry.Key);
            foreach (var key in staleLinkPairs)
            {
                if (linkLines[key] != null) Destroy(linkLines[key].gameObject);
                linkLines.Remove(key);
            }
        }

        private void UpdateFlashes(float deltaTime)
        {
            for (int i = flashes.Count - 1; i >= 0; i--)
            {
                (LineRenderer line, float life) item = flashes[i];
                item.life -= deltaTime;
                if (item.life > 0f)
                {
                    flashes[i] = item;
                    continue;
                }
                if (item.line != null)
                    Destroy(item.line.gameObject);
                flashes.RemoveAt(i);
            }
        }

        private static float DistanceToSegment(Vector2 point, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            if (ab.sqrMagnitude < 0.0001f)
                return Vector2.Distance(point, a);
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(point, a + ab * t);
        }

        private void OnDestroy()
        {
            Teardown();
        }
    }
}
