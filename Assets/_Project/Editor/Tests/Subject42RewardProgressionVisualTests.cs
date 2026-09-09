#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Subject42.Combat.OrbitalStation;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

public sealed class Subject42RewardProgressionVisualTests
{
    private static T Field<T>(object obj, string name) => (T)obj.GetType().GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);

    [Test]
    public void ProductionCardsAndMixedPauseContactSheet()
    {
        Directory.CreateDirectory(Subject42RewardProgressionTests.Output);
        var scene = EditorSceneManager.NewPreviewScene();
        var canvasRoot = new GameObject("QA Canvas", typeof(RectTransform), typeof(Canvas));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(canvasRoot, scene);
        var cameraRoot = new GameObject("QA Camera", typeof(Camera));
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(cameraRoot, scene);
        var camera = cameraRoot.GetComponent<Camera>(); camera.scene = scene;
        camera.transform.position = new Vector3(0, 0, -10); camera.orthographic = true;
        camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = new Color(.025f, .04f, .05f);
        var canvas = canvasRoot.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = camera;
        var previousLocalization = LocalizationService.Instance;
        var locRoot = new GameObject("QA Localization");
        UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(locRoot, scene);
        var loc = locRoot.AddComponent<LocalizationService>();
        typeof(LocalizationService).GetField("table", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(loc, Resources.Load<LocalizationTable>("Localization/LocalizationTable"));
        var locInstance = typeof(LocalizationService).GetField("<Instance>k__BackingField", BindingFlags.NonPublic | BindingFlags.Static);
        locInstance.SetValue(null, loc);
        try
        {
            using var provider = new OrbitalRewardProvider(Subject42RewardProgressionTests.BodyAssets());
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/_UpdateUI/MVPUpgradePanel.prefab");
            var cardPrefab = prefab.GetComponentsInChildren<UpgradeCardView>(true)[0];
            var kinds = new[] { OrbitalRewardKind.Pistol, OrbitalRewardKind.LaserSword, OrbitalRewardKind.ImpulseGun, OrbitalRewardKind.ArcEmitter, OrbitalRewardKind.LinkPair,
                OrbitalRewardKind.AddMount, OrbitalRewardKind.RingCapacity, OrbitalRewardKind.RingPower, OrbitalRewardKind.RingSpeed, OrbitalRewardKind.MaxHealth,
                OrbitalRewardKind.MoveSpeed, OrbitalRewardKind.CoreUpgrade, OrbitalRewardKind.NewRing };
            var cardState = OrbitalRunState.CreateDefault(1);
            cardState.AddMount(1, out _); cardState.UpgradeRingPower(1); cardState.UpgradeRingSpeed(1);
            var capacityRing = cardState.AddRing();
            cardState.AddMount(capacityRing.StableRingId, out _); cardState.AddMount(capacityRing.StableRingId, out _);
            var cards = new System.Collections.Generic.List<GameObject>();
            for (int i = 0; i < kinds.Length; i++)
            {
                var card = Object.Instantiate(cardPrefab, canvasRoot.transform);
                cards.Add(card.gameObject);
                var rect = (RectTransform)card.transform;
                rect.anchorMin = rect.anchorMax = rect.pivot = Vector2.one * .5f;
                rect.sizeDelta = new Vector2(300, 500); rect.localScale = Vector3.one;
                rect.anchoredPosition = new Vector2((i % 5 - 2) * 330, (1 - i / 5) * 535);
                card.Setup(provider.GetDefinition(kinds[i], cardState), _ => { });
                Assert.That(Field<Image>(card, "iconImage").sprite, Is.EqualTo(OrbitalRewardIconResolver.Resolve(provider.GetDefinition(kinds[i], cardState)).Sprite));
            }
            Render(camera, canvasRoot, 1740, 1660, "cards-contact-sheet.png");
            foreach (var card in cards) Object.DestroyImmediate(card);
            var pause = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/PausePanel.prefab"), canvasRoot.transform);
            pause.SetActive(true);
            var view = pause.GetComponent<PauseBuildOverview>();
            var state = OrbitalRunState.CreateDefault(42);
            state.AddMount(1, out _); state.AddMount(1, out _); state.UpgradeRingCapacity(1);
            state.InstallModule(OrbitalModuleKind.LaserSword, 1, 1, out _);
            state.InstallModule(OrbitalModuleKind.ImpulseGun, 1, 2, out _);
            var second = state.AddRing(); state.AddMount(second.StableRingId, out _); state.AddMount(second.StableRingId, out _);
            state.InstallModule(OrbitalModuleKind.ArcEmitter, second.StableRingId, 0, out _);
            state.InstallLinkPair(second.StableRingId, 1, second.StableRingId, 2, out _, out _, out _);
            state.UpgradeRingPower(1); state.UpgradeRingSpeed(1); state.UpgradeRingSpeed(1);
            state.UpgradeCore(); state.UpgradeCore();
            var slots = new RunItemSlots();
            foreach (var body in Subject42RewardProgressionTests.BodyAssets().GroupBy(d => d.upgradeType).Select(g => g.First())) slots.TryAdd(body);
            view.RefreshBuild(state, slots);
            Assert.That(Field<TMP_Text>(view, "ringsText").text, Does.Contain("3 / 4").And.Contain("DAMAGE ×1.25"));
            Assert.That(Field<TMP_Text>(view, "playerText").text, Does.Contain("20 HP").And.Contain("1.10"));
            Render(camera, canvasRoot, 1920, 1080, "pause-mixed-build.png");
        }
        finally
        {
            locInstance.SetValue(null, previousLocalization);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static void Render(Camera camera, GameObject root, int width, int height, string filename)
    {
        ((RectTransform)root.transform).sizeDelta = new Vector2(width, height);
        camera.orthographicSize = height / 2f;
        Canvas.ForceUpdateCanvases();
        foreach (var rect in root.GetComponentsInChildren<RectTransform>()) LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
        var pause = root.GetComponentInChildren<PauseBuildOverview>();
        if (pause != null) typeof(PauseBuildOverview).GetMethod("FitWindow", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(pause, null);
        Canvas.ForceUpdateCanvases();
        foreach (var text in root.GetComponentsInChildren<TMP_Text>())
        {
            text.ForceMeshUpdate();
            if (text.GetComponentInParent<UpgradeCardView>() != null)
                Assert.That(text.isTextOverflowing, Is.False, text.name + ": " + text.text);
        }
        var texture = new RenderTexture(width, height, 24); var old = RenderTexture.active;
        var png = new Texture2D(width, height, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = texture; camera.Render(); RenderTexture.active = texture;
            png.ReadPixels(new Rect(0, 0, width, height), 0, 0); png.Apply();
            File.WriteAllBytes(Subject42RewardProgressionTests.Output + filename, png.EncodeToPNG());
        }
        finally { camera.targetTexture = null; RenderTexture.active = old; Object.DestroyImmediate(texture); Object.DestroyImmediate(png); }
    }
}
#endif
