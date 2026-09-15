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
