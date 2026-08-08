using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class PolishFootballTerminalScene
{
    private const string ScenePath = "Assets/_Project/Scenes/MainMenu.unity";
    private const string ReferencePrefabPath =
        "Assets/_Project/prefabs/Envir/BunkerRoom/_newItems/p_bunkerWeaponShowcase.prefab";
    [MenuItem("Tools/Bunker/Polish Football Terminal")]
    public static void PolishAndSave()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || scene.path != ScenePath)
        {
            Debug.LogError($"[FootballTerminalPolish] Expected active scene '{ScenePath}'.");
            return;
        }

        BunkerStation terminal = Object.FindObjectsByType<BunkerStation>(
                FindObjectsInactive.Include, FindObjectsSortMode.None)
            .FirstOrDefault(station => station.name == "FootballChallengeTerminal");
        GameObject referencePrefab = AssetDatabase.LoadAssetAtPath<GameObject>(ReferencePrefabPath);

        if (terminal == null || referencePrefab == null)
        {
            Debug.LogError("[FootballTerminalPolish] Terminal or weapon-station reference prefab is missing.");
            return;
        }

        Transform referenceGraphic = referencePrefab.transform.Find("Graphic");
        BunkerInteractableCollider referenceInteraction =
            referencePrefab.GetComponentInChildren<BunkerInteractableCollider>(true);
        BunkerInteractableCollider terminalInteraction =
            terminal.GetComponentInChildren<BunkerInteractableCollider>(true);

        if (referenceGraphic == null || referenceInteraction == null || terminalInteraction == null)
        {
            Debug.LogError("[FootballTerminalPolish] Reference or terminal interaction structure is incomplete.");
            return;
        }

        Transform oldVisual = terminal.transform.Find("Visual");
        if (oldVisual != null)
            Undo.DestroyObjectImmediate(oldVisual.gameObject);

        GameObject visual = Object.Instantiate(referenceGraphic.gameObject);
        visual.name = "Visual";
        Undo.RegisterCreatedObjectUndo(visual, "Add Football Terminal Visual");
        visual.transform.SetParent(terminal.transform, false);
        visual.transform.localPosition = Vector3.zero;
        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localScale = Vector3.one;

        SpriteRenderer placeholder = terminal.GetComponent<SpriteRenderer>();
        if (placeholder != null)
            Undo.DestroyObjectImmediate(placeholder);

        SortingGroup referenceSorting = referencePrefab.GetComponent<SortingGroup>();
        SortingGroup terminalSorting = terminal.GetComponent<SortingGroup>();
        if (referenceSorting != null)
        {
            if (terminalSorting == null)
                terminalSorting = Undo.AddComponent<SortingGroup>(terminal.gameObject);

            EditorUtility.CopySerialized(referenceSorting, terminalSorting);
        }

        BunkerHoverOutline hover = terminal.GetComponent<BunkerHoverOutline>();
        if (hover == null)
            hover = Undo.AddComponent<BunkerHoverOutline>(terminal.gameObject);

        SerializedObject hoverData = new(hover);
        hoverData.FindProperty("autoFindRenderers").boolValue = true;
        hoverData.FindProperty("targetRenderers").arraySize = 0;
        hoverData.ApplyModifiedPropertiesWithoutUndo();

        Collider2D referenceCollider = referenceInteraction.GetComponent<Collider2D>();
        Collider2D terminalCollider = terminalInteraction.GetComponent<Collider2D>();
        if (referenceCollider is BoxCollider2D && terminalCollider is BoxCollider2D)
            EditorUtility.CopySerialized(referenceCollider, terminalCollider);

        terminalInteraction.gameObject.layer = referenceInteraction.gameObject.layer;

        SerializedObject interactionData = new(terminalInteraction);
        interactionData.FindProperty("sourceRoot").objectReferenceValue = terminal.gameObject;
        interactionData.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject stationData = new(terminal);
        stationData.FindProperty("progressionEnabled").boolValue = false;
        stationData.ApplyModifiedPropertiesWithoutUndo();

        int rendererCount = visual.GetComponentsInChildren<SpriteRenderer>(true).Length;
        if (rendererCount == 0 || terminalInteraction.gameObject.layer != 9)
        {
            Debug.LogError("[FootballTerminalPolish] Validation failed; scene was not saved.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log(
            $"[FootballTerminalPolish] SUCCESS reference=p_bunkerWeaponShowcase; " +
            $"visualRenderers={rendererCount}; hover=BunkerHoverOutline; interactionLayer=9; sceneSaved={scene.path}.");
    }
}
