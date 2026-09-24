using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Shared fixed-target rocket flight, warning and impact. Owners supply timing and cancellation.</summary>
public sealed class RocketAttackRunner : IDisposable
{
    private sealed class Shot
    {
        public float age, delay, duration;
        public Vector3 target, launchStart, fallStart;
        public PooledGameObject marker, launch, falling;
    }

    private sealed class Impact
    {
        public PooledGameObject item;
        public float remaining;
    }

    private readonly SimplePrefabPool rockets, markers, explosions;
    private readonly ParticleSystem explosionPrefab;
    private readonly List<Shot> shots = new();
    private readonly List<Impact> impacts = new();
    private int generation;
    private bool disposed;
    public int PendingCount => shots.Count;
    public bool IsReady => !disposed && rockets != null && markers != null && explosions != null;

    public RocketAttackRunner(MonoBehaviour owner, GameObject rocketPrefab,
        GameObject targetPrefab, ParticleSystem explosionPrefab)
    {
        this.explosionPrefab = explosionPrefab;
        if (rocketPrefab == null || targetPrefab == null || explosionPrefab == null) return;
        rockets = new SimplePrefabPool(owner, rocketPrefab, 0, 24);
        markers = new SimplePrefabPool(owner, targetPrefab, 0, 12);
        explosions = new SimplePrefabPool(owner, explosionPrefab.gameObject, 0, 12);
    }

    public bool Launch(Vector3 target, Vector3? origin, float delay, float duration, float radius)
    {
        if (!IsReady) return false;
        var shot = new Shot { target = target, delay = Mathf.Max(.01f, delay),
            duration = Mathf.Max(.05f, duration), launchStart = origin ?? target };
        shot.marker = markers.Get(target, Quaternion.identity);
        ExplosionWarningVisual.Configure(shot.marker.gameObject, radius, shot.delay + shot.duration);
        foreach (var ps in shot.marker.GetComponentsInChildren<ParticleSystem>(true))
        {
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.startLifetime = main.duration + .1f;
            main.startSpeed = 0f;
            ps.Play(false);
        }
        if (origin.HasValue) shot.launch = SpawnRocket(origin.Value, false);
        shots.Add(shot);
        return true;
    }

    private PooledGameObject SpawnRocket(Vector3 position, bool falling)
    {
        var rocket = rockets.Get(position, Quaternion.identity);
        var ps = rocket.PrimaryParticleSystem;
        ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startSpeed = 0f;
        main.startLifetime = 30f;
        main.startRotation = falling ? -Mathf.PI * .5f : Mathf.PI * .5f;
        var emission = ps.emission;
        emission.enabled = false;
        // Prepare clears all children; restart the authored smoke on pooled reuse too.
        ps.Play(true);
        ps.Emit(1);
        return rocket;
    }

    public void Tick(float deltaTime, float spawnHeight, float radius, int damage,
        Func<bool> combatAllowed, bool nonLethal = false)
    {
        if (disposed) return;
        if (!combatAllowed()) { Cancel(); return; }
        for (int i = impacts.Count - 1; i >= 0; i--)
        {
            var impact = impacts[i];
            impact.remaining -= deltaTime;
            if (impact.remaining > 0f) continue;
            Release(impact.item);
            impacts.RemoveAt(i);
        }
        for (int i = shots.Count - 1; i >= 0; i--)
        {
            var shot = shots[i];
            shot.age += deltaTime;
            float launchDuration = Mathf.Min(.35f, shot.delay);
            if (shot.launch != null)
            {
                shot.launch.transform.position = shot.launchStart + Vector3.up *
                    Mathf.Max(1f, spawnHeight) * Mathf.Clamp01(shot.age / launchDuration);
                if (shot.age >= launchDuration) { Release(shot.launch); shot.launch = null; }
            }
            if (shot.age < shot.delay) continue;
            if (shot.falling == null)
            {
                float spawnY = shot.target.y + Mathf.Max(1f, spawnHeight);
                var camera = Camera.main;
                if (camera != null && camera.orthographic)
                    spawnY = Mathf.Max(spawnY, camera.transform.position.y + camera.orthographicSize + 2f);
                shot.fallStart = new Vector3(shot.target.x, spawnY, shot.target.z);
                shot.falling = SpawnRocket(shot.fallStart, true);
            }
            float progress = Mathf.Clamp01((shot.age - shot.delay) / shot.duration);
            shot.falling.transform.position = Vector3.Lerp(shot.fallStart, shot.target, progress);
            if (progress < 1f) continue;
            shots.RemoveAt(i); // Damage callbacks may cancel/restart this owner.
            ReleaseShot(shot);
            if (!combatAllowed()) { Cancel(); return; }
            int beforeImpact = generation;
            var fx = EnemyExplosion.Detonate(shot.target, radius, damage, explosionPrefab, explosions, nonLethal);
            var item = fx != null ? fx.GetComponent<PooledGameObject>() : null;
            if (generation != beforeImpact || !combatAllowed())
            {
                Release(item);
                Cancel();
                return;
            }
            if (item != null) impacts.Add(new Impact { item = item, remaining = fx.main.duration });
        }
    }

    private static void Release(PooledGameObject item)
    {
        if (item == null) return;
        foreach (var ps in item.GetComponentsInChildren<ParticleSystem>(true))
            ps.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
        item.Release();
    }

    private static void ReleaseShot(Shot shot)
    {
        Release(shot.marker);
        Release(shot.launch);
        Release(shot.falling);
    }

    public void Cancel()
    {
        generation++;
        foreach (var shot in shots) ReleaseShot(shot);
        shots.Clear();
        foreach (var impact in impacts) Release(impact.item);
        impacts.Clear();
    }

    public void Dispose()
    {
        if (disposed) return;
        Cancel();
        disposed = true;
        rockets?.Dispose();
        markers?.Dispose();
        explosions?.Dispose();
    }
}
