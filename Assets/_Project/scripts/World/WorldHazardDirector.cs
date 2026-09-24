using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public sealed class WorldHazardDirector : MonoBehaviour
{
    private readonly List<WorldHazardDefinition> definitions = new();
    private readonly List<IWorldHazardAttack> attacks = new();
    private readonly Collider2D[] overlaps = new Collider2D[24];
    private WorldHazardPreset preset;
    private RunFlowController flow;
    private CharacterSpawner spawner;
    private GameplayAreaService area;
    private PlayerHealth player;
    private int runId;
    private float timer;
    public int AttacksStarted { get; private set; }
    public Vector3 LastTarget { get; private set; }
    public bool HasPendingAttack => attacks.Exists(attack => attack.IsBusy);

    public void Initialize(WorldHazardPreset config, RunFlowController runFlow,
        CharacterSpawner characterSpawner, GameplayAreaService gameplayArea)
    {
        DisposeAttacks();
        preset = config;
        flow = runFlow;
        spawner = characterSpawner;
        area = gameplayArea;
        player = null;
        runId = RunStateManager.Instance != null ? RunStateManager.Instance.RunId : -1;
        AttacksStarted = 0;
        LastTarget = default;
        timer = preset != null ? preset.InitialDelay : 0f;
        if (preset == null || preset.Attacks == null) return;
        foreach (var definition in preset.Attacks)
        {
            if (definition == null || !definition.IsConfigured || definitions.Contains(definition)) continue;
            definitions.Add(definition);
            attacks.Add(definition.CreateAttack(this));
        }
    }

    private bool CombatAllowed() => isActiveAndEnabled && preset != null && flow != null &&
        flow.isActiveAndEnabled && flow.Phase == RunPhase.NormalSector &&
        RunStateManager.Instance != null && RunStateManager.Instance.IsActiveRun(runId) &&
        player != null && player.isActiveAndEnabled && !player.IsDead &&
        !TutorialController.IsActive && !SceneTransitionOverlay.IsTransitioning;

    private void Update()
    {
        var currentPlayer = spawner != null ? spawner.SpawnedPlayer : null;
        var health = currentPlayer != null ? currentPlayer.GetComponent<PlayerHealth>() : null;
        if (health != player) { Cancel(); player = health; }
        if (!CombatAllowed()) { Cancel(); return; }
        if (Time.timeScale == 0f || EnemyDebugAiFreeze.IsFrozen) return;
        foreach (var attack in attacks) attack.Tick(Time.deltaTime, CombatAllowed);
        if (!CombatAllowed()) { Cancel(); return; }
        // Cooldown starts after the previous strike, so warnings never overlap.
        if (HasPendingAttack) return;
        timer -= Time.deltaTime;
        if (timer > 0f || attacks.Count == 0) return;
        timer = preset.NextInterval();
        int first = Random.Range(0, attacks.Count);
        for (int i = 0; i < attacks.Count; i++)
        {
            int index = (first + i) % attacks.Count;
            if (!definitions[index].IsConfigured ||
                !TryPickTarget(definitions[index].DangerRadius, out Vector3 target)) continue;
            if (!attacks[index].TryStart(target)) continue;
            LastTarget = target;
            AttacksStarted++;
            break;
        }
    }

    private bool TryPickTarget(float radius, out Vector3 target)
    {
        target = default;
        var camera = Camera.main;
        if (area == null || camera == null || !camera.orthographic) return false;
        float playerRadius = 0f;
        foreach (var collider in player.GetComponentsInChildren<Collider2D>())
            if (collider.enabled) playerRadius = Mathf.Max(playerRadius,
                Vector2.Distance(player.transform.position, collider.bounds.center) +
                ((Vector2)collider.bounds.extents).magnitude);
        float minimum = Mathf.Max(preset.MinDistance, radius + playerRadius + preset.PlayerClearance);
        if (minimum > preset.MaxDistance) return false;
        for (int i = 0; i < 24; i++)
        {
            Vector2 direction = Random.insideUnitCircle.normalized;
            if (direction.sqrMagnitude < .5f) continue;
            Vector2 point = (Vector2)player.transform.position + direction * Random.Range(minimum, preset.MaxDistance);
            float margin = radius + preset.EdgeClearance;
            if (!area.IsInsidePlayableArea(point, margin)) continue;
            Vector3 viewport = camera.WorldToViewportPoint(point);
            float vertical = margin / (2f * camera.orthographicSize);
            float horizontal = vertical / camera.aspect;
            if (viewport.z <= 0f || viewport.x < horizontal || viewport.x > 1f - horizontal ||
                viewport.y < vertical || viewport.y > 1f - vertical) continue;
            var filter = ContactFilter2D.noFilter;
            filter.useTriggers = false;
            int count = Physics2D.OverlapCircle(point, margin, filter, overlaps);
            bool blocked = count == overlaps.Length;
            for (int j = 0; j < count && !blocked; j++)
            {
                var hit = overlaps[j];
                if (hit == area.PlayableArea || hit == area.SpawnArea ||
                    hit.GetComponentInParent<EnemyHealth>() != null) continue;
                blocked = true;
            }
            if (blocked) continue;
            target = point;
            return true;
        }
        return false; // No unsafe fallback when the player is cornered.
    }

    public void Cancel()
    {
        foreach (var attack in attacks) attack.Cancel();
        timer = preset != null ? preset.InitialDelay : 0f;
    }

    private void DisposeAttacks()
    {
        foreach (var attack in attacks) attack.Dispose();
        attacks.Clear();
        definitions.Clear();
    }

    private void OnDisable() => Cancel();
    private void OnDestroy() => DisposeAttacks();
}
