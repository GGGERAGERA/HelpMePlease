#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Subject42.Prototype.OrbitalCombatLab.Integration.Editor
{
    public static class OrbitalIntegrationSandboxBuilder
    {
        public const string SourceScene = "Assets/_Project/Scenes/MVP.unity";
        public const string TargetScene =
            "Assets/_Project/Prototype/OrbitalCombatLab/Integration/OrbitalIntegrationSandbox.unity";

        [InitializeOnLoadMethod]
        private static void EnsureFirstSandboxCopyExists()
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null) return;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode &&
                    AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) == null)
                    Build();
            };
        }

        [MenuItem("Tools/Orbital Combat Lab/Build Integration Sandbox")]
        public static void Build()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                Debug.LogWarning("[OrbitalIntegration] Stop Play Mode before rebuilding the sandbox.");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(SourceScene) == null)
            {
                Debug.LogError($"[OrbitalIntegration] Production scene not found: {SourceScene}");
                return;
            }

            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(TargetScene) != null)
                AssetDatabase.DeleteAsset(TargetScene);
            if (!AssetDatabase.CopyAsset(SourceScene, TargetScene))
            {
                Debug.LogError("[OrbitalIntegration] Could not create the isolated scene copy.");
                return;
            }
            AssetDatabase.ImportAsset(TargetScene, ImportAssetOptions.ForceSynchronousImport);

            Scene scene = EditorSceneManager.OpenScene(TargetScene, OpenSceneMode.Single);
            CharacterMovement2D player = Object.FindFirstObjectByType<CharacterMovement2D>(FindObjectsInactive.Include);
            Camera camera = Camera.main != null
                ? Camera.main
                : Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);

            GameObject root = new("ORBITAL INTEGRATION SANDBOX (LAB ONLY)");
            OrbitalIntegrationSandboxAdapter adapter = root.AddComponent<OrbitalIntegrationSandboxAdapter>();
            OrbitalCombatLabController controller = root.AddComponent<OrbitalCombatLabController>();
            controller.IntegrationMode = true;
            controller.IntegrationPlayer = player != null ? player.transform : null;
            controller.IntegrationCamera = camera;
            controller.IntegrationCameraOverride = false;
            adapter.ConfigureEditor(controller, player, camera);

            EditorUtility.SetDirty(root);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, TargetScene);
            AssetDatabase.SaveAssets();
            Selection.activeObject = root;
            Debug.Log($"[OrbitalIntegration] Built isolated sandbox from {SourceScene}: {TargetScene}; " +
                $"player={(player != null ? player.name : "MISSING")}; camera={(camera != null ? camera.name : "MISSING")}");
        }
    }
}
#endif
