#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class EnemyHitFlashTests
{
    [UnityTest]
    public IEnumerator ShooterAndAltFlashAfterPoolReuse()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        Time.timeScale = 1f;
        foreach (var path in new[] { "p_Enemy_Shooter.prefab", "PreparedVariants/p_Enemy_Shooter_Alt.prefab" })
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/Enemies/" + path);
            var ownerObject = new GameObject("Flash test pool owner");
            var owner = ownerObject.AddComponent<EnemyWhiteFlash>();
            var pool = new SimplePrefabPool(owner, prefab, 1, 1);
            var item = pool.Get(Vector3.zero, Quaternion.identity);
            var health = item.GetComponent<EnemyHealth>();
            var flash = item.GetComponent<EnemyWhiteFlash>();
            var body = flash.TargetRenderer;
            Assert.That(body.gameObject.activeInHierarchy && body.enabled, Is.True, path + " targets a hidden body");
            var baseline = body.sharedMaterial;
            var flashMaterial = new SerializedObject(flash).FindProperty("flashMaterial").objectReferenceValue;
            yield return null;
            health.TakeDamage(1f, Vector2.zero);
            Assert.That(body.sharedMaterial, Is.SameAs(flashMaterial), path);
            float until = Time.time + flash.FlashDuration + 0.1f;
            while (Time.time < until) yield return null;
            Assert.That(body.sharedMaterial, Is.SameAs(baseline), path);
            health.TakeDamage(1f, Vector2.zero);
            item.Release(); // Return during the flash, not only after it finishes.
            var reused = pool.Get(Vector3.zero, Quaternion.identity);
            Assert.That(reused, Is.SameAs(item));
            Assert.That(body.sharedMaterial, Is.SameAs(baseline), "Respawn must clear interrupted flash");
            health.TakeDamage(1f, Vector2.zero);
            Assert.That(body.sharedMaterial, Is.SameAs(flashMaterial), "Respawn damage must flash");
            until = Time.time + flash.FlashDuration + 0.1f;
            while (Time.time < until) yield return null;
            Assert.That(body.sharedMaterial, Is.SameAs(baseline));
            item.Release();
            pool.Dispose();
            Object.Destroy(ownerObject);
        }
        yield return new ExitPlayMode();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
