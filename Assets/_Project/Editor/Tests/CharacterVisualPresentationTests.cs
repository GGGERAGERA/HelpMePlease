#if UNITY_EDITOR
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CharacterVisualPresentationTests
{
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        if (Application.isPlaying)
        {
            Time.timeScale = 1f;
            yield return new ExitPlayMode();
        }
    }

    [UnityTest]
    public IEnumerator ProductionCharacters_PathsFacingAndSectorRestore()
    {
        yield return new EnterPlayMode();
        const string output = "Artifacts/GeneratedQA/CharacterVisuals/Stabilization/";
        Directory.CreateDirectory(output);
        var names = new[] { "01_Gera", "02_Di-mag", "03_Vika" };
        var paths = new[] { OrbitalPathType.Circle, OrbitalPathType.FigureEight, OrbitalPathType.Custom };
        for (int index = 0; index < names.Length; index++)
        {
            yield return SceneManager.LoadSceneAsync("MainMenu");
            yield return null;
            var character = AssetDatabase.LoadAssetAtPath<CharacterData>(
                "Assets/_Project/Scriptable Objects/Characters/" + names[index] + ".asset");
            RunSelectionManager.Instance.SelectCharacter(character);
            var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
            starter.StartRun((Transform)typeof(BunkerRunStarter).GetField("cameraRig",
                BindingFlags.NonPublic | BindingFlags.Instance).GetValue(starter));
            float deadline = Time.realtimeSinceStartup + 30f;
            OrbitalStationRuntime station = null;
            while (Time.realtimeSinceStartup < deadline)
            {
                station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
                if (station != null && station.IsInitialized && !SceneTransitionOverlay.IsTransitioning &&
                    (index != 2 || (station.CustomDrawing != null && station.CustomDrawing.isActiveAndEnabled))) break;
                yield return null;
            }
            Assert.That(station != null && station.IsInitialized, Is.True, names[index]);
            Assert.That(station.State.UsesCustomPaths, Is.EqualTo(index == 2));
            if (index == 2)
            {
                Assert.That(station.HasPendingCustomRings, Is.True);
                var draw = station.CustomDrawing;
                Assert.That(draw.isActiveAndEnabled, Is.True);
                draw.BeginStroke(Vector2.right * 3f);
                for (int i = 1; i < 120; i++)
                {
                    float a = i * Mathf.PI * 2f / 120f;
                    draw.AppendPoint(new Vector2(3f * Mathf.Cos(a), 2f * Mathf.Sin(a)));
                }
                draw.EndStroke(Vector2.right * 3f);
                Assert.That(draw.State, Is.EqualTo(CustomOrbitDrawing.PathState.VALID));
                yield return null;
                ScreenCapture.CaptureScreenshot(output + "Vika-drawing.png");
                for (int frame = 0; frame < 4; frame++) yield return null;
                draw.Confirm();
            }
            else
            {
                Assert.That(station.HasPendingCustomRings, Is.False);
                station.State.AddRing();
                station.State.AddRing();
                var state = station.State;
                int first = state.Rings[0].StableRingId;
                int second = state.Rings[1].StableRingId;
                int third = state.Rings[2].StableRingId;
                Assert.That(state.AddMount(first, out _), Is.True);
                Assert.That(state.AddMount(first, out _), Is.True);
                Assert.That(state.InstallModule(OrbitalModuleKind.ArcEmitter, first, 1, out _), Is.True);
                Assert.That(state.InstallModule(OrbitalModuleKind.LaserSword, first, 2, out _), Is.True);
                Assert.That(state.InstallModule(OrbitalModuleKind.ImpulseGun, second, 0, out _), Is.True);
                Assert.That(state.AddMount(second, out _), Is.True);
                Assert.That(state.InstallLinkPair(second, 1, third, 0, out _, out _, out _), Is.True);
                Assert.That(state.UpgradeRingSpeed(first), Is.True);
                Assert.That(state.UpgradeRingPower(first), Is.True);
                Assert.That(state.UpgradeRingCapacity(first), Is.True);
                Assert.That(state.UpgradeCore(), Is.True);
                Assert.That(station.RebuildRuntimeFromState(), Is.True);
                Assert.That(station.Rings.Count, Is.EqualTo(3));
            }
            foreach (var ring in station.Rings)
                Assert.That(ring.Geometry.Type, Is.EqualTo(paths[index]), names[index]);

            var movement = station.GetComponentInParent<CharacterMovement2D>();
            var core = station.GetComponent<OrbitalStationView>().Core;
            Vector3 coreScale = core.transform.localScale;
            Color coreColor = core.color;
            for (int direction = -1; direction <= 1; direction += 2)
            {
                int intent = direction;
                movement.MovementIntent = () => new Vector2(intent, 0f);
                for (int frame = 0; frame < 12; frame++) yield return null;
                Assert.That(Mathf.Sign(movement.VisualRoot.localScale.x), Is.EqualTo(-direction));
                Assert.That(movement.VisualRoot.gameObject.activeInHierarchy, Is.True);
                ScreenCapture.CaptureScreenshot(output + names[index] + (direction < 0 ? "-left.png" : "-right.png"));
                for (int frame = 0; frame < 4; frame++) yield return null;
            }
            movement.MovementIntent = () => Vector2.zero;
            station.FlashCore(Color.white);
            for (int frame = 0; frame < 3; frame++) yield return null;
            Assert.That(core.transform.localScale, Is.EqualTo(coreScale), "Core art keeps its authored size while pulsing");
            Assert.That(core.color, Is.EqualTo(coreColor), "Core art keeps its authored color while pulsing");
            string before = JsonUtility.ToJson(station.State);
            Assert.That(station.SimulateSectorRestore(), Is.True);
            Assert.That(JsonUtility.ToJson(station.State), Is.EqualTo(before));
            foreach (var ring in station.Rings)
                Assert.That(ring.Geometry.Type, Is.EqualTo(paths[index]));
            if (index == 2)
            {
                Assert.That(station.AddRing(), Is.Not.Null);
                yield return null;
                Assert.That(station.HasPendingCustomRings, Is.True, "Every new Vika ring needs its own drawing");
                Assert.That(station.CustomDrawing.isActiveAndEnabled, Is.True);
                var draw = station.CustomDrawing;
                draw.BeginStroke(Vector2.right * 4f);
                for (int i = 1; i < 120; i++)
                {
                    float a = i * Mathf.PI * 2f / 120f;
                    draw.AppendPoint(new Vector2(4f * Mathf.Cos(a), 3f * Mathf.Sin(a)));
                }
                draw.EndStroke(Vector2.right * 4f);
                Assert.That(draw.State, Is.EqualTo(CustomOrbitDrawing.PathState.VALID));
                draw.Confirm();
                yield return null;
                Assert.That(station.HasPendingCustomRings, Is.False);
                Assert.That(station.Rings.Count, Is.EqualTo(2));
                foreach (var ring in station.Rings)
                    Assert.That(ring.Geometry.Type, Is.EqualTo(OrbitalPathType.Custom));
                ScreenCapture.CaptureScreenshot(output + "Vika-two-drawn-rings.png");
                for (int frame = 0; frame < 4; frame++) yield return null;
            }
        }
        yield return new ExitPlayMode();
    }

    [Test]
    public void BunkerAndProductionShareEachCharactersVisual()
    {
        foreach (var entry in OrbitalPresentationConfig.Active.PlayerVariants)
        {
            var source = entry.Source.GetComponentInChildren<Animator>(true);
            var production = entry.Production.GetComponentInChildren<Animator>(true);
            Assert.That(PrefabUtility.GetCorrespondingObjectFromOriginalSource(production.gameObject),
                Is.SameAs(PrefabUtility.GetCorrespondingObjectFromOriginalSource(source.gameObject)), entry.Source.name);
            string[] Appearance(Animator root) => root.GetComponentsInChildren<SpriteRenderer>(true)
                .Select(r => $"{AnimationUtility.CalculateTransformPath(r.transform, root.transform)}:{r.sprite?.name}:{r.enabled}:{r.gameObject.activeSelf}")
                .OrderBy(s => s).ToArray();
            Assert.That(Appearance(production), Is.EqualTo(Appearance(source)), entry.Source.name);
        }
    }

    [UnityTest]
    public IEnumerator VikaInGameplay_DrawHelperAboveArea()
    {
        yield return new EnterPlayMode();
        yield return SceneManager.LoadSceneAsync("MainMenu");
        yield return null;
        var character = AssetDatabase.LoadAssetAtPath<CharacterData>("Assets/_Project/Scriptable Objects/Characters/03_Vika.asset");
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        starter.StartRun((Transform)typeof(BunkerRunStarter).GetField("cameraRig", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(starter));
        float deadline = Time.realtimeSinceStartup + 30;
        OrbitalStationRuntime station = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            station = Object.FindFirstObjectByType<OrbitalStationRuntime>();
            if (station != null && station.CustomDrawing != null && station.CustomDrawing.isActiveAndEnabled) break;
            yield return null;
        }
        Assert.That(station?.CustomDrawing, Is.Not.Null);
        var draw = station.CustomDrawing;
        for (int i = 0; i < 8; i++) yield return null;
        var panel = (Rect)typeof(CustomOrbitDrawing).GetProperty("PanelRect", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(draw);
        Vector3 top = draw.DrawingCamera.WorldToScreenPoint(draw.Player.transform.position + Vector3.up * draw.MaxDrawRadius);
        Assert.That(panel.yMax, Is.LessThan(Screen.height - top.y), "Helper must fit above the draw circle");
        Assert.That(draw.Player.GetComponentInChildren<Animator>().runtimeAnimatorController,
            Is.EqualTo(character.characterPrefab.GetComponentInChildren<Animator>().runtimeAnimatorController));
        draw.BeginStroke(Vector2.right * 3);
        for (int i = 1; i < 120; i++)
        {
            float angle = i * Mathf.PI * 2 / 120;
            draw.AppendPoint(new Vector2(3 * Mathf.Cos(angle), 2 * Mathf.Sin(angle)));
        }
        draw.EndStroke(Vector2.right * 3);
        Assert.That(draw.State, Is.EqualTo(CustomOrbitDrawing.PathState.VALID));
        for (int i = 0; i < 4; i++) yield return null;
        const string output = "Artifacts/GeneratedQA/CharacterVisuals/";
        Directory.CreateDirectory(output);
        ScreenCapture.CaptureScreenshot(output + "draw.png");
        for (int i = 0; i < 4; i++) yield return null;
        draw.Confirm();
        yield return null;
        Assert.That(station.HasPendingCustomRings, Is.False);
        Assert.That(draw.gameObject.activeSelf, Is.False);
        Assert.That(Time.timeScale, Is.EqualTo(1f));
        yield return new ExitPlayMode();
    }
}
#endif
