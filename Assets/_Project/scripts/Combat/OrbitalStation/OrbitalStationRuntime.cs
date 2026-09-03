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
        private Transform runtimeRoot;
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
        internal Transform RuntimeRoot => runtimeRoot;
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
            OrbitalStationRuntime station = player.GetComponent<OrbitalStationRuntime>();
            station ??= player.AddComponent<OrbitalStationRuntime>();
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
            if (initialized)
                return;
            runStateManager = RunStateManager.EnsureExists();
            State = runStateManager.EnsureOrbitalRunState();
            if (!State.Validate(out string stateError))
            {
                Debug.LogWarning(
                    $"[OrbitalStation] Restore state invalid ({stateError}); using base state.", this);
                State = runStateManager.ResetOrbitalRunState();
            }
            tearingDown = false;
            runtimeRoot = new GameObject("Orbital Station Runtime").transform;
            runtimeRoot.SetParent(transform, false);
            Texture2D texture = new(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Orbital Station Pixel",
                hideFlags = HideFlags.DontSave
            };
            texture.SetPixel(0, 0, Color.white);
            texture.Apply();
            sharedSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1),
                new Vector2(0.5f, 0.5f), 1f);
            sharedSprite.name = "Orbital Station Sprite";
            sharedCircleSprite = CreateCircleSprite();
            lineMaterial = new Material(Shader.Find("Sprites/Default"))
            {
                name = "Orbital Station Lines",
                hideFlags = HideFlags.DontSave
            };
            GameObject coreObject = CreateCircleVisual("Orbital Core",
                new Color(0.72f, 0.25f, 1f), new Vector2(0.34f, 0.34f), 16);
            coreObject.transform.SetParent(runtimeRoot, false);
            coreVisual = coreObject.GetComponent<SpriteRenderer>();
            Owner = new ProductionOrbitalOwnerAdapter(gameObject);
            Combat = new ProductionOrbitalCombatAdapter(runtimeRoot, sharedSprite);
            Core = new OrbitalCoreRuntime(State.CoreState);
            Interaction = GetComponent<OrbitalInteractionPresentation>();
            Interaction ??= gameObject.AddComponent<OrbitalInteractionPresentation>();
            Interaction.Bind(this);
            RewardFlow = GetComponent<OrbitalRewardFlowController>();
            RewardFlow ??= gameObject.AddComponent<OrbitalRewardFlowController>();
            RewardFlow.Bind(this);
            relocation = GetComponent<OrbitalRelocationController>();
            relocation ??= gameObject.AddComponent<OrbitalRelocationController>();
            relocation.Bind(this);
            worldTelekinesis = GetComponent<OrbitalWorldTelekinesisController>();
            worldTelekinesis ??=
                gameObject.AddComponent<OrbitalWorldTelekinesisController>();
            worldTelekinesis.Bind(this);
            initialized = true;
            if (!BuildPresentationFromState(out string restoreError))
            {
                Debug.LogWarning(
                    $"[OrbitalStation] Restore failed ({restoreError}); rebuilding base state.", this);
                Teardown();
                State = runStateManager.ResetOrbitalRunState();
                Initialize();
                return;
            }
            State.MarkRestored();
            Debug.Log($"[OrbitalStation] Restored run {State.RunId}: " +
                $"Core {Core.Level}, Rings {rings.Count}, Modules {modules.Count}.", this);
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

        public OrbitalRingRuntime AddRing()
        {
            if (!initialized || State == null)
                return null;
            OrbitalRingState ringState = State.AddRing();
            try
            {
                OrbitalRingRuntime ring = CreateRingPresentation(ringState, true);
                SelectRing(ring);
                return ring;
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception, this);
                RebuildRuntimeFromState();
                return rings.Find(value => value.RingId == ringState.StableRingId);
            }
        }

        public void BeginModulePlacement(OrbitalModuleKind kind)
        {
            if (!initialized)
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
            if (State == null || !State.UpgradeRingSpeed(stableRingId))
                return false;
            return RebuildRuntimeFromState(stableRingId);
        }

        public bool UpgradeRingPower(int stableRingId)
        {
            if (State == null || !State.UpgradeRingPower(stableRingId))
                return false;
            return RebuildRuntimeFromState(stableRingId);
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
            return RebuildRuntimeFromState(stableRingId);
        }

        public void UpgradeCore()
        {
            if (State == null)
                return;
            if (State.UpgradeCore())
                FlashCore(new Color(0.85f, 0.3f, 1f));
        }

        public bool UpgradeLinkMatrix()
        {
            if (State == null || !State.UpgradeLinkMatrix())
                return false;
            FlashCore(new Color(0.9f, 0.2f, 1f));
            return true;
        }

        public bool ProcessPlayerLevelMilestone(int playerLevel)
        {
            if (State == null || playerLevel <= State.LastProcessedPlayerLevel)
                return false;
            State.MarkPlayerLevelProcessed(playerLevel);
            OrbitalProgressionConfig config = OrbitalProgressionConfig.Default;
            if (!config.IsRingMilestone(playerLevel) ||
                State.Rings.Count >= config.MaxNormalRings)
                return false;
            OrbitalRingRuntime ring = AddRing();
            if (ring == null)
                return false;
            FlashCore(new Color(0.75f, 0.25f, 1f));
            RunMessageService.Instance?.ShowCustom(
                string.Empty,
                $"ТЕЛЕКИНЕТИЧЕСКИЙ УРОВЕНЬ: {State.Rings.Count}",
                1.35f);
            StartCoroutine(CompensateCameraForRadius(ring.Radius));
            return true;
        }

        public void ApplyPresetStart()
        {
            if (runStateManager == null)
                return;
            State = runStateManager.ResetOrbitalRunState();
            RebuildRuntimeFromState();
        }

        public void ApplyPresetMid()
        {
            ResetStation();
            OrbitalRingRuntime second = AddRing();
            InstallFirstFree(rings[0], OrbitalModuleKind.LaserSword);
            InstallFirstFree(second, OrbitalModuleKind.ArcEmitter);
            UpgradeCore();
        }

        public void ApplyPresetFinal()
        {
            ApplyPresetMid();
            OrbitalRingRuntime third = AddRing();
            InstallFirstFree(rings[0], OrbitalModuleKind.LinkNode);
            InstallFirstFree(rings[1], OrbitalModuleKind.LinkNode);
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
            OrbitalRingRuntime second = AddRing();
            OrbitalRingRuntime third = AddRing();
            InstallModule(OrbitalModuleKind.LinkNode, rings[0].RingId, 1, out _);
            InstallModule(OrbitalModuleKind.LinkNode, second.RingId, 0, out _);
            InstallModule(OrbitalModuleKind.LaserSword, second.RingId, 1, out _);
            InstallModule(OrbitalModuleKind.ImpulseGun, third.RingId, 0, out _);
            InstallModule(OrbitalModuleKind.ArcEmitter, third.RingId, 1, out _);
            if (Camera.main != null && Camera.main.orthographic)
                Camera.main.orthographicSize = 8.1f;
        }

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
            gameObject.transform.SetParent(runtimeRoot, true);
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

        public void Teardown()
        {
            if (!initialized || tearingDown)
                return;
            tearingDown = true;
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
            if (runtimeRoot != null)
                Destroy(runtimeRoot.gameObject);
            if (lineMaterial != null)
                Destroy(lineMaterial);
            if (sharedSprite != null)
            {
                Texture2D texture = sharedSprite.texture;
                Destroy(sharedSprite);
                if (texture != null)
                    Destroy(texture);
            }
            if (sharedCircleSprite != null)
            {
                Texture2D texture = sharedCircleSprite.texture;
                Destroy(sharedCircleSprite);
                if (texture != null)
                    Destroy(texture);
            }
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
            Debug.Log("[OrbitalStation] Teardown complete.", this);
        }

        private void ResetStation()
        {
            ApplyPresetStart();
        }

        private void InstallFirstFree(OrbitalRingRuntime ring, OrbitalModuleKind kind)
        {
            if (ring == null)
                return;
            for (int i = 0; i < ring.Mounts.Count; i++)
            {
                if (!ring.Mounts[i].Occupied)
                {
                    InstallModule(ring.Mounts[i], kind);
                    return;
                }
            }
        }

        private bool InstallModule(OrbitalMountRuntime mount, OrbitalModuleKind kind)
        {
            if (mount == null || mount.Occupied)
                return false;
            return InstallModule(kind, mount.Ring.RingId, mount.MountIndex,
                out _);
        }

        public bool InstallModule(OrbitalModuleKind kind, int stableRingId,
            int mountIndex, out string error)
        {
            error = null;
            if (State == null)
            {
                error = "state is missing";
                return false;
            }
            OrbitalRingRuntime ring = rings.Find(value =>
                value.RingId == stableRingId);
            if (ring == null || mountIndex < 0 || mountIndex >= ring.Mounts.Count)
            {
                error = $"runtime mount {stableRingId}:{mountIndex} is missing";
                return false;
            }
            OrbitalMountRuntime mount = ring.Mounts[mountIndex];
            if (!State.InstallModule(kind, stableRingId, mountIndex,
                    out OrbitalModuleState moduleState))
            {
                error = $"state rejected module at {stableRingId}:{mountIndex}";
                return false;
            }
            if (InstallModulePresentation(mount, moduleState))
                return true;
            State.RemoveModule(moduleState.StableModuleId);
            RebuildRuntimeFromState(stableRingId);
            error = $"presentation rejected module at {stableRingId}:{mountIndex}";
            return false;
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
            error = null;
            OrbitalModuleState original = State?.Modules.Find(value =>
                value.StableModuleId == stableModuleId);
            if (original == null)
            {
                error = $"module {stableModuleId} does not exist";
                return false;
            }
            int sourceRingId = original.StableRingId;
            int sourceMountIndex = original.MountIndex;
            if (State == null || !State.MoveModule(stableModuleId,
                    targetRingId, targetMountIndex, out error))
            {
                return false;
            }
            if (RebuildRuntimeFromState(targetRingId))
                return true;
            State?.MoveModule(stableModuleId, sourceRingId, sourceMountIndex, out _);
            RebuildRuntimeFromState(sourceRingId);
            error = $"runtime attach failed at {targetRingId}:{targetMountIndex}; state rolled back";
            Debug.LogError($"[OrbitalStation] Relocation failed: {error}", this);
            return false;
        }

        public bool RemoveModule(int stableModuleId)
        {
            if (State == null || !State.RemoveModule(stableModuleId))
                return false;
            RebuildRuntimeFromState();
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
            if (State == null || !State.UpgradeModuleDamage(stableModuleId))
                return false;
            OrbitalModuleRuntime module = modules.Find(value =>
                value.StableModuleId == stableModuleId);
            module?.TriggerUpgradePresentation();
            FlashCore(new Color(1f, 0.82f, 0.28f));
            return true;
        }

        public bool SetModuleRewardPresentationVisible(int stableModuleId,
            bool visible)
        {
            if (State == null)
                return false;
            int index = State.Modules.FindIndex(value =>
                value.StableModuleId == stableModuleId);
            if (index < 0 || index >= modules.Count || modules[index] == null)
                return false;
            modules[index].SetRewardPresentationVisible(visible);
            return true;
        }

        public bool RemoveRing(int stableRingId, out string error)
        {
            error = null;
            if (State == null || !State.RemoveRing(stableRingId, out error))
                return false;
            RebuildRuntimeFromState();
            return true;
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
            if (runStateManager == null ||
                runStateManager.OrbitalStationState == null)
            {
                return false;
            }
            if (preferredRingId == 0 && selectedRing != null)
                preferredRingId = selectedRing.RingId;
            Teardown();
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
            OrbitalRingRuntime ring = new(ringState, runtimeRoot,
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
                Subject42DebugMenu.IsDebugMenuOpen)
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

        private static OrbitalMountRuntime FindMountAt(OrbitalRingRuntime ring,
            Vector2 world)
        {
            if (ring == null)
                return null;
            OrbitalMountRuntime best = null;
            float bestDistance = 0.35f * 0.35f;
            for (int i = 0; i < ring.Mounts.Count; i++)
            {
                OrbitalMountRuntime mount = ring.Mounts[i];
                if (mount.Occupied || mount.Transform == null)
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
            List<OrbitalLinkNodeModule> nodes = new();
            for (int i = 0; i < modules.Count; i++)
                if (modules[i] is OrbitalLinkNodeModule node && node.CurrentMount != null)
                    nodes.Add(node);
            for (int i = 0; i + 1 < nodes.Count; i += 2)
            {
                Vector2 from = nodes[i].CurrentMount.Transform.position;
                Vector2 to = nodes[i + 1].CurrentMount.Transform.position;
                FlashLink(from, to, new Color(0.7f, 0.2f, 1f, 0.35f),
                    Mathf.Max(0.03f, deltaTime * 1.5f));
                if (nodes[i].RuntimeCooldown > 0f)
                    continue;
                EnemyHealth target = Combat.FindNearest((from + to) * 0.5f, 2f);
                if (target != null && DistanceToSegment(target.transform.position,
                    from, to) < 0.4f)
                {
                    float linkPower = (nodes[i].CurrentMount.Ring.PowerMultiplier +
                        nodes[i + 1].CurrentMount.Ring.PowerMultiplier) * 0.5f;
                    float matrixPower = 1f + State.CoreState.LinkMatrixUpgradeLevel * 0.25f;
                    Combat.ApplyDamage(target, 5f * linkPower * matrixPower *
                        Core.DamageMultiplier, target.transform.position);
                    nodes[i].RuntimeCooldown = 0.55f;
                }
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

        private static Sprite CreateCircleSprite()
        {
            const int size = 64;
            Texture2D texture = new(size, size, TextureFormat.RGBA32, false)
            {
                name = "Orbital Station Circle Texture",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.DontSave
            };
            Color[] pixels = new Color[size * size];
            Vector2 center = Vector2.one * ((size - 1) * 0.5f);
            float radius = size * 0.48f;
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float alpha = Mathf.Clamp01(radius -
                        Vector2.Distance(new Vector2(x, y), center) + 0.75f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), size);
            sprite.name = "Orbital Station Circle";
            sprite.hideFlags = HideFlags.DontSave;
            return sprite;
        }

        private void OnDestroy()
        {
            Teardown();
        }
    }
}
