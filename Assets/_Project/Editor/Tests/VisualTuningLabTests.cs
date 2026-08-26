using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class VisualTuningLabTests
{
    private static string LifecycleBackupPath => Path.Combine(
        Path.GetTempPath(), "Subject42_VisualTuningSavedValues.backup");

    [Test]
    public void PlayerVisualScaleAndOffsetDoNotChangeCollider()
    {
        GameObject player = new("Visual Test Player") { tag = "Player" };
        CapsuleCollider2D collider = player.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(1f, 2f);
        GameObject spriteObject = new("Player Sprite");
        spriteObject.transform.SetParent(player.transform, false);
        SpriteRenderer renderer = spriteObject.AddComponent<SpriteRenderer>();
        GameObject host = new("Visual Tuning Host");
        try
        {
            ProductionVisualTuningController tuning =
                host.AddComponent<ProductionVisualTuningController>();
            tuning.Configure();
            tuning.SetPlayerVisualScale(1.5f);
            tuning.SetPlayerVisualOffsetX(.4f);

            Assert.That(renderer.transform.localScale.x,
                Is.EqualTo(1.5f).Within(.001f));
            Assert.That(renderer.transform.localPosition.x,
                Is.EqualTo(.4f).Within(.001f));
            Assert.That(collider.size, Is.EqualTo(new Vector2(1f, 2f)));
            Assert.That(player.transform.localScale, Is.EqualTo(Vector3.one));
        }
        finally
        {
            Object.DestroyImmediate(host);
            Object.DestroyImmediate(player);
        }
    }

    [Test]
    public void OrbitVisualRadiusMultiplierDoesNotChangeWeaponRadius()
    {
        GameObject player = new("Orbit Test Player");
        LineRenderer line = player.AddComponent<LineRenderer>();
        PlayerWeaponOrbitVisual ring =
            player.AddComponent<PlayerWeaponOrbitVisual>();
        GameObject weaponObject = new("Orbit Test Weapon");
        ProjectileWeapon weapon = weaponObject.AddComponent<ProjectileWeapon>();
        try
        {
            float gameplayRadius = weapon.CurrentOrbitRadius;
            ring.Bind(weapon);
            ring.SetRingRadiusMultiplier(2.5f);

            Assert.That(weapon.CurrentOrbitRadius,
                Is.EqualTo(gameplayRadius).Within(.001f));
            Assert.That(ring.CurrentVisualRadius,
                Is.EqualTo(gameplayRadius * 2.5f).Within(.001f));
            Assert.That(line.positionCount, Is.GreaterThan(0));
        }
        finally
        {
            Object.DestroyImmediate(player);
            Object.DestroyImmediate(weaponObject);
        }
    }

    [Test]
    public void SavedProductionSnapshotBecomesProjectileResetSource()
    {
        GameObject host = new("Visual Persistence Host");
        try
        {
            ProductionVisualTuningController tuning =
                host.AddComponent<ProductionVisualTuningController>();
            VisualTuningSnapshot saved = new()
            {
                ProjectileScale = 1.63f,
                TrailWidth = 1.25f,
                TrailLifetime = 2.4f,
                TrailOpacity = .68f,
                TrailBrightness = 1.8f,
                LaserCoreWidth = 2.2f,
                LaserGlowWidth = 3.1f,
                LaserBrightness = 1.7f,
                PlayerScale = 1f,
                PlayerBrightness = 1f,
                PlayerSaturation = 1f,
                PlayerOpacity = 1f,
                PlayerTint = Color.white,
                WeaponScale = 1f,
                WeaponBrightness = 1f,
                WeaponSaturation = 1f,
                WeaponOpacity = 1f,
                WeaponTint = Color.white
            };

            tuning.ApplyProductionSnapshot(saved);
            tuning.SetProjectileVisualScale(3.5f);
            tuning.SetTrailWidth(4f);
            tuning.SetTrailTime(5f);
            tuning.ResetProjectileSettings();

            Assert.That(tuning.ProjectileVisualScale,
                Is.EqualTo(1.63f).Within(.001f));
            Assert.That(tuning.TrailWidth,
                Is.EqualTo(1.25f).Within(.001f));
            Assert.That(tuning.TrailTime,
                Is.EqualTo(2.4f).Within(.001f));
            Assert.That(tuning.ProductionProjectileVisualScale,
                Is.EqualTo(1.63f).Within(.001f));
        }
        finally
        {
            Object.DestroyImmediate(host);
        }
    }

    [Test]
    public void VersionControlledProductionPresetCanBeLoaded()
    {
        bool loaded = VisualTuningPresetStorage.TryLoad(
            out VisualTuningSnapshot snapshot,
            out string source,
            out string message);

        Assert.That(loaded, Is.True, message);
        Assert.That(source, Is.EqualTo(VisualTuningPresetStorage.AssetPath));
        Assert.That(snapshot.ProjectileScale, Is.GreaterThanOrEqualTo(.1f));
        Assert.That(snapshot.TrailWidth, Is.GreaterThanOrEqualTo(.1f));
        Assert.That(snapshot.CameraOrthographicSize, Is.InRange(2f, 16f));
    }

    [UnityTest, Explicit("Writes and restores the production asset; run in isolation.")]
    public IEnumerator ProductionValuesSurvivePlayModeRestart()
    {
        Assert.That(VisualTuningPresetStorage.TryLoad(
            out VisualTuningSnapshot original,
            out _, out string loadMessage), Is.True, loadMessage);
        File.Copy(
            VisualTuningPresetStorage.AssetPath, LifecycleBackupPath, true);
        VisualTuningSnapshot testValues = original;
        testValues.ProjectileScale = 1.63f;
        testValues.TrailWidth = 1.25f;
        testValues.TrailLifetime = 2.4f;
        testValues.CameraOrthographicSize = 12.5f;

        try
        {
            Assert.That(VisualTuningPresetStorage.Save(
                testValues, "Visual persistence lifecycle test",
                out string saveMessage), Is.True, saveMessage);

            yield return new EnterPlayMode();

            Assert.That(VisualTuningPresetStorage.TryLoad(
                out VisualTuningSnapshot afterRestart,
                out _, out string restartMessage), Is.True, restartMessage);
            Assert.That(afterRestart.ProjectileScale,
                Is.EqualTo(1.63f).Within(.001f));
            Assert.That(afterRestart.TrailWidth,
                Is.EqualTo(1.25f).Within(.001f));
            Assert.That(afterRestart.TrailLifetime,
                Is.EqualTo(2.4f).Within(.001f));
            Assert.That(afterRestart.CameraOrthographicSize,
                Is.EqualTo(12.5f).Within(.001f));

            GameObject host = new("Visual Restart Test Host");
            ProductionVisualTuningController tuning =
                host.AddComponent<ProductionVisualTuningController>();
            tuning.ApplyProductionSnapshot(afterRestart);
            tuning.SetProjectileVisualScale(3.2f);
            tuning.SetTrailWidth(4f);
            tuning.ResetProjectileSettings();
            Assert.That(tuning.ProjectileVisualScale,
                Is.EqualTo(1.63f).Within(.001f));
            Assert.That(tuning.TrailWidth,
                Is.EqualTo(1.25f).Within(.001f));
            Object.Destroy(host);

            yield return new ExitPlayMode();
        }
        finally
        {
            if (File.Exists(LifecycleBackupPath))
            {
                File.Copy(
                    LifecycleBackupPath,
                    VisualTuningPresetStorage.AssetPath,
                    true);
                File.Delete(LifecycleBackupPath);
                AssetDatabase.ImportAsset(
                    VisualTuningPresetStorage.AssetPath,
                    ImportAssetOptions.ForceUpdate);
            }
        }
    }
}
