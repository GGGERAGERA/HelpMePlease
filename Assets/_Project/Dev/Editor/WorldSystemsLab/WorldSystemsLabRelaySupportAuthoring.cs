#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering.Universal;

[InitializeOnLoad]
public static class WorldSystemsLabRelaySupportAuthoring
{
    public const string SupportPath = "Assets/_Project/Dev/Labs/WorldSystemsLab/RelaySupport.prefab";
    static WorldSystemsLabRelaySupportAuthoring() { EditorApplication.update += Poll; }
    private static void Poll()
    {
        const string request = "Artifacts/GeneratedQA/OrbitalRelay/lab.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating || EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(request);
        try { CreateSupportAndPatchLab(); File.WriteAllText("Artifacts/GeneratedQA/OrbitalRelay/lab-result.txt", "SUCCESS"); }
        catch (Exception error) { File.WriteAllText("Artifacts/GeneratedQA/OrbitalRelay/lab-result.txt", error.ToString()); Debug.LogException(error); }
    }
    public static void CreateSupportAndPatchLab()
    {
        var root = new GameObject("Lab Relay Support"); root.SetActive(false);
        var lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsSortMode.None)
            .Where(light => light.enabled && light.lightType == Light2D.LightType.Global).ToArray();
        foreach (var light in lights) light.enabled = false;
        Scene production = EditorSceneManager.OpenPreviewScene("Assets/_Project/Scenes/MainBuild/MVP.unity");
        try
        {
            var hud = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/prefabs/UI/HUD/GameplayHUD.prefab"), root.transform);
            hud.name = "Shared notifications";
            var messages = hud.GetComponentInChildren<RunMessageService>(true);
            var serialized = new SerializedObject(messages);
            var hint = (GameObject)serialized.FindProperty("movementHint").objectReferenceValue;
            Prune(hud.transform, new HashSet<Transform> { messages.transform, messages.View.transform, hint.transform });
            foreach (var group in messages.View.GetComponentsInParent<CanvasGroup>(true))
                if (group.gameObject != messages.View.gameObject) group.alpha = 1f;
            OrbitalRelayAuthoring.Set(messages, "hud", null);
            foreach (var component in hud.GetComponentsInChildren<MonoBehaviour>(true))
                if (component != null && component.GetType().Assembly == typeof(WorldEvent).Assembly &&
                    component is not RunMessageService && component is not RunMessageView && component is not LocalizedText)
                    UnityEngine.Object.DestroyImmediate(component);
            var rewardRoot = new GameObject("Shared reward queue"); rewardRoot.transform.SetParent(root.transform, false);
            var source = production.GetRootGameObjects().SelectMany(go => go.GetComponentsInChildren<UpgradeManager>(true)).First();
            var manager = rewardRoot.AddComponent<UpgradeManager>(); EditorUtility.CopySerialized(source, manager);
            var panelRoot = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/_Project/prefabs/UI/_UpdateUI/MVPUpgradePanel.prefab"), rewardRoot.transform);
            OrbitalRelayAuthoring.Set(manager, "upgradePanelView", panelRoot.GetComponentInChildren<UpgradePanelView>(true));
            OrbitalRelayAuthoring.Set(manager, "upgradeApplier", rewardRoot.GetComponent<UpgradeApplier>());
            rewardRoot.SetActive(false); root.SetActive(true);
            PrefabUtility.SaveAsPrefabAsset(root, SupportPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root); EditorSceneManager.ClosePreviewScene(production);
            foreach (var light in lights) if (light != null) light.enabled = true;
        }
        // Patch this one reference without reserializing other lab components.
        string sourceText = File.ReadAllText(WorldSystemsLabController.ScenePath);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(AssetDatabase.LoadAssetAtPath<GameObject>(SupportPath), out string guid, out long fileId);
        string reference = "  relaySupportPrefab: {fileID: " + fileId + ", guid: " + guid + ", type: 3}";
        if (sourceText.Contains("  relaySupportPrefab:"))
            sourceText = System.Text.RegularExpressions.Regex.Replace(sourceText, @"  relaySupportPrefab: [^\r\n]*", reference);
        else sourceText = sourceText.Replace("  orbitalCharacter:", reference + "\n  orbitalCharacter:");
        File.WriteAllText(WorldSystemsLabController.ScenePath, sourceText);
        AssetDatabase.ImportAsset(WorldSystemsLabController.ScenePath);
        AssetDatabase.SaveAssets();
    }
    private static void Prune(Transform root, HashSet<Transform> targets)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
        {
            var child = root.GetChild(i);
            if (targets.Contains(child)) continue;
            if (targets.Any(target => target.IsChildOf(child))) Prune(child, targets);
            else UnityEngine.Object.DestroyImmediate(child.gameObject);
        }
    }
}
#endif
