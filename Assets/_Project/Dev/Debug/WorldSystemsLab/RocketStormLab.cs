#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using UnityEngine;

// Lab-only wave scheduling. Flight, warnings, damage and pooled FX belong to RocketAttackRunner.
public sealed class RocketStormLab : MonoBehaviour
{
    [Serializable]
    public sealed class Settings
    {
        public Vector2 zoneSize = new(24f, 20f);
        [Min(1f)] public float duration = 30f;
        [Min(.5f)] public float warningTime = 2f;
        [Min(.5f)] public float waveInterval = 3.5f;
        [Range(.5f, 1.5f), Tooltip("1 = overlapping circles covering the danger area.")]
        public float rocketDensity = 1f;
        public bool enemyPressure;
    }

    private const float Radius = 2f, FallTime = .4f, Preparation = 2f;
    private WorldSystemsLabController lab;
    private Settings settings;
    private PlayerHealth health;
    private CharacterMovement2D movement;
    private RocketAttackRunner runner;
    private LineRenderer boundary;
    private Material boundaryMaterial;
    private Vector2 center, halfSize;
    private float elapsed, nextWave, finishAt, signalUntil, clearance;
    private int wave;
    private bool pressure;
    private Vector2 reachableSafe;
    public bool IsRunning { get; private set; }
    public int PendingRockets => runner?.PendingCount ?? 0;
    public int WavesLaunched => wave;
    public string Status { get; private set; } = "30 seconds inside the marked zone";

    public void Initialize(WorldSystemsLabController owner, Settings tuning,
        GameObject rocket, GameObject marker, ParticleSystem explosion)
    {
        lab = owner;
        settings = tuning;
        health = lab.Player.GetComponent<PlayerHealth>();
        movement = lab.Player.GetComponent<CharacterMovement2D>();
        runner = new RocketAttackRunner(this, rocket, marker, explosion);
        var root = new GameObject("Rocket Storm boundary");
        root.transform.SetParent(transform, false);
        boundary = root.AddComponent<LineRenderer>();
        boundaryMaterial = new Material(Shader.Find("Sprites/Default"));
        boundary.sharedMaterial = boundaryMaterial;
        boundary.loop = true;
        boundary.useWorldSpace = true;
        boundary.positionCount = 4;
        boundary.widthMultiplier = .16f;
        boundary.sortingLayerName = "Midground";
        boundary.sortingOrder = 100;
        boundary.enabled = false;
    }

    public void StartStorm()
    {
        if (!runner.IsReady) { Status = "Missing rocket assets"; return; }
        if (!lab.PrepareRocketStorm())
        { Status = "Lab combat bootstrap failed; see Console"; return; }
        health.SetRuntimeHealth(health.MaxHealth, health.MaxHealth);
        movement.enabled = true;
        lab.TeleportPlayerCenter();
        center = lab.Player.position;
        halfSize = new Vector2(Mathf.Max(12f, settings.zoneSize.x), Mathf.Max(12f, settings.zoneSize.y)) * .5f;
        clearance = .5f;
        foreach (var collider in lab.Player.GetComponentsInChildren<Collider2D>())
            if (!collider.isTrigger) clearance = Mathf.Max(clearance, ((Vector2)collider.bounds.extents).magnitude);
        elapsed = 0f;
        nextWave = Preparation;
        finishAt = Mathf.Max(Preparation + Mathf.Max(.5f, settings.warningTime), settings.duration);
        wave = 0;
        signalUntil = 0f;
        IsRunning = true;
        boundary.SetPositions(new[] {
            (Vector3)(center + new Vector2(-halfSize.x, -halfSize.y)),
            (Vector3)(center + new Vector2(halfSize.x, -halfSize.y)),
            (Vector3)(center + new Vector2(halfSize.x, halfSize.y)),
            (Vector3)(center + new Vector2(-halfSize.x, halfSize.y)) });
        boundary.startColor = boundary.endColor = new Color(.2f, 1f, 1f);
        boundary.enabled = true;
        pressure = settings.enemyPressure;
        lab.SetRocketStormPressure(pressure);
        Status = "Prepare...";
    }

    public void SetEnemyPressure(bool value)
    {
        if (!IsRunning || pressure == value) return;
        pressure = value;
        lab.SetRocketStormPressure(value);
    }

    private bool AttemptAllowed()
    {
        Vector2 p = (Vector2)lab.Player.position - center;
        return IsRunning && !health.IsDead && health.isActiveAndEnabled &&
            Mathf.Abs(p.x) <= halfSize.x && Mathf.Abs(p.y) <= halfSize.y;
    }

    private void Update()
    {
        if (!IsRunning)
        {
            if (signalUntil > 0f && Time.time >= signalUntil)
            { boundary.enabled = false; signalUntil = 0f; }
            return;
        }
        if (!AttemptAllowed())
        {
            bool dead = health.IsDead;
            Stop();
            Status = dead ? "Failed: player died" : "Cancelled: left zone";
            return;
        }
        elapsed += Time.deltaTime;
        runner.Tick(Time.deltaTime, 16f, Radius, 25, AttemptAllowed);
        if (!AttemptAllowed()) return; // Impact can kill the player; cancellation happens next frame.
        float warning = Mathf.Max(.5f, settings.warningTime);
        if (elapsed >= nextWave && nextWave + warning <= finishAt + .01f)
        {
            LaunchWave(warning);
            nextWave = elapsed + Mathf.Max(warning + .3f, settings.waveInterval);
        }
        if (elapsed >= finishAt && runner.PendingCount == 0)
        {
            Stop();
            Status = "SUCCESS: survived Rocket Storm";
            boundary.startColor = boundary.endColor = Color.green;
            boundary.enabled = true;
            signalUntil = Time.time + 1.2f;
        }
    }

    private void LaunchWave(float warning)
    {
        int pattern = wave % 6;
        float safeRadius = 1.8f;
        Vector2 player = (Vector2)lab.Player.position - center;
        Vector2 desired;
        if (pattern < 3)
            desired = new Vector2((pattern - 1) * halfSize.x * .5f, player.y);
        else if (pattern == 3)
            desired = Vector2.Dot(player, new Vector2(halfSize.x, halfSize.y)) < 0f
                ? -halfSize * .5f : halfSize * .5f;
        else if (pattern == 4) desired = Vector2.zero;
        else desired = new Vector2(player.x < 0 ? -halfSize.x + safeRadius : halfSize.x - safeRadius, player.y);
        desired.x = Mathf.Clamp(desired.x, -halfSize.x + safeRadius, halfSize.x - safeRadius);
        desired.y = Mathf.Clamp(desired.y, -halfSize.y + safeRadius, halfSize.y - safeRadius);
        // Conservative walk budget leaves time for acceleration/reaction and does not require Shift.
        reachableSafe = Vector2.MoveTowards(player, desired,
            Mathf.Max(0f, movement.speed) * warning * .5f);
        float exclusion = safeRadius + Radius + clearance;
        float spacing = Radius * 1.3f / Mathf.Clamp(settings.rocketDensity, .5f, 1.5f);
        int columns = Mathf.CeilToInt(halfSize.x * 2f / spacing);
        int rows = Mathf.CeilToInt(halfSize.y * 2f / spacing);
        for (int x = 0; x <= columns; x++)
        for (int y = 0; y <= rows; y++)
        {
            Vector2 p = new(-halfSize.x + x * halfSize.x * 2f / columns,
                -halfSize.y + y * halfSize.y * 2f / rows);
            if (Vector2.Distance(p, reachableSafe) <= exclusion) continue;
            if (pattern < 3 && Mathf.Abs(p.x - desired.x) <= safeRadius + Radius + clearance) continue;
            if (pattern == 3 && IsIsland(p, exclusion)) continue;
            if (pattern == 4 && p.magnitude <= exclusion) continue;
            if (pattern == 5 && Mathf.Abs(p.x) >= halfSize.x - exclusion) continue;
            runner.Launch(center + p, null, Mathf.Max(.1f, warning - FallTime), FallTime, Radius);
        }
        wave++;
        Status = $"{Mathf.Max(0f, finishAt - elapsed):F0}s / wave {wave}: " +
            (pattern < 3 ? "Strip" : pattern == 3 ? "Islands" : pattern == 4 ? "Edges" : "Centre");
    }

    private bool IsIsland(Vector2 p, float exclusion)
    {
        for (int sign = -1; sign <= 1; sign += 2)
            if (Vector2.Distance(p, halfSize * (sign * .5f)) <= exclusion) return true;
        return false;
    }

    public void Stop()
    {
        IsRunning = false;
        runner?.Cancel();
        if (boundary != null) boundary.enabled = false;
        signalUntil = 0f;
        if (lab != null) lab.SetRocketStormPressure(false);
        Status = "Stopped / start again";
    }
    private void OnDisable() => Stop();
    private void OnDestroy()
    {
        runner?.Dispose();
        if (boundaryMaterial != null) Destroy(boundaryMaterial);
    }
}
#endif
