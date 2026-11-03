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
        var startFlanker = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_Flanker_Start.prefab");
        var startBreacher = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "p_Enemy_Breacher_Start.prefab");
        var spiders = new[] { "White", "Dark", "BigGait", "Blue" }.Select(Spider).ToArray();
        foreach (string guid in AssetDatabase.FindAssets("t:EnemySpawnProfile", new[] { "Assets/_Project/Data/Stages/EnemySpawnProfiles" }))
        {
            var profile = AssetDatabase.LoadAssetAtPath<EnemySpawnProfile>(AssetDatabase.GUIDToAssetPath(guid));
            if (!int.TryParse(profile.name.Replace("ESP_Level_", ""), out int sector) || sector < 1 || sector > StageProfileData.ProductionTierCount) continue;
            var serialized = new SerializedObject(profile);
            var phases = serialized.FindProperty("phases");
            for (int i = 0; i < phases.arraySize; i++)
            {
                var entries = phases.GetArrayElementAtIndex(i).FindPropertyRelative("enemies");
                Remove(entries, startFlanker); Remove(entries, startBreacher);
                if (sector >= 2) Add(entries, flanker, 6f, 0, 2); else Remove(entries, flanker);
                if (sector >= 3) Add(entries, breacher, 4f, 0, 3); else Remove(entries, breacher);
                SplitChaserWeight(entries, spiders);
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
            movement.FindProperty("useLabTacticalMovement").boolValue = true;
            instance.GetComponent<EnemyHealth>().maxHealth = pattern == EnemyChaseMovement.ChasePattern.Flanker ? 16f : 32f;
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
            var visual = (Transform)movement.FindProperty("visualRoot").objectReferenceValue;
            while (visual.parent != instance.transform) visual = visual.parent;
            bool isFlanker = pattern == EnemyChaseMovement.ChasePattern.Flanker;
            visual.localScale = Vector3.Scale(visual.localScale, isFlanker ? new Vector3(.85f, 1.15f, 1f) : new Vector3(1.35f, 1.25f, 1f));
            foreach (var sprite in visual.GetComponentsInChildren<SpriteRenderer>(true))
                sprite.color = isFlanker ? new Color(.88f, .42f, 1f, sprite.color.a) : new Color(1f, .4f, .24f, sprite.color.a);
            var timer = instance.AddComponent<EnemyTacticalTimedShield>();
            timer.SpawnChance = isFlanker ? .20f : .25f;
            timer.ShieldTemplate = AssetDatabase.LoadAssetAtPath<GameObject>(Enemies + "BreakableShieldBearer.prefab").GetComponent<EnemyDirectionalShield>();
            timer.Initialize(timer.ShieldTemplate, 3f); // Bake disabled shield parts; runtime never restarts the actor.
            var identity = new SerializedObject(instance.GetComponent<EnemyIdentity>());
            identity.FindProperty("enemyId").stringValue = id;
            identity.ApplyModifiedPropertiesWithoutUndo();
            return PrefabUtility.SaveAsPrefabAsset(instance, path);
        }
        finally { Object.DestroyImmediate(instance); }
    }

    private static void Remove(SerializedProperty entries, GameObject prefab)
    {
        for (int i = entries.arraySize - 1; i >= 0; i--)
            if (entries.GetArrayElementAtIndex(i).FindPropertyRelative("enemyPrefab").objectReferenceValue == prefab) entries.DeleteArrayElementAtIndex(i);
    }
    private static void Add(SerializedProperty entries, GameObject prefab, float weight, int cap, int minimumSector)
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
        entry.FindPropertyRelative("useExactAliveLimit").boolValue = cap > 0;
        entry.FindPropertyRelative("minimumRunLevel").intValue = minimumSector;
    }

    private static GameObject Spider(string kind)
    {
        string target = Enemies + "p_Enemy_Spider" + kind + ".prefab";
        if (AssetDatabase.LoadAssetAtPath<GameObject>(target) == null)
            AssetDatabase.CopyAsset("Assets/_Project/Dev/Labs/OrbitalLab/ArtistComparison/LabSpider" + kind + ".prefab", target);
        return AssetDatabase.LoadAssetAtPath<GameObject>(target);
    }

    private static void SplitChaserWeight(SerializedProperty entries, GameObject[] spiders)
    {
        SerializedProperty original = null;
        float total = 0f;
        for (int i = 0; i < entries.arraySize; i++)
        {
            var entry = entries.GetArrayElementAtIndex(i);
            var prefab = entry.FindPropertyRelative("enemyPrefab").objectReferenceValue as GameObject;
            if (prefab != null && (prefab.name.StartsWith("p_Enemy_classic") || spiders.Contains(prefab)))
            {
                total += entry.FindPropertyRelative("weight").floatValue;
                if (prefab.name.StartsWith("p_Enemy_classic")) original = entry.Copy();
            }
        }
        if (original == null) return;
        float share = total / 5f;
        int cap = original.FindPropertyRelative("maxAliveOfType").intValue;
        bool exact = original.FindPropertyRelative("useExactAliveLimit").boolValue;
        int minimum = original.FindPropertyRelative("minimumRunLevel").intValue;
        original.FindPropertyRelative("weight").floatValue = share;
        foreach (var spider in spiders)
        {
            Add(entries, spider, share, cap, minimum);
            for (int i = 0; i < entries.arraySize; i++)
            {
                var entry = entries.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("enemyPrefab").objectReferenceValue == spider)
                    entry.FindPropertyRelative("useExactAliveLimit").boolValue = exact;
            }
        }
    }
}
#endif
