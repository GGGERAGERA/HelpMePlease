using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class BossPracticeTests
{
    [UnityTearDown] public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }

    [UnityTest] public IEnumerator AuthoredTuningAndPlayableArena()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        BossPracticeAuthoring.Create();
        EditorSceneManager.OpenScene(BossPracticeAuthoring.ScenePath);
        yield return new EnterPlayMode();
        yield return Exercise();
    }

    static IEnumerator Exercise()
    {
        yield return null;
        var arena = Object.FindFirstObjectByType<BossPracticeArena>();
        Assert.That(arena.Player, Is.Not.Null);
        Assert.That(arena.arenaCamera.isActiveAndEnabled, Is.True);
        var attack = arena.Boss;
        Assert.That(attack.ShotsPerBurst, Is.EqualTo(3));
        Assert.That(attack.DelayBetweenShots, Is.EqualTo(.275f).Within(.001f));
        Assert.That(attack.RocketFallDelayMin, Is.EqualTo(.35f).Within(.001f));
        Assert.That(attack.RocketFallDelayMax, Is.EqualTo(1.8f).Within(.001f));
        Assert.That(attack.TargetSpreadRadius, Is.EqualTo(3.25f).Within(.001f));
        Assert.That(attack.AttackCooldown, Is.EqualTo(6f));
        var movement = arena.Player.GetComponent<CharacterMovement2D>();
        Vector3 start = arena.Player.transform.position;
        movement.MovementIntent = () => Vector2.up;
        float end = Time.time + .5f;
        while (Time.time < end) yield return null;
        Assert.That(arena.Player.transform.position.y, Is.GreaterThan(start.y + .5f));
        movement.MovementIntent = () => Vector2.zero;
        var shotsField = typeof(BossRocketAttack).GetField("shots", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        var seen = new HashSet<object>();
        var falling = new HashSet<int>();
        var delays = new List<float>();
        bool began = false;
        int cycles = 0;
        bool wasFiring = false;
        float deadline = Time.time + 20f;
        while (Time.time < deadline)
        {
            began |= attack.IsBurstActive;
            bool firing = attack.State == BossRocketAttack.AttackState.Firing;
            if (firing && !wasFiring) cycles++;
            wasFiring = firing;
            foreach (object shot in (IEnumerable)shotsField.GetValue(attack))
            {
                var type = shot.GetType();
                if (seen.Add(shot))
                {
                    float delay = (float)type.GetField("delay").GetValue(shot);
                    Assert.That(delay, Is.InRange(.35f, 1.8f));
                    delays.Add(delay);
                    var target = (Vector3)type.GetField("target").GetValue(shot);
                    Assert.That(Vector2.Distance(target, arena.Player.transform.position), Is.LessThanOrEqualTo(3.26f));
                }
                var visual = (GameObject)type.GetField("falling").GetValue(shot);
                if (visual != null) falling.Add(visual.GetInstanceID());
            }
            if (began && !attack.IsBurstActive && attack.PendingRocketCount == 0) break;
            yield return null;
        }
        Assert.That(cycles, Is.EqualTo(3));
        Assert.That(seen.Count, Is.EqualTo(6));
        Assert.That(falling.Count, Is.EqualTo(6));
        Assert.That(attack.GetComponent<EnemyChaseMovement>().IsAttackPaused, Is.False);
        arena.RestartRound();
        yield return null;
        Assert.That(arena.Player.IsDead, Is.False);
        Assert.That(arena.Player.CurrentHealth, Is.EqualTo(arena.Player.MaxHealth));
        Assert.That(arena.Boss.PendingRocketCount, Is.Zero);
        Assert.That(Object.FindObjectsByType<BossRocketAttack>(FindObjectsSortMode.None).Length, Is.EqualTo(1));
        Directory.CreateDirectory("Artifacts/BossPractice");
        File.WriteAllText("Artifacts/BossPractice/checks.txt", "PASS authored tuning: 3 Attack cycles, 6 launches, 6 falling rockets; real player movement, camera and restart. Delays: " + string.Join(", ", delays) + "\n");
    }
}
