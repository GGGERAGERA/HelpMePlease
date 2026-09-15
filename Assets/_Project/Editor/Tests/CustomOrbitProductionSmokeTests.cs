#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

public sealed class CustomOrbitProductionSmokeTests
{
    [UnityTest]
    public IEnumerator ProductionCreationQueue_Modules_AndSectorRestore()
    {
        yield return new EnterPlayMode();
        const string slotKey = "ORBITAL_SLOT_PENDING";
        bool hadSlot = PlayerPrefs.HasKey(slotKey);
        int savedSlot = PlayerPrefs.GetInt(slotKey);
        try
        {
            PlayerPrefs.DeleteKey(slotKey);
            yield return StartRun("Gera");
            var station = Station();
            Assert.That(station.HasPendingCustomRings, Is.False);
            Assert.That(station.Rings.Single().Geometry.Type, Is.EqualTo(OrbitalPathType.Circle));
            Assert.That(station.Modules.Single().Kind, Is.EqualTo(OrbitalModuleKind.Pistol));
            Assert.That(station.CustomDrawing, Is.Null);
            Debug.Log("CustomOrbit PASS 1: Gera retains circular start, no drawing.");

            yield return StartRun("Vika");
            station = Station();
            yield return WaitDrawing(station);
            Assert.That(station.State.PendingRingCount, Is.EqualTo(1));
            Assert.That(station.Rings.Count, Is.Zero);
            Assert.That(station.State.Rings[0].MountCount, Is.EqualTo(1));
            Assert.That(station.State.Modules.Single().ModuleType, Is.EqualTo(OrbitalModuleKind.Pistol));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(station.InputOwner.IsGameplayInputBlocked, Is.True);
            var drawing = station.CustomDrawing;
            drawing.BeginStroke(Vector2.right * 2);
            int count = drawing.PathLine.positionCount;
            drawing.AppendPoint(Vector2.right * 20);
            Assert.That(drawing.PathLine.positionCount, Is.EqualTo(count));
            drawing.EndStroke(Vector2.up * 3);
            drawing.Confirm();
            Assert.That(station.State.PendingRingCount, Is.EqualTo(1), "Invalid input must retain the earned ring");
            Draw(station, false);
            yield return null;
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Assert.That(station.Rings.Count, Is.EqualTo(1));
            Assert.That(station.Modules.Count, Is.EqualTo(1));
            Assert.That(drawing.gameObject.activeSelf, Is.False);
            Debug.Log("CustomOrbit PASS 2: Vika pending start, boundary/invalid guard, Confirm activates gun and gameplay.");

            var upgrades = UpgradeManager.Instance;
            Assert.That(upgrades.DebugForceOrbitalReward(OrbitalRewardKind.NewRing), Is.True);
            Assert.That(upgrades.DebugSelectCurrentChoice(0), Is.True);
            yield return WaitDrawing(station);
            Assert.That(station.Rings.Count, Is.EqualTo(1));
            Assert.That(upgrades.IsRewardQueueIdle, Is.False);
            Draw(station, true);
            yield return null;
            yield return null;
            Assert.That(upgrades.IsRewardQueueIdle, Is.True);
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            var state = station.State;
            Vector2[][] paths = state.Rings.Select(r => (Vector2[])r.CustomPath.Clone()).ToArray();
            Assert.That(paths[0].SequenceEqual(paths[1]), Is.False);
            Debug.Log("CustomOrbit PASS 3/5: NewRing reward awaits Confirm; circle and eight remain independent.");

            int first = state.Rings[0].StableRingId, second = state.Rings[1].StableRingId;
            Assert.That(station.AddMount(first, out var error), Is.True, error);
            Assert.That(station.AddMount(second, out error), Is.True, error);
            Assert.That(station.InstallLinkPair(first, 1, second, 0, out error), Is.True, error);
            Assert.That(station.InstallModule(OrbitalModuleKind.LaserSword, second, 1, out error), Is.True, error);
            for (int i = 0; i < 5; i++) yield return null;
            Assert.That(station.Modules.Count, Is.EqualTo(4));
            Assert.That(state.Validate(out error), Is.True, error);
            CheckPaths(station, paths);
            Time.timeScale = 0f;
            Vector3 offset = new(4f, -3f, 0f);
            var mount = station.Rings[1].Mounts[1].Transform;
            Vector3 before = mount.position;
            station.Owner.Transform.position += offset;
            Assert.That(Vector3.Distance(mount.position, before + offset), Is.LessThan(.001f));
            CheckPaths(station, paths);
            var restored = JsonUtility.FromJson<OrbitalRunState>(JsonUtility.ToJson(state));
            Assert.That(restored.Validate(out error), Is.True, error);
            Assert.That(restored.PendingRingCount, Is.Zero);
            Assert.That(restored.Rings[1].CustomPath, Is.EqualTo(paths[1]));
            Assert.That(station.SimulateSectorRestore(), Is.True);
            CheckPaths(station, paths);
            Debug.Log("CustomOrbit PASS 7: LinkPair + LaserSword tick, arc-length placement and local following; rebuild preserves geometry.");

            for (int sectorNumber = 2; sectorNumber <= 3; sectorNumber++)
            {
                var manager = RunStateManager.Instance;
                var choice = Object.FindFirstObjectByType<LevelChoiceManager>();
                var stage = AssetDatabase.FindAssets("t:StageProfileData")
                    .Select(id => AssetDatabase.LoadAssetAtPath<StageProfileData>(AssetDatabase.GUIDToAssetPath(id)))
                    .First(p => p.SectorNumber == sectorNumber);
                var next = new RunSector(sectorNumber, stage, manager.CurrentSector.WorldRule, manager.CurrentSector.LocalAnomaly);
                int oldScene = station.gameObject.scene.handle;
                typeof(LevelChoiceManager).GetMethod("TransitionToSector", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(choice, new object[] { next });
                yield return WaitFor(() => Station() != null && Station().IsInitialized &&
                    Station().gameObject.scene.handle != oldScene && !SceneTransitionOverlay.IsTransitioning);
                station = Station();
                Assert.That(station.State, Is.SameAs(state));
                Assert.That(manager.CurrentSector.SectorNumber, Is.EqualTo(sectorNumber));
                CheckPaths(station, paths);
                Assert.That(station.CustomDrawing, Is.Null);
                Debug.Log($"CustomOrbit PASS 6: actual sector {sectorNumber}, both paths restored without drawing.");
            }

            PlayerPrefs.SetInt(slotKey, (int)OrbitalSlotSymbol.Ring);
            yield return StartRun("Vika");
            station = Station();
            yield return WaitDrawing(station);
            Assert.That(station.State.PendingRingCount, Is.EqualTo(2));
            Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.Ring));
            Draw(station, false);
            yield return WaitDrawing(station);
            Assert.That(station.State.PendingRingCount, Is.EqualTo(1));
            Assert.That(station.Rings.Count, Is.EqualTo(1));
            Assert.That(Time.timeScale, Is.Zero);
            Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.Ring));
            Draw(station, true);
            yield return null;
            Assert.That(station.Rings.Count, Is.EqualTo(2));
            Assert.That(station.State.PendingRingCount, Is.Zero);
            Assert.That(OrbitalSlotMachine.Pending, Is.EqualTo(OrbitalSlotSymbol.None));
            Assert.That(Time.timeScale, Is.EqualTo(1f));
            Debug.Log("CustomOrbit PASS 4: casino/start queue sequential; bonus consumed only after second Confirm.");
        }
        finally
        {
            if (hadSlot) PlayerPrefs.SetInt(slotKey, savedSlot); else PlayerPrefs.DeleteKey(slotKey);
            PlayerPrefs.Save();
            Time.timeScale = 1f;
        }
        yield return new ExitPlayMode();
    }

    private static OrbitalStationRuntime Station() => Object.FindFirstObjectByType<OrbitalStationRuntime>();
    private static IEnumerator StartRun(string name)
    {
        yield return SceneManager.LoadSceneAsync("MainMenu");
        var character = AssetDatabase.FindAssets("t:CharacterData")
            .Select(id => AssetDatabase.LoadAssetAtPath<CharacterData>(AssetDatabase.GUIDToAssetPath(id)))
            .First(c => c != null && c.characterName == name);
        yield return WaitFor(() => Object.FindFirstObjectByType<BunkerRunStarter>() != null);
        RunSelectionManager.Instance.SelectCharacter(character);
        var starter = Object.FindFirstObjectByType<BunkerRunStarter>();
        var cameraRig = (Transform)typeof(BunkerRunStarter).GetField("cameraRig", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(starter);
        starter.StartRun(cameraRig);
        yield return WaitFor(() => SceneManager.GetActiveScene().name == "MVP" && Station() != null &&
            Station().IsInitialized && !SceneTransitionOverlay.IsTransitioning);
    }
    private static IEnumerator WaitFor(Func<bool> predicate)
    {
        float deadline = Time.realtimeSinceStartup + 30f;
        while (!predicate() && Time.realtimeSinceStartup < deadline) yield return null;
        Assert.That(predicate(), Is.True, "Timed out waiting for production flow");
    }
    private static IEnumerator WaitDrawing(OrbitalStationRuntime station) => WaitFor(() =>
        station.CustomDrawing != null && station.CustomDrawing.gameObject.activeInHierarchy);
    private static void Draw(OrbitalStationRuntime station, bool eight)
    {
        var draw = station.CustomDrawing;
        draw.Clear();
        Vector2 Point(int i)
        {
            float t = i * Mathf.PI * 2f / 160;
            return eight ? new Vector2(3.8f * Mathf.Cos(t), 1.8f * Mathf.Sin(2f * t))
                : new Vector2(2.5f * Mathf.Cos(t), 2.5f * Mathf.Sin(t));
        }
        draw.BeginStroke(Point(0));
        for (int i = 1; i < 160; i++) draw.AppendPoint(Point(i));
        draw.EndStroke(Point(0));
        Assert.That(draw.State, Is.EqualTo(CustomOrbitDrawing.PathState.VALID), draw.Notice);
        draw.Confirm();
    }
    private static void CheckPaths(OrbitalStationRuntime station, Vector2[][] paths)
    {
        Assert.That(station.HasPendingCustomRings, Is.False);
        Assert.That(station.Rings.Count, Is.EqualTo(paths.Length));
        for (int i = 0; i < paths.Length; i++)
        {
            var ring = station.Rings[i];
            Assert.That(ring.State.CustomPath, Is.EqualTo(paths[i]));
            Assert.That(ring.Geometry.Type, Is.EqualTo(OrbitalPathType.Custom));
            var path = ring.Geometry.CustomPath;
            for (int m = 0; m < ring.MountCount; m++)
            {
                Vector2 expected = path.PositionAtDistance((ring.Phase / 360f + (float)m / ring.MountCount) * path.TotalPathLength);
                Assert.That(Vector2.Distance(ring.Mounts[m].Transform.localPosition, expected), Is.LessThan(.002f));
            }
        }
    }
}
#endif
