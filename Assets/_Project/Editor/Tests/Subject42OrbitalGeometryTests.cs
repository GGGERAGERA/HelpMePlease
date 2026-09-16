#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;
using GCIs = UnityEngine.TestTools.Constraints.Is;
using UnityEngine.TestTools.Constraints;
using Is = NUnit.Framework.Is;

public sealed class Subject42OrbitalGeometryTests
{
    public const string Output = "Artifacts/GeneratedQA/CharacterGeometry/";

    [Test]
    public void AllocationCounterPositiveControl()
    {
        Assert.That((TestDelegate)(() => GC.KeepAlive(new byte[8192])), GCIs.AllocatingGCMemory());
    }

    [Test]
    public void CircleMatchesPreviousFormulaAndFigureEightStyles()
    {
        for (int ring = 0; ring < 8; ring++)
            for (int degrees = 0; degrees < 360; degrees++)
            {
                float radius = 1.25f + ring * .72f;
                float radians = degrees * Mathf.Deg2Rad;
                Assert.That(OrbitalPathGeometry.Circle.PositionDegrees(degrees, radius), Is.EqualTo(
                    new Vector3(Mathf.Cos(radians) * radius, Mathf.Sin(radians) * radius, 0f)));
            }
        var config = OrbitalPresentationConfig.Active;
        var view = Object.Instantiate(config.RingPrefab);
        try
        {
            view.InitializeGeometry(new OrbitalPathGeometry(config));
            var material = view.FrontLine.sharedMaterial;
            var block = new MaterialPropertyBlock();
            for (int tier = 1; tier <= 4; tier++)
            {
                view.InitializeTier(tier);
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, false, true, 0f);
                Assert.That(view.AccentColor, Is.EqualTo(config.GetRingTier(tier).BaseColor));
                view.FrontLine.GetPropertyBlock(block);
                float plain = block.GetVector("_RingSurface").x;
                view.UpdateTierAppearance(tier, 2f, 1f, 2f, false, true, 0f);
                view.FrontLine.GetPropertyBlock(block);
                Assert.That(block.GetVector("_RingSurface").x, Is.GreaterThan(plain));
                float alpha = view.FrontLine.colorGradient.Evaluate(.5f).a;
                view.UpdateTierAppearance(tier, 2f, 0f, 0f, true, true, 0f);
                Assert.That(view.FrontLine.colorGradient.Evaluate(.5f).a, Is.LessThan(alpha));
                Assert.That(view.FrontLine.sharedMaterial, Is.SameAs(material));
            }
        }
        finally { Object.DestroyImmediate(view.gameObject); }
    }

    [Test]
    public void SteadyTargetingWithLiveEnemyDoesNotAllocate()
    {
        var root = new GameObject("Targeting allocation fixture");
        try
        {
            for (int i = 3; i >= 1; i--)
            {
                var target = new GameObject("Target " + i);
                target.transform.SetParent(root.transform);
                target.transform.position = new Vector3(i, 0, 0);
                var enemy = target.AddComponent<EnemyHealth>();
                typeof(EnemyHealth).GetMethod("OnEnable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(enemy, null); // Edit Mode does not run this runtime component's lifecycle.
                enemy.SetRuntimeMaxHealth(100f);
            }
            var combat = new ProductionOrbitalCombatAdapter(root.transform, OrbitalPresentationConfig.Active.PixelSprite);
            for (int warm = 0; warm < 32; warm++)
            {
                combat.FindNearest(Vector2.zero, 8f);
                combat.FindNearestMany(Vector2.zero, 8f, 3);
            }
            Assert.That(combat.FindNearest(Vector2.zero, 8f).transform.position.x, Is.EqualTo(1));
            var ordered = combat.FindNearestMany(Vector2.zero, 8f, 3);
            for (int i = 0; i < 3; i++) Assert.That(ordered[i].transform.position.x, Is.EqualTo(i + 1));
            Assert.That((TestDelegate)(() => {
                for (int i = 0; i < 256; i++)
                {
                    combat.FindNearest(Vector2.zero, 8f);
                    combat.FindNearestMany(Vector2.zero, 8f, 3);
                }
            }), GCIs.Not.AllocatingGCMemory());
        }
        finally
        {
            foreach (var enemy in root.GetComponentsInChildren<EnemyHealth>()) typeof(EnemyHealth).GetMethod("OnDisable", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(enemy, null);
            Object.DestroyImmediate(root);
        }
    }

    [Test]
    public void ArcLengthClosureCrossingsAndPicking()
    {
        var path = new OrbitalPathGeometry(OrbitalPresentationConfig.Active);
        float min = float.MaxValue, max = 0f;
        for (int i = 0; i < 4096; i++)
        {
            Vector2 a = path.Position(i / 4096f, 2f), b = path.Position((i + 1) / 4096f, 2f);
            float length = Vector2.Distance(a, b);
            min = Mathf.Min(min, length); max = Mathf.Max(max, length);
            Assert.That(path.Distance(a, 2f), Is.LessThan(.002f));
            Assert.That(float.IsNaN(path.Rotation(i * 360f / 4096)), Is.False);
        }
        Assert.That(max / min, Is.LessThan(1.015f), "Uniform distance per cyclic phase step");
        Assert.That(path.Position(.25f, 2f).magnitude, Is.LessThan(.0001f));
        Assert.That(path.Position(.75f, 2f).magnitude, Is.LessThan(.0001f));
        Assert.That(path.Position(0f, 2f), Is.EqualTo(path.Position(1f, 2f)));
        Assert.That(path.Position(0f, 2f).x, Is.GreaterThan(0));
        Assert.That(path.Position(.5f, 2f).x, Is.LessThan(0));
        Assert.That(path.IsBehind(Vector2.zero), Is.False);
        Assert.That(path.BackAmount(Vector2.zero), Is.Zero);
    }

    [Test]
    public void EightRingsSpeedPowerCapacityAndAllocation()
    {
        var root = new GameObject("FigureEight allocation fixture");
        try
        {
            var config = OrbitalPresentationConfig.Active;
            var path = new OrbitalPathGeometry(config);
            var state = OrbitalRunState.CreateDefault(17);
            while (state.Rings.Count < 8) state.AddRing();
            var rings = new OrbitalRingRuntime[8];
            for (int i = 0; i < rings.Length; i++)
            {
                var rs = state.Rings[i];
                int id = rs.StableRingId;
                Assert.That(state.AddMount(id, out _), Is.True);
                Assert.That(state.AddMount(id, out _), Is.True);
                for (int mount = i == 0 ? 1 : 0; mount < 3; mount++)
                    Assert.That(state.InstallModule(OrbitalModuleKind.Pistol, id, mount, out _), Is.True);
                Assert.That(state.UpgradeRingCapacity(id), Is.True);
                Assert.That(state.AddMount(id, out _), Is.True);
                Assert.That(state.InstallModule(OrbitalModuleKind.Pistol, id, 3, out _), Is.True);
                Assert.That(state.UpgradeRingSpeed(id), Is.True);
                Assert.That(state.UpgradeRingPower(id), Is.True);
                rings[i] = new OrbitalRingRuntime(rs, root.transform, config.VisualMaterial, config.PixelSprite, false, path);
                Assert.That(rings[i].MountCapacity, Is.EqualTo(4));
                Assert.That(rings[i].PowerMultiplier, Is.EqualTo(1.25f));
                Assert.That(rings[i].RotationSpeed, Is.EqualTo(rs.BaseRotationSpeed * 1.25f));
                for (int m = 0; m < 4; m++) Assert.That(rings[i].Mounts[m].LocalPhase, Is.EqualTo(m * 90f));
                for (int warm = 0; warm < 64; warm++) rings[i].Tick(.016f);
            }
            Assert.That((TestDelegate)(() => {
                for (int tick = 0; tick < 256; tick++)
                    for (int i = 0; i < rings.Length; i++) rings[i].Tick(.016f);
            }), GCIs.Not.AllocatingGCMemory());
            foreach (var view in root.GetComponentsInChildren<OrbitalRingView>())
            {
                Assert.That(view.BackLine.enabled, Is.False);
                Assert.That(view.FrontLine.loop, Is.True);
                Assert.That(view.FrontLine.GetPosition(view.FrontLine.positionCount / 4).magnitude, Is.LessThan(.0001f));
                Assert.That(view.FrontLine.sharedMaterial, Is.SameAs(config.RingPrefab.FrontLine.sharedMaterial));
            }
        }
        finally { Object.DestroyImmediate(root); }
    }

    [Test, Explicit("Manual BEFORE baseline capture")] public void CaptureGeraBefore() => CaptureGera("Before");
    [Test, Explicit("Requires the BEFORE capture")] public void CaptureGeraAfter() => CaptureGera("After");

    private static void CaptureGera(string label)
    {
        var config = OrbitalPresentationConfig.Active;
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/01_Gera.asset");
        foreach (int count in new[] { 1, 4, 8 })
        {
            var player = Object.Instantiate(character.ProductionPrefab);
            var cameraRoot = new GameObject("Geometry capture camera");
            var target = new RenderTexture(1280, 720, 24);
            var pixels = new Texture2D(1280, 720, TextureFormat.RGB24, false);
            var previous = RenderTexture.active;
            try
            {
                player.transform.position = Vector3.zero;
                var root = player.GetComponentInChildren<OrbitalStationView>().RingsRoot;
                var state = OrbitalRunState.CreateDefault(17);
                while (state.Rings.Count < count) state.AddRing();
                var positions = new System.Text.StringBuilder();
                foreach (var rs in state.Rings)
                {
                    rs.CurrentPhase = 37f;
                    var ring = new OrbitalRingRuntime(rs, root, config.VisualMaterial, config.PixelSprite);
                    ring.Tick(0f);
                    foreach (var mount in ring.Mounts)
                    {
                        positions.AppendLine(JsonUtility.ToJson(mount.Transform.localPosition));
                        Object.Instantiate(config.PistolPrefab, mount.Transform, false);
                        mount.SetVisualState(OrbitalMountRuntime.VisualState.Occupied);
                    }
                }
                foreach (var child in player.GetComponentsInChildren<Transform>(true)) child.gameObject.layer = 31;
                var camera = cameraRoot.AddComponent<Camera>();
                camera.transform.position = new Vector3(0, 0, -10);
                camera.orthographic = true;
                camera.orthographicSize = count == 1 ? 3.5f : 8f;
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.015f, .02f, .035f);
                camera.cullingMask = 1 << 31;
                camera.targetTexture = target;
                camera.Render();
                RenderTexture.active = target;
                pixels.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0);
                pixels.Apply();
                Directory.CreateDirectory(Output + label);
                File.WriteAllBytes(Output + label + $"/gera-{count}.png", pixels.EncodeToPNG());
                File.WriteAllText(Output + label + $"/gera-{count}.json", positions.ToString());
                if (label == "After")
                    Assert.That(positions.ToString(), Is.EqualTo(File.ReadAllText(Output + $"Before/gera-{count}.json")));
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(player); Object.DestroyImmediate(cameraRoot);
                Object.DestroyImmediate(target); Object.DestroyImmediate(pixels);
            }
        }
    }
}
#endif
