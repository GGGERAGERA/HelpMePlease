using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using Subject42.Combat.OrbitalStation;
using UnityEngine;
using Object = UnityEngine.Object;

public static class EnemyFeedbackSmoke
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    public static IEnumerator VerifyCurrentRun(string directory)
    {
        Directory.CreateDirectory(directory);
        var failures = new List<string>();
        var trace = new StringBuilder($"Editor={Application.isEditor} Development={Debug.isDebugBuild}\n");
        float deadline = Time.realtimeSinceStartup + 15f;
        while (EnemyHealth.ActiveInstances.Count < 6 && Time.realtimeSinceStartup < deadline) yield return null;
        Object.FindFirstObjectByType<RunFlowController>().StopRunGameplay();
        var crowd = EnemyHealth.ActiveInstances.Where(e => !e.IsBoss && e.GetComponent<EnemyWhiteFlash>() != null)
            .Take(6).ToArray();
        if (crowd.Length < 3) throw new InvalidOperationException("Expected a real production enemy group.");
        var player = Object.FindFirstObjectByType<CharacterSpawner>().SpawnedPlayer;
        player.GetComponent<PlayerHealth>().SetRuntimeHealth(10000f, 10000f);
        player.GetComponent<Rigidbody2D>().constraints = RigidbodyConstraints2D.FreezeAll;
        var station = player.GetComponentInChildren<OrbitalStationRuntime>();
        station.InputOwner.enabled = false;
        station.enabled = false;
        ((ProductionOrbitalCombatAdapter)station.Combat).DebugClearProjectiles();
        foreach (var enemy in EnemyHealth.ActiveInstances.ToArray())
        {
            if (!crowd.Contains(enemy)) { Object.Destroy(enemy.gameObject); continue; }
            foreach (var behaviour in enemy.GetComponents<MonoBehaviour>())
                if (behaviour is not EnemyHealth && behaviour is not EnemyWhiteFlash && behaviour is not EnemyHealthPresentation)
                    behaviour.enabled = false;
            foreach (var animator in enemy.GetComponentsInChildren<Animator>(true)) animator.enabled = false;
            int index = Array.IndexOf(crowd, enemy);
            Vector2 position = (Vector2)player.transform.position + new Vector2(2.4f + index % 3 * .85f, (index / 3 - .5f) * 1.5f);
            enemy.transform.position = position;
            var rb = enemy.GetComponent<Rigidbody2D>();
            if (rb != null) { rb.constraints = RigidbodyConstraints2D.FreezeAll; rb.position = position; }
            enemy.SetRuntimeMaxHealth(1000f);
        }
        foreach (var projectile in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None))
        {
            if (projectile.TryGetComponent<PooledGameObject>(out var pooled)) pooled.Release();
            else Object.Destroy(projectile.gameObject);
        }
        Physics2D.SyncTransforms();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        follow.IsPresentationSuspended = true;
        var camera = follow.ControlledCamera;
        follow.transform.position += new Vector3(player.transform.position.x + 1.6f, player.transform.position.y + 1f,
            camera.transform.position.z) - camera.transform.position;
        camera.orthographicSize = 5.5f;
        yield return WaitReal(.4f); // Let any earlier player-hit presentation recover.
        Check(Screen.width == 1600 && Screen.height == 900, "Expected 1600x900 Game View.");
        var target = crowd[0];
        var flash = target.GetComponent<EnemyWhiteFlash>();
        var body = flash.TargetRenderer;
        var baseline = body.sharedMaterial;
        var presentation = target.GetComponent<EnemyHealthPresentation>();
        var hitPrefab = presentation.BloodHitPrefab.GetComponent<ParticleSystem>();
        var deathPrefab = presentation.DeathFxPrefab.GetComponent<ParticleSystem>();
        float authoredHitDuration = hitPrefab.main.duration, authoredDeathDuration = deathPrefab.main.duration;
        float authoredHitSize = hitPrefab.main.startSizeMultiplier, authoredDeathSize = deathPrefab.main.startSizeMultiplier;
        var collider = target.GetComponent<Collider2D>();
        Bounds colliderBounds = collider != null ? collider.bounds : default;
        Vector3 scale = body.transform.localScale;
        var properties = new MaterialPropertyBlock();
        body.GetPropertyBlock(properties);
        properties.SetFloat("_FeedbackQASentinel", 123f);
        body.SetPropertyBlock(properties);
        yield return Capture(directory, "01-crowd-before-hit");
        target.TakeDamage(8f, target.transform.position);
        Check(target.CurrentHealth == 992f, "Presentation changed ordinary damage.");
        body.GetPropertyBlock(properties);
        float blend = properties.GetFloat("_FlashBlend");
        Check(body.sharedMaterial.HasProperty("_FlashBlend") && blend >= .3f && blend <= .75f,
            "Enemy flash must retain authored texture/color rather than a solid white silhouette.");
        Check(properties.GetFloat("_FeedbackQASentinel") == 123f, "Flash erased an existing renderer property.");
        float duration = (float)typeof(EnemyWhiteFlash).GetField("flashDuration", Private).GetValue(flash);
        Check(duration <= .12f, "Common enemy flash must not hide details for .2 seconds.");
        var hit = FindEffect(hitPrefab.name, target.transform.position);
        Check(hit != null && hit.main.duration <= .25f && hit.main.startLifetime.constantMax <= .2f,
            "The existing hit sprite must be brief and return promptly to its pool.");
        trace.AppendLine($"Target={target.name} hit 1000->992 blend={blend:F3} flash={duration:F3}; crowd={crowd.Length}");
        yield return Capture(directory, "02-enemy-hit");
        body.GetPropertyBlock(properties);
        properties.SetFloat("_FeedbackQASentinel", 456f);
        body.SetPropertyBlock(properties);
        Time.timeScale = 0f;
        yield return WaitReal(.18f);
        Check(body.sharedMaterial == baseline, "Paused UI must not retain a flashed enemy.");
        body.GetPropertyBlock(properties);
        Check(properties.GetFloat("_FlashBlend") == 1f && properties.GetFloat("_FeedbackQASentinel") == 456f,
            "Flash recovery must reset its own property while preserving concurrent renderer changes.");
        Check(body.transform.localScale == scale && (collider == null || collider.bounds == colliderBounds),
            "Visual feedback must not change the enemy's physical silhouette.");
        yield return Capture(directory, "03-enemy-recovered-paused");
        flash.Flash();
        target.gameObject.SetActive(false);
        Check(body.sharedMaterial == baseline, "Interrupted flash must restore before actor reuse.");
        target.gameObject.SetActive(true);
        target.SetRuntimeMaxHealth(1000f);
        Time.timeScale = 1f;
        yield return WaitGame(.27f);
        Check(hit == null || !hit.gameObject.activeInHierarchy, "Hit FX did not return to the pool.");
        target.TakeDamage(8f, target.transform.position, true);
        var critical = FindEffect(hitPrefab.name, target.transform.position);
        Check(critical != null && Mathf.Approximately(critical.main.startSizeMultiplier, authoredHitSize * .75f * 1.4f),
            "Existing critical emphasis must remain.");
        yield return WaitGame(.27f);
        target.TakeDamage(8f, target.transform.position);
        var reused = FindEffect(hitPrefab.name, target.transform.position);
        Check(reused != null && Mathf.Approximately(reused.main.startSizeMultiplier, authoredHitSize * .75f),
            "Pooled hit size must reset after a critical hit.");
        Check(target.CurrentHealth == 984f, "Critical/ordinary presentation must preserve supplied damage.");
        yield return WaitGame(.27f);
        Vector3 deathPosition = target.transform.position;
        target.TakeDamage(2000f, deathPosition);
        var death = FindEffect(deathPrefab.name, deathPosition);
        Check(death != null && death.main.duration <= .45f && death.main.startLifetime.constantMax <= .35f &&
            death.main.startSizeMultiplier >= 1.3f, "Existing common death puff must be distinct, compact in time, and bounded.");
        trace.AppendLine($"Owned hit FX={hitPrefab.name} lifetime={reused.main.startLifetime.constantMax:F3} duration={reused.main.duration:F3}; death={deathPrefab.name} lifetime={death.main.startLifetime.constantMax:F3} size={death.main.startSizeMultiplier:F3} duration={death.main.duration:F3}");
        yield return Capture(directory, "04-enemy-death");
        Check(target == null, "Lethal damage must still remove the actor immediately.");
        Check(death != null && death.gameObject.activeInHierarchy && death.transform.parent == null,
            "Existing pooled death FX must outlive its destroyed actor.");
        yield return WaitGame(.12f);
        yield return Capture(directory, "05-death-dissipating");
        yield return WaitGame(.38f);
        Check(death == null || !death.gameObject.activeInHierarchy, "Death FX must return to its existing pool without a lingering cloud.");
        yield return Capture(directory, "06-crowd-after-death");
        Check(hitPrefab.main.duration == authoredHitDuration && deathPrefab.main.duration == authoredDeathDuration &&
            hitPrefab.main.startSizeMultiplier == authoredHitSize && deathPrefab.main.startSizeMultiplier == authoredDeathSize,
            "Shared source FX assets used by props/minigames must remain unchanged.");
        trace.AppendLine(failures.Count == 0 ? "PASS" : string.Join("\n", failures));
        File.WriteAllText(directory + "/runtime-checks.txt", trace.ToString());
        if (failures.Count > 0) throw new InvalidOperationException(string.Join("\n", failures));
        void Check(bool value, string message) { if (!value) failures.Add(message); }
    }

    private static ParticleSystem FindEffect(string name, Vector3 position) =>
        Object.FindObjectsByType<PooledGameObject>(FindObjectsSortMode.None)
            .Where(p => p.PrimaryParticleSystem != null && p.name.StartsWith(name + "(Clone)") &&
                Vector2.Distance(p.transform.position, position) < .15f)
            .Select(p => p.PrimaryParticleSystem).FirstOrDefault();
    private static IEnumerator WaitReal(float duration)
    { float end = Time.realtimeSinceStartup + duration; while (Time.realtimeSinceStartup < end) yield return null; }
    private static IEnumerator WaitGame(float duration)
    { float end = Time.time + duration; while (Time.time < end) yield return null; }
    private static IEnumerator Capture(string directory, string name)
    {
        string path = Path.GetFullPath(Path.Combine(directory, name + ".png"));
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        float end = Time.realtimeSinceStartup + 5f;
        while (!File.Exists(path) && Time.realtimeSinceStartup < end) yield return null;
        if (!File.Exists(path)) throw new IOException("Missing Game View capture: " + path);
    }
}
