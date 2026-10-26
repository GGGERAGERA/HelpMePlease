#if UNITY_EDITOR
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class DashFeedbackTests
{
    private const string PresentationGuid = "13887751ea3346c8a28fa57cb1dfb211";
    private const string EffectPath = "Assets/_Project/prefabs/fx/PlayerDashBurst.prefab";

    [TestCase("Gera")]
    [TestCase("Vika")]
    [TestCase("DiMag")]
    public void ProductionCharactersAuthorDashAndKeepHealthFeedback(string character)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(
            "Assets/_Project/prefabs/Characters/" + character + ".prefab");
        var presentation = prefab.GetComponent(PresentationType());
        Assert.That(presentation, Is.Not.Null, character + " must retain its feedback component after migration.");
        var serialized = new SerializedObject(presentation);
        AssertReference(serialized, "movement", prefab.GetComponent<CharacterMovement2D>());
        AssertReference(serialized, "health", prefab.GetComponent<PlayerHealth>());
        AssertReference(serialized, "flash", prefab.GetComponent<PlayerWhiteFlash>());
        AssertReference(serialized, "hitSound", prefab.GetComponent<PlayerHitSound>());
        var effect = serialized.FindProperty("dashEffect");
        Assert.That(effect, Is.Not.Null, "Accepted dash needs authored spatial feedback.");
        Assert.That(effect.objectReferenceValue, Is.Not.Null, character + " has no dash effect.");
        Assert.That(AssetDatabase.GetAssetPath(effect.objectReferenceValue), Is.EqualTo(EffectPath));
    }

    [Test]
    public void AuthoredDashBurstIsVisibleAndFinishesBeforeHalfASecond()
    {
        var effect = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectPath);
        Assert.That(effect, Is.Not.Null, "A short authored dash burst is missing.");
        Assert.That(effect.main.stopAction, Is.EqualTo(ParticleSystemStopAction.Destroy),
            "Finished dash effects must release their world object.");
        foreach (var particles in effect.GetComponentsInChildren<ParticleSystem>(true))
        {
            var main = particles.main;
            Assert.That(main.loop, Is.False);
            Assert.That(main.duration + main.startLifetime.constantMax, Is.LessThanOrEqualTo(.5f),
                "Dash feedback must read as an impulse rather than a persistent movement aura.");
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            Assert.That(renderer.enabled && renderer.sharedMaterial != null, Is.True);
            Assert.That(particles.emission.burstCount, Is.GreaterThan(0), "Accepted dash must emit immediately.");
        }
    }

    [UnityTest]
    public IEnumerator AcceptedDashEmitsOnceWhileWalkingAndCooldownDoNot()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        yield return new EnterPlayMode();
        yield return VerifyDashInPlayMode();
    }

    private static IEnumerator VerifyDashInPlayMode()
    {
        Time.timeScale = 1f;
        var effect = AssetDatabase.LoadAssetAtPath<ParticleSystem>(EffectPath);
        Assert.That(effect, Is.Not.Null, "A short authored dash burst is missing.");

        // Keep the player's scene separate from the active scene to catch additive-scene leaks.
        var playerScene = SceneManager.CreateScene("DashFeedbackFixture");
        var player = new GameObject("Dash feedback player");
        player.SetActive(false);
        SceneManager.MoveGameObjectToScene(player, playerScene);
        player.AddComponent<Rigidbody2D>().gravityScale = 0f;
        var health = player.AddComponent<PlayerHealth>();
        var movement = player.AddComponent<CharacterMovement2D>();
        var presentation = (MonoBehaviour)player.AddComponent(PresentationType());
        var serialized = new SerializedObject(presentation);
        SetReference(serialized, "health", health);
        SetReference(serialized, "movement", movement);
        SetReference(serialized, "dashEffect", effect);
        var renderedSprites = serialized.FindProperty("renderers");
        Assert.That(renderedSprites, Is.Not.Null, "Feedback migration must retain its authored sprite array on " + presentation.GetType().FullName);
        renderedSprites.arraySize = 0;
        serialized.ApplyModifiedPropertiesWithoutUndo();
        player.SetActive(true);
        Vector2 intent = Vector2.up;
        movement.MovementIntent = () => intent;
        yield return null;
        yield return new WaitForFixedUpdate();
        Assert.That(Effects(effect).Length, Is.Zero, "Ordinary walking must not create dash feedback.");

        foreach (var direction in new[] { Vector2.up, Vector2.left, Vector2.one.normalized })
        {
            intent = direction;
            yield return null; // Normal movement samples and remembers the actual dash direction.
            Vector3 origin = player.transform.position;
            StartDash(movement); // Same acceptance method used by the Space-key branch.
            Assert.That(movement.IsDashReady, Is.False, "The real movement owner must accept the dash.");
            var spawned = Effects(effect);
            Assert.That(spawned.Length, Is.EqualTo(1), "Accepted dash must create exactly one burst.");
            var burst = spawned[0];
            Assert.That(burst.gameObject.scene, Is.EqualTo(playerScene), "VFX belongs to the player's scene.");
            Assert.That(burst.transform.parent, Is.Null, "The launch burst must stay in world space.");
            Assert.That(Vector3.Distance(burst.transform.position, origin), Is.LessThan(1f));
            Assert.That(Vector3.Dot(burst.transform.forward, -(Vector3)direction), Is.GreaterThan(.99f),
                "The particle cone must point behind the accepted dash.");
            yield return null;
            Assert.That(burst.GetComponentsInChildren<ParticleSystem>().Sum(p => p.particleCount), Is.GreaterThan(0));
            StartDash(movement);
            Assert.That(Effects(effect).Length, Is.EqualTo(1), "Rejected cooldown input must not emit another burst.");
            Assert.That(Effects(effect)[0], Is.SameAs(burst));
            // EditMode tests remain driven by the editor after EnterPlayMode;
            // wait for actual game time instead of yielding a coroutine-only instruction.
            float expires = Time.time + .65f;
            while (Time.time < expires) yield return null;
            Assert.That(burst == null, Is.True, "A completed dash burst leaked its world object." +
                (burst == null ? "" : $" time={burst.time}, playing={burst.isPlaying}, paused={burst.isPaused}, alive={burst.IsAlive(true)}"));

            // Reset via the real lifecycle instead of writing movement's private cooldown state.
            presentation.enabled = false;
            movement.enabled = false;
            movement.enabled = true;
            presentation.enabled = true;
            movement.MovementIntent = () => intent;
            yield return null;
        }

        StartDash(movement);
        Assert.That(Effects(effect).Length, Is.EqualTo(1), "Re-enable must restore one subscription.");
        presentation.enabled = false;
        yield return null;
        Assert.That(Effects(effect).Length, Is.Zero, "Disabling the presentation must clear its in-flight effect.");
        presentation.enabled = true;
        movement.enabled = false;
        movement.enabled = true;
        movement.MovementIntent = () => intent;
        yield return null;
        StartDash(movement);
        var finalBurst = Effects(effect).Single();
        yield return SceneManager.UnloadSceneAsync(playerScene);
        Assert.That(finalBurst == null, Is.True, "Scene exit must not leave dash effects in the active scene.");
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }
    }

    private static System.Type PresentationType()
    {
        var script = AssetDatabase.LoadAssetAtPath<MonoScript>(AssetDatabase.GUIDToAssetPath(PresentationGuid));
        Assert.That(script, Is.Not.Null, "Migration must preserve existing presentation GUID.");
        return script.GetClass();
    }

    private static ParticleSystem[] Effects(ParticleSystem prefab) => Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
        .Where(p => p.transform.parent == null && p.name == prefab.name + "(Clone)").ToArray();

    private static void StartDash(CharacterMovement2D movement) => typeof(CharacterMovement2D)
        .GetMethod("TryStartDash", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(movement, null);

    private static void AssertReference(SerializedObject owner, string name, Object expected)
    {
        var property = owner.FindProperty(name);
        Assert.That(property, Is.Not.Null, name + " must be authored explicitly.");
        Assert.That(property.objectReferenceValue, Is.SameAs(expected), name);
    }

    private static void SetReference(SerializedObject owner, string name, Object value)
    {
        var property = owner.FindProperty(name);
        Assert.That(property, Is.Not.Null, name + " must be authored explicitly.");
        property.objectReferenceValue = value;
    }
}
#endif
