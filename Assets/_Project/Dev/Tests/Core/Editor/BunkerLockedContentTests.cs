#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class BunkerLockedContentTests
{
    private const string Output = "Artifacts/GeneratedQA/BunkerLockedContent";

    [UnityTest]
    public IEnumerator LockedContentUsesTheApprovedConcealedPresentationInEveryStation()
    {
        EditorSceneManager.OpenScene(BunkerInteractionAuthoring.ScenePath);
        EditorWindow.GetWindow(typeof(Editor).Assembly.GetType("UnityEditor.GameView")).Focus();
        yield return new EnterPlayMode();
        yield return ExerciseStations();
    }

    private static IEnumerator ExerciseStations()
    {
        yield return CoreTestSupport.Await(() => ProductionSceneComposition.Active is { IsReady: true }
            && Object.FindFirstObjectByType<BunkerPlayerLoadoutController>() is { IsReady: true }
            && !SceneTransitionOverlay.IsTransitioning
            && Object.FindFirstObjectByType<BunkerIntroController>() is not { IsPlaying: true });
        Directory.CreateDirectory(Output);
        var panels = Object.FindFirstObjectByType<BunkerPanelManager>();
        var follow = Object.FindFirstObjectByType<CameraFollow>();
        Assert.That(follow.ControlledCamera.targetTexture, Is.Null);
        File.WriteAllText(Output + "/environment.txt", "project=" + Application.dataPath
            + " scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path
            + " unity=" + Application.unityVersion + " pid=" + System.Diagnostics.Process.GetCurrentProcess().Id
            + " camera=" + BunkerInteractionAuthoring.PathOf(follow.ControlledCamera.transform));
        int gold = CurrencyManager.Instance.TotalGold;
        string selectedCharacter = RunSelectionManager.Instance.SelectedCharacter.Id.ToString();
        var open = new Action[] { panels.OpenCharacterSelection, panels.OpenWeaponSelection,
            panels.OpenUpgrade, panels.OpenAnomalyStabilizers };
        for (int station = 0; station < open.Length; station++)
        {
            open[station]();
            yield return null;
            var window = Object.FindObjectsByType<BunkerSelectionWindow>(FindObjectsSortMode.None).Single(w => w.IsOpen);
            var model = Read<BunkerSelectionWindowModel>(window, "model");
            var lockedEntries = model.Entries.Where(e => e.Locked).ToArray();
            Assert.That(lockedEntries, Is.Not.Empty, "Production station " + station + " needs a locked entry for this regression scenario.");
            foreach (var entry in lockedEntries)
            {
                var card = window.GetComponentsInChildren<BunkerSelectionCardView>().Single(c => c.EntryId == entry.Id);
                Assert.That(Read<GameObject>(card, "concealedSubject").activeInHierarchy, Is.True,
                    "Locked cards must use the approved concealed image: " + entry.Id);
                Assert.That(Read<Image>(card, "icon").isActiveAndEnabled, Is.False,
                    "A locked item must not reveal its colored artwork: " + entry.Id);
                card.GetComponent<Button>().onClick.Invoke();
                Assert.That(Read<BunkerSelectionEntryModel>(window, "selected").Id, Is.EqualTo(entry.Id));
                Assert.That(Read<Button>(window, "confirmButton").gameObject.activeInHierarchy, Is.False);
                var detail = Read<BunkerSelectionDetailView>(window, "detailView");
                Assert.That(Read<GameObject>(detail, "concealedSubject").activeInHierarchy, Is.True);
                Assert.That(Read<GameObject>(detail, "featureBlock").activeInHierarchy, Is.False);
                Assert.That(Read<GameObject>(detail, "statsBlock").activeInHierarchy, Is.False);
                Assert.That(Read<GameObject>(detail, "descriptionBlock").activeInHierarchy, Is.False);
                Assert.That(Read<GameObject>(detail, "lockBlock").activeInHierarchy, Is.True);
                Assert.That(Read<BunkerProgressionView>(window, "itemProgression").gameObject.activeInHierarchy, Is.False);
                var scroll = Read<RectTransform>(window, "cardsRoot").GetComponentInParent<ScrollRect>();
                Canvas.ForceUpdateCanvases();
                scroll.verticalNormalizedPosition = 1f - (float)model.Entries.IndexOf(entry) / (model.Entries.Count - 1);
                yield return Capture(station + "-locked-" + entry.Id);
                Assert.That(Read<GameObject>(card, "concealedSubject").GetComponentInChildren<TMP_Text>().canvasRenderer.cull,
                    Is.False, "The selected concealed slot must be inside the rendered scroll viewport: " + entry.Id);
            }
            Read<RectTransform>(window, "cardsRoot").GetComponentInParent<ScrollRect>().verticalNormalizedPosition = 1f;
            window.GetComponentsInChildren<BunkerSelectionCardView>().Single(c => c.EntryId == lockedEntries[0].Id)
                .GetComponent<Button>().onClick.Invoke();
            yield return Capture(station + "-locked");
            var unlocked = model.Entries.First(e => !e.Locked);
            window.GetComponentsInChildren<BunkerSelectionCardView>().Single(c => c.EntryId == unlocked.Id)
                .GetComponent<Button>().onClick.Invoke();
            var unlockedDetail = Read<BunkerSelectionDetailView>(window, "detailView");
            Assert.That(Read<GameObject>(unlockedDetail, "concealedSubject").activeInHierarchy, Is.False);
            var image = unlocked.IsCharacter ? Read<Image>(unlockedDetail, "portrait")
                : Read<Image>(Read<BunkerStationItemDetail>(unlockedDetail, "itemDetail"), "preview");
            Assert.That(image.isActiveAndEnabled, Is.True, "Returning to unlocked content restores its artwork.");
            Assert.That(image.color, Is.EqualTo(unlocked.IconColor));
            yield return Capture(station + "-unlocked");
            panels.CloseAll(false);
            open[station]();
            yield return null;
            Assert.That(CurrencyManager.Instance.TotalGold, Is.EqualTo(gold));
            Assert.That(RunSelectionManager.Instance.SelectedCharacter.Id.ToString(), Is.EqualTo(selectedCharacter));
            panels.CloseAll(false);
        }
    }

    private static IEnumerator Capture(string name)
    {
        Canvas.ForceUpdateCanvases();
        float readyAt = Time.realtimeSinceStartup + .2f;
        yield return CoreTestSupport.Await(() => Time.realtimeSinceStartup >= readyAt);
        string path = Path.GetFullPath(Output + "/" + name + ".png");
        if (File.Exists(path)) File.Delete(path);
        ScreenCapture.CaptureScreenshot(path);
        yield return CoreTestSupport.Await(() => File.Exists(path));
    }

    private static T Read<T>(Object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    [UnityTearDown]
    public IEnumerator Exit()
    {
        if (Application.isPlaying) yield return new ExitPlayMode();
    }
}
#endif
