#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>Read-only inspection of saved production assets, including optional references.</summary>
public static class Subject42AssetReferenceValidator
{
    private const string ProjectRoot = "Assets/_Project";
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".unity", ".prefab", ".asset", ".mat", ".anim", ".controller", ".overrideController", ".playable"
    };
    private static readonly Regex Header = new(@"^--- !u!\d+ &(-?\d+)", RegexOptions.Multiline);
    private static readonly Regex Reference = new(@"\{fileID:\s*(-?\d+)([^}]*)\}");
    private static readonly Regex GuidValue = new(@"guid:\s*([0-9a-fA-F]{32})");

    public static bool IsDevPath(string path) =>
        path.Replace('\\', '/').StartsWith(ProjectRoot + "/Dev/", StringComparison.OrdinalIgnoreCase);

    // AssetDatabase paths stay logical in diagnostics; disk reads follow the installed package location.
    public static string GetPhysicalAssetPath(string assetPath)
    {
        string normalized = assetPath.Replace('\\', '/');
        if (normalized.StartsWith("Packages/", StringComparison.OrdinalIgnoreCase))
        {
            PackageInfo package = PackageInfo.FindForAssetPath(normalized);
            if (package != null && !string.IsNullOrEmpty(package.resolvedPath) &&
                normalized.StartsWith(package.assetPath + "/", StringComparison.OrdinalIgnoreCase))
                return Path.GetFullPath(Path.Combine(package.resolvedPath,
                    normalized.Substring(package.assetPath.Length + 1)));
        }
        return Path.GetFullPath(assetPath);
    }

    public static string[] GetProductionRoots()
    {
        // Canonical authoring roots include unreferenced production prefabs/configs/scenes.
        // Artist/importer source assets outside these folders are inspected only when consumed.
        // Enabled build scenes are included separately so a mistakenly enabled Dev scene fails.
        return AssetDatabase.FindAssets("", new[] { ProjectRoot + "/Scenes", ProjectRoot + "/prefabs", ProjectRoot + "/Data" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Where(path => !IsDevPath(path) && IsRootExtension(path))
            .Concat(EditorBuildSettings.scenes.Where(scene => scene.enabled).Select(scene => scene.path))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal).ToArray();
    }

    private static bool IsRootExtension(string path)
    {
        string extension = Path.GetExtension(path);
        return extension.Equals(".unity", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".prefab", StringComparison.OrdinalIgnoreCase) ||
            extension.Equals(".asset", StringComparison.OrdinalIgnoreCase);
    }

    public static string[] GetInspectionPaths(string[] roots) =>
        AssetDatabase.GetDependencies(roots, true).Concat(roots)
            .Where(path => !IsDevPath(path) && Extensions.Contains(Path.GetExtension(path)))
            .Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(path => path, StringComparer.Ordinal).ToArray();

    public static Subject42ValidationReport Validate(string[] roots, Subject42ValidationReport report = null)
    {
        report ??= new Subject42ValidationReport();
        foreach (string root in roots)
        {
            if (IsDevPath(root))
                report.Add(Subject42ValidationSeverity.Error, "DEV_PRODUCTION_ROOT", $"Production root '{root}' is Dev content.");
            foreach (string dependency in AssetDatabase.GetDependencies(root, true))
                if (IsDevPath(dependency))
                    report.Add(Subject42ValidationSeverity.Error, "DEV_DEPENDENCY", $"'{root}' -> '{dependency}'.");
        }

        var fileIds = new Dictionary<string, HashSet<long>>(StringComparer.OrdinalIgnoreCase);
        foreach (string path in GetInspectionPaths(roots))
        {
            try
            {
                InspectSavedReferences(path, fileIds, report);
                InspectObjects(path, report);
            }
            catch (Exception exception)
            {
                report.Add(Subject42ValidationSeverity.Error, "ASSET_INSPECTION",
                    $"Could not inspect '{path}': {exception.Message}");
            }
        }
        return report;
    }

    private static void InspectSavedReferences(string path, Dictionary<string, HashSet<long>> cache,
        Subject42ValidationReport report)
    {
        string physicalPath = GetPhysicalAssetPath(path);
        if (!File.Exists(physicalPath))
        {
            report.Add(Subject42ValidationSeverity.Error, "ASSET_MISSING", $"Asset '{path}' does not exist.");
            return;
        }
        string text = File.ReadAllText(physicalPath);
        if (!text.StartsWith("%YAML", StringComparison.Ordinal)) return; // Binary assets use SerializedObject below.
        HashSet<long> localIds = ParseFileIds(text);
        string[] lines = text.Split('\n');
        for (int index = 0; index < lines.Length; index++)
        {
            string line = lines[index];
            bool script = line.TrimStart().StartsWith("m_Script:", StringComparison.Ordinal);
            // Unity embeds package-version editor metadata in materials. It is not a runtime script.
            if (script && Path.GetExtension(path).Equals(".mat", StringComparison.OrdinalIgnoreCase)) continue;
            foreach (Match reference in Reference.Matches(line))
            {
                long id = long.Parse(reference.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);
                string location = $"'{path}' line {index + 1}";
                if (id == 0)
                {
                    if (script) report.Add(Subject42ValidationSeverity.Error, "MISSING_SCRIPT", location + ": missing script.");
                    continue; // An explicit null is valid for any optional object reference.
                }
                Match guidMatch = GuidValue.Match(reference.Groups[2].Value);
                if (!guidMatch.Success)
                {
                    if (!localIds.Contains(id))
                        report.Add(Subject42ValidationSeverity.Error, "BROKEN_LOCAL_FILE_ID",
                            location + $": unresolved local fileID {id}.");
                    continue;
                }
                string guid = guidMatch.Groups[1].Value;
                // Engine resources do not have project asset paths; live properties inspect them below.
                if (IsEngineGuid(guid)) continue;
                string targetPath = AssetDatabase.GUIDToAssetPath(guid);
                // The GUID cache can retain a deleted Assets path until the next refresh.
                // Package paths can be virtual, so only project Assets paths use the disk check.
                if (string.IsNullOrEmpty(targetPath) ||
                    (targetPath.StartsWith("Assets/", StringComparison.OrdinalIgnoreCase) && !File.Exists(targetPath)))
                {
                    report.Add(Subject42ValidationSeverity.Error, script ? "BROKEN_SCRIPT_GUID" : "BROKEN_ASSET_GUID",
                        location + $": unresolved GUID {guid}, fileID {id}.");
                    continue;
                }
                if (!GetFileIds(targetPath, cache).Contains(id))
                    report.Add(Subject42ValidationSeverity.Error, "BROKEN_FILE_ID",
                        location + $": unresolved fileID {id} in '{targetPath}' (GUID {guid}).");
            }
        }
    }

    private static bool IsEngineGuid(string guid) =>
        guid.Equals("0000000000000000e000000000000000", StringComparison.OrdinalIgnoreCase) ||
        guid.Equals("0000000000000000f000000000000000", StringComparison.OrdinalIgnoreCase);

    private static HashSet<long> ParseFileIds(string text) => new(Header.Matches(text).Cast<Match>()
        .Select(match => long.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture)));

    private static HashSet<long> GetFileIds(string path, Dictionary<string, HashSet<long>> cache)
    {
        if (cache.TryGetValue(path, out HashSet<long> ids)) return ids;
        ids = new HashSet<long>();
        cache.Add(path, ids);
        // Saved YAML includes hidden/stripped objects which LoadAllAssetsAtPath may omit.
        string physicalPath = GetPhysicalAssetPath(path);
        if (Extensions.Contains(Path.GetExtension(path)) && File.Exists(physicalPath))
        {
            string text = File.ReadAllText(physicalPath);
            if (text.StartsWith("%YAML", StringComparison.Ordinal)) ids.UnionWith(ParseFileIds(text));
        }
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AddFileId(asset, ids);
            if (asset is GameObject gameObject)
                foreach (Transform child in gameObject.GetComponentsInChildren<Transform>(true))
                {
                    AddFileId(child.gameObject, ids);
                    foreach (Component component in child.GetComponents<Component>()) AddFileId(component, ids);
                }
        }
        // Unity exposes this source-prefab identifier outside the YAML object list for
        // authored prefabs and model-prefab roots generated by the native ModelImporter.
        bool authoredPrefab = Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase);
        bool modelPrefab = AssetImporter.GetAtPath(path) is ModelImporter &&
            AssetDatabase.LoadMainAssetAtPath(path) is GameObject modelRoot &&
            PrefabUtility.GetPrefabAssetType(modelRoot) == PrefabAssetType.Model;
        if (authoredPrefab || modelPrefab) ids.Add(100100000);
        return ids;
    }

    private static void AddFileId(Object value, HashSet<long> ids)
    {
        if (value != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string _, out long id)) ids.Add(id);
    }

    private static void InspectObjects(string path, Subject42ValidationReport report)
    {
        if (Path.GetExtension(path).Equals(".unity", StringComparison.OrdinalIgnoreCase))
        {
            Scene scene = default;
            try
            {
                // A separate preview scene reads disk without replacing or saving the user's open scenes.
                scene = EditorSceneManager.OpenPreviewScene(path);
                foreach (GameObject root in scene.GetRootGameObjects()) InspectHierarchy(root, path, report);
            }
            finally { if (scene.IsValid()) EditorSceneManager.ClosePreviewScene(scene); }
            return;
        }
        if (Path.GetExtension(path).Equals(".prefab", StringComparison.OrdinalIgnoreCase))
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (root != null) InspectHierarchy(root, path, report);
            return;
        }
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is GameObject root) InspectHierarchy(root, path, report);
            else InspectSerializedObject(asset, path, report);
        }
    }

    private static void InspectHierarchy(GameObject root, string path, Subject42ValidationReport report)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(child.gameObject) != 0)
                report.Add(Subject42ValidationSeverity.Error, "MISSING_SCRIPT",
                    $"'{path}': missing script on '{child.name}'.");
            InspectSerializedObject(child.gameObject, path, report);
            // Includes all MonoBehaviours, renderers, UI graphics, colliders and inactive children.
            foreach (Component component in child.GetComponents<Component>()) InspectSerializedObject(component, path, report);
        }
    }

    private static void InspectSerializedObject(Object owner, string path, Subject42ValidationReport report)
    {
        // Native importer metadata can retain unresolved transient handles despite valid saved IDs.
        // Its saved GUID/fileID entries are checked above; only typed loaded objects have live fields.
        if (owner == null || owner.GetType() == typeof(Object)) return;
        using var serialized = new SerializedObject(owner);
        SerializedProperty property = serialized.GetIterator();
        while (property.Next(true))
            if (property.propertyType == SerializedPropertyType.ObjectReference &&
                property.objectReferenceValue == null && property.objectReferenceInstanceIDValue != 0)
                report.Add(Subject42ValidationSeverity.Error, "MISSING_OBJECT_REFERENCE",
                    $"'{path}': {owner.GetType().Name}.{property.propertyPath} is unresolved.");
    }
}
#endif
