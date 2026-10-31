#if UNITY_EDITOR
using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// Explicit prefab/profile authoring only; production never patches its assets.
[InitializeOnLoad]
public static class TacticalEnemyMvpAuthoring
{
    private const string Output = "Artifacts/GeneratedQA/TacticalEnemies";
    private const string Enemies = "Assets/_Project/prefabs/Enemies/";
    static TacticalEnemyMvpAuthoring() => EditorApplication.update += () =>
    {
        const string request = Output + "/author.request";
        if (!File.Exists(request) || EditorApplication.isCompiling || EditorApplication.isUpdating ||
            EditorApplication.isPlayingOrWillChangePlaymode || SessionState.GetBool("Subject42.Verification.AnyRunActive", false)) return;
        File.Delete(request);
        try { Author(); File.WriteAllText(Output + "/author-result.txt", "PASS " + DateTime.UtcNow.ToString("O")); }
        catch (Exception error) { File.WriteAllText(Output + "/author-error.txt", error.ToString()); Debug.LogException(error); }
    };

    [MenuItem("Tools/Subject42/Dev/Authoring/Enemies/Author Tactical MVP Enemies")]
    public static void Author()
    {
        var flanker = Variant("Flanker", false, EnemyChaseMovement.ChasePattern.Flanker);
        var breacher = Variant("Breacher", false, EnemyChaseMovement.ChasePattern.Breacher);
        var startFlanker = Variant("Flanker", true, EnemyChaseMovement.ChasePattern.Flanker);
        var startBreacher = Variant("Breacher", true, EnemyChaseMovement.ChasePattern.Breacher);
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile", new[] { "Assets/_Project/Data/Stages/EnemySpawnProfiles" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            bool opening = profile.name == "ESP_Level_01";
            var serialized = new SerializedObject(profile);
            var phases = serialized.FindProperty("phases");
            for (int i = 0; i < phases.arraySize; i++)
            {
                var entries = phases.GetArrayElementAtIndex(i).FindPropertyRelative("enemies");
                Add(entries, opening ? startFlanker : flanker, 8f, 2);
                Add(entries, opening ? startBreacher : breacher, 5f, 1);
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
    }

    private static GameObject Variant(string id, bool opening, EnemyChaseMovement.ChasePattern pattern)
    {
        string path = Enemies + "p_Enemy_" + id + (opening ? "_Start" : "") + ".prefab";
        var source = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + (opening ? "p_Enemy_classic_start.prefab" : "p_Enemy_classic.prefab"));
        var instance = (GameObject)PrefabUtility.InstantiatePrefab(source);
        try
        {
            instance.name = Path.GetFileNameWithoutExtension(path);
            var movement = new SerializedObject(instance.GetComponent<EnemyChaseMovement>());
            movement.FindProperty("chasePattern").enumValueIndex = (int)pattern;
            if (pattern == EnemyChaseMovement.ChasePattern.Breacher)
            {
                var cue = new GameObject("Ram direction telegraph").AddComponent<LineRenderer>();
                cue.transform.SetParent(instance.transform, false);
                cue.useWorldSpace = true;
                cue.positionCount = 2;
                cue.startWidth = .075f;
                cue.endWidth = .04f;
                cue.startColor = new Color(1f, .22f, .16f, .9f);
                cue.endColor = new Color(1f, .65f, .2f, .65f);
                cue.sortingOrder = 20;
                // Keep the turret's red line language readable outside local lights.
                cue.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Orbital/Visual.mat");
                cue.enabled = false;
                movement.FindProperty("ramTelegraph").objectReferenceValue = cue;
            }
            movement.ApplyModifiedPropertiesWithoutUndo();
            var identity = new SerializedObject(instance.GetComponent<EnemyIdentity>());
            identity.FindProperty("enemyId").stringValue = id;
            identity.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private static void Add(SerializedProperty entries, GameObject prefab, float weight, int cap)
    {
        SerializedProperty entry = null;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var candidate = entries.GetArrayElementAtIndex(i);
            if (candidate.FindPropertyRelative("enemyPrefab").objectReferenceValue == prefab) { entry = candidate; break; }
        }
        if (entry == null)
        {
            entries.InsertArrayElementAtIndex(entries.arraySize);
            entry = entries.GetArrayElementAtIndex(entries.arraySize - 1);
        }
        entry.FindPropertyRelative("enemyPrefab").objectReferenceValue = prefab;
        entry.FindPropertyRelative("weight").floatValue = weight;
        entry.FindPropertyRelative("maxAliveOfType").intValue = cap;
        entry.FindPropertyRelative("useExactAliveLimit").boolValue = true;
        entry.FindPropertyRelative("minimumRunLevel").intValue = 1;
    }
}
#endif
