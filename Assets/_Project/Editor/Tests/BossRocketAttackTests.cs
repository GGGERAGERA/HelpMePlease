using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossRocketAttackTests
{
    const string BossPath = "Assets/_Project/prefabs/Enemies/p_Boss1.prefab";
    const string Output = "Artifacts/BossRocketSmoke";
    [Test]
    public void BossHasRocketAttackAndKeepsContactAndChase()
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Enemies/p_Boss1.prefab");
        Assert.That(prefab.GetComponent<EnemyChaseMovement>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<EnemyCollisionHandler>(), Is.Not.Null);
        Assert.That(prefab.GetComponent("BossRocketAttack"), Is.Not.Null,
            "Boss must have a configured rocket ability in addition to chase/contact.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    static IEnumerator Until(System.Func<bool> ready, float timeout = 8f,
        [System.Runtime.CompilerServices.CallerLineNumber] int sourceLine = 0)
    {
        float end = Time.time + timeout;
        while (!ready() && Time.time < end) yield return null;
        Assert.That(ready(), Is.True, $"Timed out waiting for boss stage at line {sourceLine}");
    }

    static IEnumerator Delay(float seconds)
    {
        float end = Time.time + seconds;
        while (Time.time < end) yield return null;
    }

    static GameObject FindFx(string prefix) => Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
        .FirstOrDefault(t => t.parent == null && t.name.StartsWith(prefix))?.gameObject;

    static void Capture(Camera camera, string name)
    {
        Directory.CreateDirectory(Output);
        var rt = RenderTexture.GetTemporary(960, 640, 24);
        var previous = RenderTexture.active;
        camera.targetTexture = rt;
        camera.Render();
        RenderTexture.active = rt;
        var texture = new Texture2D(960, 640, TextureFormat.RGB24, false);
        texture.ReadPixels(new Rect(0, 0, 960, 640), 0, 0);
        texture.Apply();
        File.WriteAllBytes(Output + "/" + name + ".png", texture.EncodeToPNG());
        camera.targetTexture = null;
        RenderTexture.active = previous;
        RenderTexture.ReleaseTemporary(rt);
        Object.Destroy(texture);
    }

    [UnityTest]
    public IEnumerator CycleLocksTargetHitsOnceAndCancelsPendingRockets()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return ExerciseCycle();
    }

    // Create captured locals after EnterPlayMode's domain reload.
    private static IEnumerator ExerciseCycle()
    {
        var flow = new GameObject("Run flow").AddComponent<RunFlowController>();
        var camera = new GameObject("Camera").AddComponent<Camera>();
        camera.gameObject.AddComponent<AudioListener>();
        camera.tag = "MainCamera";
        camera.orthographic = true;
        camera.orthographicSize = 9f;
        camera.transform.position = new Vector3(-3, 3, -10);
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(.12f, .14f, .17f);
        var playerObject = new GameObject("Player");
        playerObject.tag = "Player";
        var playerBody = playerObject.AddComponent<Rigidbody2D>();
        playerBody.gravityScale = 0;
        playerBody.constraints = RigidbodyConstraints2D.FreezeRotation;
        playerObject.AddComponent<CircleCollider2D>().radius = .25f;
        var child = new GameObject("Second player collider");
        child.transform.SetParent(playerObject.transform);
        child.AddComponent<CircleCollider2D>().radius = .2f;
        var player = playerObject.AddComponent<PlayerHealth>();
        var bossPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossPath);
        Assert.That(bossPrefab, Is.Not.Null, "Loaded boss prefab in Play Mode");
        var boss = Object.Instantiate(bossPrefab, new Vector3(-20, 0), Quaternion.identity);
        Assert.That(boss, Is.Not.Null, "Instantiated boss in Play Mode");
        var attack = boss.GetComponent<BossRocketAttack>();
        Assert.That(attack, Is.Not.Null, "Ability on instantiated boss");
        var chase = boss.GetComponent<EnemyChaseMovement>();
        var bossHealth = boss.GetComponent<EnemyHealth>();
        var healthSettings = new SerializedObject(bossHealth);
        healthSettings.FindProperty("lootPrefab").objectReferenceValue = null;
        healthSettings.ApplyModifiedPropertiesWithoutUndo();
        attack.ShotsPerBurst = 1; // This test isolates one pair's damage; burst counts are covered separately.
        attack.RocketFallDelayMin = attack.RocketFallDelayMax = 1.2f;
        attack.TargetSpreadRadius = .01f;
        attack.AttackCooldown = 1.5f; // Isolate damage checks from overlapping bursts.
        yield return Delay(.3f);
        Assert.That(boss.transform.position.x, Is.GreaterThan(-20f), "Chase still moves");
        yield return Until(() => attack.State == BossRocketAttack.AttackState.PreparingAttack);
        Assert.That(chase.enabled, Is.True);
        Assert.That(chase.IsAttackPaused, Is.True);
        yield return Delay(.04f);
        Vector3 stopped = boss.transform.position;
        var animator = boss.transform.Find("graphic").GetComponent<Animator>();
        yield return Until(() => animator.GetCurrentAnimatorStateInfo(1).IsName("animBossPrepareToShootIDle"));
        yield return Until(() => attack.PendingRocketCount > 0);
        Assert.That(Vector3.Distance(stopped, boss.transform.position), Is.LessThan(.06f));
        Assert.That(attack.PendingRocketCount, Is.EqualTo(2));
        attack.FireRocket();
        Assert.That(attack.PendingRocketCount, Is.EqualTo(2), "Repeated Animation Event ignored");
        var marker = FindFx("fx_BossTaget1");
        Assert.That(marker, Is.Not.Null);
        Vector3 target = marker.transform.position;
        var muzzles = boss.GetComponentsInChildren<ParticleSystem>().Where(p => p.name.Contains("RocketMuzzle")).ToArray();
        Assert.That(muzzles.Any(p => p.isPlaying), Is.True, "Existing muzzle plays on event");
        yield return Delay(.08f);
        Capture(camera, "01-launch-marker");
        playerBody.position = new Vector2(5, 0);
        yield return Delay(attack.RocketFallDelayMax + .1f);
        Assert.That(marker.transform.position, Is.EqualTo(target), "Target is fixed in world");
        var falling = FindFx("p_fxRocket1");
        Assert.That(falling, Is.Not.Null);
        Assert.That(falling.transform.position.x, Is.EqualTo(target.x).Within(.03f));
        Assert.That(falling.transform.position.y, Is.GreaterThan(target.y));
        Assert.That(marker.GetComponentsInChildren<ParticleSystem>().Any(p => p.particleCount > 0), Is.True);
        Capture(camera, "02-falling");
        yield return Until(() => !attack.IsBurstActive && attack.PendingRocketCount == 0);
        yield return null;
        Assert.That(player.CurrentHealth, Is.EqualTo(100), "Outside radius is safe");
        var explosion = FindFx("fx_BomberExplosion");
        Assert.That(explosion, Is.Not.Null);
        Assert.That(Vector3.Distance(explosion.transform.position, target), Is.LessThan(.03f));
        yield return Delay(.12f);
        Capture(camera, "03-impact");
        Assert.That(FindFx("fx_BossTaget1"), Is.Null);
        yield return Until(() => attack.State == BossRocketAttack.AttackState.Chasing);
        Assert.That(chase.enabled, Is.True, "Recovery restores chase");
        int hits = 0;
        player.DebugDamageApplied += _ => hits++;
        yield return Until(() => attack.PendingRocketCount > 0);
        yield return Until(() => !attack.IsBurstActive && attack.PendingRocketCount == 0);
        Assert.That(player.CurrentHealth, Is.EqualTo(75), "Inside radius receives configured damage");
        Assert.That(hits, Is.EqualTo(1), "Compound collider takes one hit");
        boss.GetComponent<Rigidbody2D>().position = playerBody.position + Vector2.left * 20;
        // Component disable must clean a fired sequence and restore movement.
        yield return Until(() => attack.PendingRocketCount > 0);
        attack.enabled = false;
        yield return null;
        Assert.That(chase.enabled, Is.True);
        Assert.That(FindFx("fx_BossTaget1"), Is.Null);
        Assert.That(FindFx("p_fxRocket1"), Is.Null);
        yield return Delay(2f);
        Assert.That(hits, Is.EqualTo(1), "Canceled sequence cannot hit later");
        attack.enabled = true;
        yield return Until(() => attack.PendingRocketCount > 0);
        bossHealth.TakeDamage(10000, boss.transform.position);
        yield return null;
        Assert.That(FindFx("fx_BossTaget1"), Is.Null);
        Assert.That(FindFx("p_fxRocket1"), Is.Null);
        yield return Delay(2f);
        Assert.That(hits, Is.EqualTo(1), "Dead boss cannot cause a pending impact");
        boss = Object.Instantiate(bossPrefab, new Vector3(-20, 0), Quaternion.identity);
        attack = boss.GetComponent<BossRocketAttack>();
        attack.enabled = false;
        attack.AttackCooldown = .25f;
        attack.enabled = true;
        yield return Until(() => attack.PendingRocketCount > 0);
        player.TakeDamage(10000, Vector2.zero);
        yield return null;
        yield return null;
        Assert.That(attack.PendingRocketCount, Is.Zero, "Player death cancels shots");
        Assert.That(FindFx("fx_BossTaget1"), Is.Null);
        Assert.That(FindFx("p_fxRocket1"), Is.Null);
        Object.Destroy(flow.gameObject);
        yield return null;
        player.SetRuntimeHealth(100, 100);
        yield return Until(() => attack.PendingRocketCount > 0);
        flow = new GameObject("Run flow").AddComponent<RunFlowController>();
        flow.StopRunGameplay();
        yield return null;
        yield return null;
        Assert.That(attack.PendingRocketCount, Is.Zero, "Stopped run cancels shots");
        Assert.That(FindFx("fx_BossTaget1"), Is.Null);
        Assert.That(FindFx("p_fxRocket1"), Is.Null);
        yield return Delay(2f);
        Assert.That(player.CurrentHealth, Is.EqualTo(100), "No damage after fight ends");
        File.WriteAllText(Output + "/checks.txt", "PASS real Play Mode: chase, stop, prepare/attack events, muzzle, locked target, vertical fall, impact position, inside/outside radius, one hit with compound colliders, repeated cycles, recovery, disable/boss death/player death/fight end cleanup.\n");
    }
}
