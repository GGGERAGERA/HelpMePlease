using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class BossBurstTests
{
    const string Path = "Assets/_Project/prefabs/Enemies/p_Boss1.prefab";
    const string Output = "Artifacts/BossBurst";
    static Camera testCamera;
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
    static IEnumerator Wait(Func<bool> ready, float timeout = 15f)
    {
        float end = Time.time + timeout;
        while (!ready() && Time.time < end) yield return null;
        Assert.That(ready(), Is.True, "Timed out waiting for boss");
    }
    static IEnumerator Delay(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }
    static BossRocketAttack Spawn()
    {
        testCamera = new GameObject("Boss burst test camera").AddComponent<Camera>();
        testCamera.gameObject.AddComponent<AudioListener>();
        testCamera.orthographic = true;
        // Frame the boss, moving target and upward launches in Game View.
        testCamera.orthographicSize = Mathf.Max(15f, 23f / Mathf.Max(.1f, testCamera.aspect));
        testCamera.transform.position = new Vector3(15, 5, -10);
        testCamera.clearFlags = CameraClearFlags.SolidColor;
        testCamera.backgroundColor = new Color(.12f, .14f, .17f);
        new GameObject("Run flow").AddComponent<RunFlowController>();
        var player = new GameObject("Player");
        player.tag = "Player";
        player.transform.position = Vector3.right * 15;
        player.AddComponent<PlayerHealth>();
        var boss = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Path));
        var health = new SerializedObject(boss.GetComponent<EnemyHealth>());
        health.FindProperty("lootPrefab").objectReferenceValue = null;
        health.FindProperty("damagePopupPrefab").objectReferenceValue = null;
        health.ApplyModifiedPropertiesWithoutUndo();
        var attack = boss.GetComponent<BossRocketAttack>();
        attack.enabled = false;
        attack.AttackCooldown = 1f;
        attack.enabled = true;
        return attack;
    }
    static Transform[] Fx(string prefix) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
        .Where(t => t.parent == null && t.name.StartsWith(prefix)).ToArray();

    [UnityTest] public IEnumerator OneEventLaunchesBothHands()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseDual();
    }
    static IEnumerator ExerciseDual()
    {
        var attack = Spawn();
        yield return Wait(() => attack.PendingRocketCount > 0);
        Assert.That(attack.PendingRocketCount, Is.EqualTo(2), "One FireRocket must launch LH and RH");
        Assert.That(Fx("p_fxRocket1").Length, Is.EqualTo(2));
        Assert.That(Fx("fx_BossTaget1").Length, Is.EqualTo(2));
        var muzzles = attack.GetComponentsInChildren<ParticleSystem>().Where(p => p.name.Contains("RocketMuzzle")).ToArray();
        Assert.That(muzzles.Length, Is.EqualTo(2));
        Assert.That(muzzles.All(p => p.isPlaying), Is.True);
        var origins = Fx("p_fxRocket1").Select(t => t.position).ToArray();
        foreach (var muzzle in muzzles)
            Assert.That(origins.Any(p => Vector3.Distance(p, muzzle.transform.position) < .2f), Is.True,
                "Each hand must own a separate upward launch");
        CaptureDualLaunch();
        attack.FireRocket();
        Assert.That(attack.PendingRocketCount, Is.EqualTo(2), "Duplicate event must not launch another pair");
    }

    static void CaptureDualLaunch()
    {
        var camera = testCamera;
        var rt = RenderTexture.GetTemporary(1200, 800, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var image = new Texture2D(1200, 800, TextureFormat.RGB24, false);
        image.ReadPixels(new Rect(0, 0, 1200, 800), 0, 0);
        image.Apply();
        Directory.CreateDirectory(Output);
        File.WriteAllBytes(Output + "/dual-launch.png", image.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(image);
    }

    [UnityTest] public IEnumerator MovementUsesWalkSprites()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseWalk();
    }
    static IEnumerator ExerciseWalk()
    {
        var attack = Spawn();
        attack.enabled = false;
        var animator = attack.transform.Find("graphic").GetComponent<Animator>();
        var foot = attack.transform.Find("graphic/Boss2Body1/Boss2Foot").GetComponent<SpriteRenderer>();
        var sprites = new HashSet<Sprite>();
        float start = Time.time;
        while (Time.time - start < 2f)
        {
            if (Time.time - start > .2f)
            {
                Assert.That(animator.GetFloat("Speed"), Is.GreaterThan(.01f));
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("WalkLeft"), Is.True);
                sprites.Add(foot.sprite);
            }
            yield return null;
        }
        Assert.That(sprites.Count, Is.GreaterThanOrEqualTo(4));
    }

    [UnityTest] public IEnumerator BurstsOneThreeFiveKeepIdleAndScheduleIndependentRockets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseBursts();
    }
    static IEnumerator ExerciseBursts()
    {
        var attack = Spawn();
        var chase = attack.GetComponent<EnemyChaseMovement>();
        var animator = attack.transform.Find("graphic").GetComponent<Animator>();
        var rb = attack.GetComponent<Rigidbody2D>();
        var player = GameObject.FindGameObjectWithTag("Player").transform;
        var report = new System.Text.StringBuilder();
        Directory.CreateDirectory(Output);
        foreach (int count in new[] { 1, 3, 5 })
        {
            attack.enabled = false;
            attack.ShotsPerBurst = count;
            attack.RocketFallDelayMin = .4f;
            attack.RocketFallDelayMax = 2f;
            attack.AttackCooldown = .5f;
            attack.DelayBetweenShots = .1f;
            attack.enabled = true;
            player.position = rb.position + Vector2.right * 15f;
            yield return Wait(() => attack.IsBurstActive);
            attack.AttackCooldown = 30f; // Isolate one burst, allowing its pending rockets to finish.
            yield return Delay(.1f);
            Vector2 stopped = rb.position;
            var seen = new HashSet<object>();
            var targets = new Dictionary<Transform, Vector3>();
            var delays = new List<float>();
            var fallingInstances = new HashSet<int>();
            int cycles = 0;
            bool wasShoot = false;
            float deadline = Time.time + 20f;
            var shotField = typeof(BossRocketAttack).GetField("shots", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            void ObserveFalling()
            {
                foreach (object shot in (IEnumerable)shotField.GetValue(attack))
                {
                    var falling = (GameObject)shot.GetType().GetField("falling").GetValue(shot);
                    if (falling != null) fallingInstances.Add(falling.GetInstanceID());
                }
            }
            while (attack.IsBurstActive && Time.time < deadline)
            {
                ObserveFalling();
                bool shooting = animator.GetCurrentAnimatorStateInfo(1).IsName("animBossShoot1") ||
                    (animator.IsInTransition(1) && animator.GetNextAnimatorStateInfo(1).IsName("animBossShoot1"));
                if (shooting && !wasShoot) cycles++;
                wasShoot = shooting;
                Assert.That(chase.enabled && chase.IsAttackPaused, Is.True);
                Assert.That(Vector2.Distance(rb.position, stopped), Is.LessThan(.02f));
                Assert.That(animator.GetFloat("Speed"), Is.Zero);
                Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("animBossIdle1"), Is.True, "No Walk between shots");
                foreach (object shot in (IEnumerable)shotField.GetValue(attack))
                {
                    if (!seen.Add(shot)) continue;
                    var type = shot.GetType();
                    float delay = (float)type.GetField("delay").GetValue(shot);
                    Assert.That(delay, Is.InRange(.4f, 2f));
                    delays.Add(delay);
                    var marker = (GameObject)type.GetField("marker").GetValue(shot);
                    var launch = (GameObject)type.GetField("launch").GetValue(shot);
                    Assert.That(launch, Is.Not.Null);
                    Assert.That(marker, Is.Not.Null);
                    targets.Add(marker.transform, marker.transform.position);
                }
                foreach (var target in targets)
                    if (target.Key != null) Assert.That(target.Key.position, Is.EqualTo(target.Value));
                player.position += Vector3.right * Time.deltaTime; // Markers must stay fixed as player moves.
                yield return null;
            }
            Assert.That(attack.IsBurstActive, Is.False, "Burst must finish");
            Assert.That(cycles, Is.EqualTo(count));
            Assert.That(seen.Count, Is.EqualTo(count * 2));
            Assert.That(delays.Distinct().Count(), Is.EqualTo(count * 2));
            var points = targets.Values.ToArray();
            for (int i = 0; i < points.Length; i += 2)
                Assert.That(Vector3.Distance(points[i], points[i + 1]), Is.GreaterThan(.01f));
            Assert.That(chase.IsAttackPaused, Is.False);
            Assert.That(attack.PendingRocketCount, Is.GreaterThan(0), "Release before all rockets land");
            float movingStart = Time.time;
            while (Time.time - movingStart < .2f) { ObserveFalling(); yield return null; }
            Assert.That(rb.position.x, Is.GreaterThan(stopped.x + .1f));
            Assert.That(animator.GetCurrentAnimatorStateInfo(0).IsName("WalkLeft"), Is.True);
            while (attack.PendingRocketCount > 0 && Time.time < deadline) { ObserveFalling(); yield return null; }
            Assert.That(attack.PendingRocketCount, Is.Zero);
            Assert.That(fallingInstances.Count, Is.EqualTo(count * 2), "Every scheduled rocket actually falls");
            report.AppendLine($"PASS ShotsPerBurst={count}: {cycles} Attack cycles, {seen.Count} launches, {fallingInstances.Count} falling instances; stationary Idle throughout burst; Walk resumes before last impact. Delays: {string.Join(", ", delays.Select(d => d.ToString("F3", System.Globalization.CultureInfo.InvariantCulture)))}");
        }
        File.WriteAllText(Output + "/bursts.txt", report.ToString());
    }

    [UnityTest] public IEnumerator DeathDisableAndMissingEventReleaseBurst()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseCleanup();
    }
    static IEnumerator ExerciseCleanup()
    {
        var attack = Spawn();
        attack.ShotsPerBurst = 5;
        var chase = attack.GetComponent<EnemyChaseMovement>();
        var animator = attack.transform.Find("graphic").GetComponent<Animator>();
        foreach (string stage in new[] { "event missing", "disable", "death" })
        {
            yield return Wait(() => attack.IsBurstActive);
            if (stage == "event missing")
            {
                animator.speed = 0f;
                yield return Wait(() => !chase.IsAttackPaused, 5f);
                animator.speed = 1f;
            }
            else
            {
                yield return Wait(() => attack.PendingRocketCount > 0);
                if (stage == "disable") attack.enabled = false;
                else attack.GetComponent<EnemyHealth>().TakeDamage(10000, attack.transform.position);
            }
            Assert.That(chase.IsAttackPaused, Is.False);
            yield return null;
            Assert.That(Fx("p_fxRocket1"), Is.Empty);
            Assert.That(Fx("fx_BossTaget1"), Is.Empty);
            if (stage == "disable") attack.enabled = true;
        }
        yield return Delay(3f);
        Assert.That(Fx("p_fxRocket1"), Is.Empty);
        Assert.That(Fx("fx_BossTaget1"), Is.Empty);
    }
}
