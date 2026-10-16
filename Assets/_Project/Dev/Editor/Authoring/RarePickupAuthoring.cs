#if UNITY_EDITOR
using System.IO;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

// Explicit authoring only; no asset discovery or UI construction during gameplay.
[InitializeOnLoad]
public static class RarePickupAuthoring
{
    private const string Pickups = "Assets/_Project/prefabs/Pickups/";
    private const string Hud = "Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab";
    private const string Rewards = "Assets/_Project/prefabs/Bootstrap/GameplayRewards.prefab";
    static RarePickupAuthoring() => EditorApplication.update += CheckRequest;

    private static void CheckRequest()
    {
        const string request = "Artifacts/GeneratedQA/RarePickups/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        File.Delete(request);
        Build();
        File.WriteAllText("Artifacts/GeneratedQA/RarePickups/author.done", "PASS");
    }

    [MenuItem("Tools/Subject42/Author Rare Pickups")]
    public static void Build()
    {
        ConfigurePickup("p_medKit1", RarePickup.PickupKind.MedKit);
        ConfigurePickup("p_magnet1", RarePickup.PickupKind.Magnet);
        EditPrefab(Rewards, root =>
        {
            var data = new SerializedObject(root.GetComponent<ExperienceManager>());
            data.FindProperty("medKitPrefab").objectReferenceValue = LoadPickup("p_medKit1").GetComponent<RarePickup>();
            data.FindProperty("magnetPrefab").objectReferenceValue = LoadPickup("p_magnet1").GetComponent<RarePickup>();
            data.FindProperty("healingPopupPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/DamagePopup.prefab").GetComponent<DamagePopup>();
            data.ApplyModifiedPropertiesWithoutUndo();
        });
        EditPrefab(Hud, root =>
        {
            var hud = root.GetComponentInChildren<HUDManager>(true);
            // Idempotent: leave an already authored indicator and its local tuning intact.
            if (root.GetComponentInChildren<MagnetHudView>(true) != null) return;
            var panel = new GameObject("MagnetHUD", typeof(RectTransform), typeof(CanvasGroup), typeof(MagnetHudView));
            panel.layer = 5;
            var rect = (RectTransform)panel.transform;
            rect.SetParent(hud.transform, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(24f, -228f);
            rect.sizeDelta = new Vector2(106f, 32f);
            var group = panel.GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.interactable = group.blocksRaycasts = false;

            var iconObject = new GameObject("Icon", typeof(RectTransform), typeof(Image));
            iconObject.layer = 5;
            var iconRect = (RectTransform)iconObject.transform;
            iconRect.SetParent(rect, false);
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0f, 0.5f);
            iconRect.anchoredPosition = new Vector2(16f, 0f);
            iconRect.sizeDelta = new Vector2(28f, 28f);
            var icon = iconObject.GetComponent<Image>();
            icon.sprite = LoadPickup("p_magnet1").GetComponentInChildren<SpriteRenderer>(true).sprite;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var textObject = new GameObject("RemainingTime", typeof(RectTransform), typeof(TextMeshProUGUI));
            textObject.layer = 5;
            var timeRect = (RectTransform)textObject.transform;
            timeRect.SetParent(rect, false);
            timeRect.anchorMin = new Vector2(0f, 0f);
            timeRect.anchorMax = new Vector2(1f, 1f);
            timeRect.offsetMin = new Vector2(38f, 0f);
            timeRect.offsetMax = Vector2.zero;
            var text = textObject.GetComponent<TextMeshProUGUI>();
            var existingText = new SerializedObject(hud).FindProperty("timerText").objectReferenceValue as TMP_Text;
            text.font = existingText.font;
            text.fontSharedMaterial = existingText.fontSharedMaterial;
            text.fontSize = 20f;
            text.color = new Color(0.8f, 1f, 0.95f, 1f);
            text.alignment = TextAlignmentOptions.MidlineLeft;
            text.raycastTarget = false;
            text.text = "0:30";
            var view = new SerializedObject(panel.GetComponent<MagnetHudView>());
            view.FindProperty("hud").objectReferenceValue = hud;
            view.FindProperty("visibility").objectReferenceValue = group;
            view.FindProperty("remainingTime").objectReferenceValue = text;
            view.ApplyModifiedPropertiesWithoutUndo();
        });
        AssetDatabase.SaveAssets();
    }

    private static GameObject LoadPickup(string name) => AssetDatabase.LoadAssetAtPath<GameObject>(Pickups + name + ".prefab");
    private static void ConfigurePickup(string name, RarePickup.PickupKind kind) =>
        EditPrefab(Pickups + name + ".prefab", root =>
        {
            var pickup = root.GetComponent<RarePickup>() ?? root.AddComponent<RarePickup>();
            var data = new SerializedObject(pickup);
            data.FindProperty("kind").enumValueIndex = (int)kind;
            // Match the authored ordinary XP movement defaults, with radius supplied by the player.
            data.FindProperty("magnetSpeed").floatValue = 5f;
            data.ApplyModifiedPropertiesWithoutUndo();
            foreach (var light in root.GetComponentsInChildren<Light2D>(true))
                light.transform.localPosition = Vector3.zero;
        });

    private static void EditPrefab(string path, System.Action<GameObject> edit)
    {
        var root = PrefabUtility.LoadPrefabContents(path);
        try { edit(root); PrefabUtility.SaveAsPrefabAsset(root, path); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }
}
#endif
