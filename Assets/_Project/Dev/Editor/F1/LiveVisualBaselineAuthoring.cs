#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Reads Git objects directly. Historical assets are never imported into the current project.
public sealed class LiveVisualBaselineAuthoring : EditorWindow
{
    public const string OriginalRevision = "19b6c01af57f91a3cb0248e5767079e50a1f2859";
    public const string OutputPath = "Assets/_Project/Data/World/Presentation/LiveVisualOriginal.json";
    private string revision = OriginalRevision;
    private string status;

    [MenuItem("Tools/Subject42/Visual Studio/Original Baseline from Git")]
    private static void Open() => GetWindow<LiveVisualBaselineAuthoring>("Git Visual Baseline");

    private void OnGUI()
    {
        EditorGUILayout.HelpBox("Original reads an explicit Git commit. This does not revert the working tree. " +
            "The default commit predates CombatHierarchy; its earlier uncommitted QA normalization is not a Git snapshot.", MessageType.Info);
        revision = EditorGUILayout.TextField("Git revision", revision);
        if (GUILayout.Button("Export Original Baseline"))
        {
            try
            {
                string json = Export(revision);
                File.WriteAllText(OutputPath, json + "\n", new UTF8Encoding(false));
                AssetDatabase.ImportAsset(OutputPath);
                status = "Saved " + OutputPath;
            }
            catch (Exception exception) { status = exception.Message; UnityEngine.Debug.LogException(exception); }
        }
        EditorGUILayout.LabelField(status ?? "", EditorStyles.wordWrappedLabel);
    }

    public static string Export(string requestedRevision)
    {
        if (string.IsNullOrWhiteSpace(requestedRevision) ||
            !Regex.IsMatch(requestedRevision, @"\A[a-zA-Z0-9_./^~{}@-]+\z"))
            throw new ArgumentException("Enter an explicit Git revision; shell expressions are not accepted.");
        string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
        return new Exporter(root, requestedRevision).Run();
    }

    [Serializable] private sealed class Snapshot
    {
        public int Version = 1;
        public string Name = "Original — Git before CombatHierarchy";
        public string BaselineRevision;
        public List<Entry> Values = new();
        public List<MaterialSource> MaterialSources = new();
        public List<Blob> SourceBlobs = new();
        public List<string> Notes = new();
    }
    [Serializable] private sealed class Entry
    {
        public string Scope = "Type";
        public string Target, Property;
        public float Value;
    }
    [Serializable] private sealed class MaterialSource
    {
        public string Target, AssetPath, ShaderName;
    }
    [Serializable] private sealed class Blob { public string AssetPath, GitBlob; }

    private sealed class Record
    {
        public string Id, Type, Raw, ParentOverride;
        public Func<string, string> Resolve;
        public Dictionary<string, string> Overrides = new();
        private readonly Dictionary<string, string> fieldCache = new();
        public string Field(string key)
        {
            if (Overrides.TryGetValue(key, out string value)) return value;
            if (fieldCache.TryGetValue(key, out value)) return value;
            Match match = Regex.Match(Raw, @"(?m)^\s*" + Regex.Escape(key) + @":\s*([^\r\n]*)");
            return fieldCache[key] = match.Success ? match.Groups[1].Value : null;
        }
        public string Reference(string key)
        {
            Match match = Regex.Match(Field(key) ?? "", @"fileID: (-?\d+)");
            return match.Success && match.Groups[1].Value != "0" ? Resolve(match.Groups[1].Value) : null;
        }
        public Record Clone(string prefix) => new()
        {
            Id = prefix + Id, Type = Type, Raw = Raw,
            Resolve = id => prefix + Resolve(id),
            ParentOverride = ParentOverride == null ? null : ParentOverride == "0" ? "0" : prefix + ParentOverride,
            Overrides = new Dictionary<string, string>(Overrides)
        };
    }

    private sealed class Exporter
    {
        private readonly string root, revision;
        private readonly Snapshot result = new();
        private readonly Dictionary<string, string> guids = new();
        private readonly Dictionary<string, string> texts = new();
        private readonly Dictionary<string, Dictionary<string, Record>> documents = new();
        private readonly Dictionary<string, Dictionary<string, string>> documentAliases = new();
        private readonly HashSet<string> resolving = new();
        private readonly HashSet<string> noted = new();
        private readonly Dictionary<string, string> blobs = new();
        private Process blobReader;
        private StreamWriter blobRequests;
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;

        public Exporter(string root, string revision)
        {
            this.root = root;
            this.revision = Git("rev-parse --verify " + Quote(revision + "^{commit}")).Trim();
            result.BaselineRevision = this.revision;
            foreach (string line in Git("ls-tree -r " + this.revision + " -- Assets Packages").Split('\n'))
            {
                Match match = Regex.Match(line.TrimEnd('\r'), @"^\d+ blob ([a-f0-9]+)\t(.+)$");
                if (match.Success) blobs[match.Groups[2].Value] = match.Groups[1].Value;
            }
        }

        public string Run()
        {
            try { return RunCore(); }
            finally
            {
                blobRequests?.Dispose();
                if (blobReader != null)
                {
                    if (!blobReader.WaitForExit(3000)) blobReader.Kill();
                    blobReader.Dispose();
                }
            }
        }

        private string RunCore()
        {
            foreach (string line in Git("grep " + Quote("^guid: ") + " " + revision + " -- Assets").Split('\n'))
            {
                Match match = Regex.Match(line.TrimEnd('\r'), @"^[^:]+:(.*\.meta):guid: ([a-f0-9]{32})$");
                if (match.Success) guids[match.Groups[2].Value] = match.Groups[1].Value[..^5];
            }
            string[] prefabRoots = { "Enemies/", "fx/", "Orbital/", "Environment/Props/", "Weapons/" };
            var paths = blobs.Keys.Where(p => p == "Assets/_Project/Scenes/MainBuild/MVP.unity" ||
                p.EndsWith(".prefab", StringComparison.Ordinal) && prefabRoots.Any(folder =>
                    p.StartsWith("Assets/_Project/prefabs/" + folder, StringComparison.Ordinal)))
                .OrderBy(p => p, StringComparer.Ordinal);
            foreach (string path in paths) ExportDocument(path);
            ExportConfig("Assets/_Project/Data/Orbital/OrbitalPresentationConfig.asset");
            ExportRecognizedCodeValues();
            result.Notes.Add("Values are extracted from Git commit " + revision + "; Current is captured separately from the working tree.");
            result.Notes.Add("The CombatHierarchy QA before capture contained earlier uncommitted normalization. It is not identical to this Git baseline.");
            result.Notes.Add("Coverage: production MVP scene and Enemies, fx, Orbital, Environment/Props, Weapons prefabs. Obsolete full mine-map prefabs, bunker/lab scenes and other prefab families are excluded; uncovered values must remain marked unavailable for Original.");
            result.Notes.Add("Only serialized visual values are recorded. Runtime C# constants, shader implementation, geometry and gameplay timing require explicit adapters or a separate Git build; absent values must not be called historical defaults.");
            result.Notes.Add("Particle curve multipliers are recorded without flattening curves. Gradients, bursts, shape/subemitters and renderer texture assignments are not represented by scalar entries.");
            result.Values = result.Values.OrderBy(v => v.Target, StringComparer.Ordinal).ThenBy(v => v.Property, StringComparer.Ordinal).ToList();
            result.MaterialSources = result.MaterialSources.OrderBy(v => v.Target, StringComparer.Ordinal).ToList();
            foreach (string path in texts.Keys.OrderBy(p => p, StringComparer.Ordinal))
                result.SourceBlobs.Add(new Blob { AssetPath = path, GitBlob = blobs[path] });
            return JsonUtility.ToJson(result, true);
        }

        private string Git(string arguments)
        {
            using var process = new Process { StartInfo = new ProcessStartInfo("git", "-c core.quotepath=false " + arguments)
            {
                WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                RedirectStandardOutput = true, RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
            } };
            process.Start();
            var errorRead = process.StandardError.ReadToEndAsync();
            string output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            string error = errorRead.GetAwaiter().GetResult();
            if (process.ExitCode != 0) throw new InvalidOperationException("Git baseline extraction failed: " + error.Trim());
            return output;
        }
        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        private string Text(string path)
        {
            if (texts.TryGetValue(path, out string text)) return text;
            if (!blobs.TryGetValue(path, out string hash)) throw new InvalidOperationException("File is absent from Git baseline: " + path);
            if (blobReader == null)
            {
                blobReader = new Process { StartInfo = new ProcessStartInfo("git", "cat-file --batch")
                { WorkingDirectory = root, UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardInput = true, RedirectStandardOutput = true } };
                blobReader.Start();
                blobRequests = new StreamWriter(blobReader.StandardInput.BaseStream, new UTF8Encoding(false));
            }
            blobRequests.WriteLine(hash); blobRequests.Flush();
            var output = blobReader.StandardOutput.BaseStream;
            var header = new StringBuilder();
            int next;
            while ((next = output.ReadByte()) >= 0 && next != 10 && header.Length < 256) header.Append((char)next);
            string[] fields = header.ToString().Split(' ');
            if (fields.Length != 3 || fields[1] != "blob" || !int.TryParse(fields[2], out int size) || size < 0 || size > 64 * 1024 * 1024)
                throw new InvalidOperationException("Invalid or oversized Git blob: " + path);
            var bytes = new byte[size];
            int offset = 0;
            while (offset < size)
            {
                int count = output.Read(bytes, offset, size - offset);
                if (count <= 0) throw new EndOfStreamException("Incomplete Git blob: " + path);
                offset += count;
            }
            if (output.ReadByte() != 10) throw new InvalidDataException("Missing Git blob terminator: " + path);
            return texts[path] = Encoding.UTF8.GetString(bytes);
        }
        private string GuidPath(string raw)
        {
            Match match = Regex.Match(raw ?? "", @"guid: ([a-f0-9]{32})");
            return match.Success && guids.TryGetValue(match.Groups[1].Value, out string path) ? path : null;
        }
        private static List<Record> Parse(string raw)
        {
            var records = new List<Record>();
            var matches = Regex.Matches(raw, @"(?m)^--- !u!\d+ &(-?\d+)([^\r\n]*)\r?\n(\w+):\r?\n");
            for (int i = 0; i < matches.Count; i++)
            {
                Match match = matches[i];
                int end = i + 1 < matches.Count ? matches[i + 1].Index : raw.Length;
                records.Add(new Record { Id = match.Groups[1].Value, Type = match.Groups[3].Value,
                    Raw = raw.Substring(match.Index + match.Length, end - match.Index - match.Length), Resolve = id => id });
            }
            return records;
        }

        private Dictionary<string, Record> Document(string path)
        {
            if (documents.TryGetValue(path, out var ready)) return ready;
            if (!resolving.Add(path)) throw new InvalidOperationException("Recursive prefab in baseline: " + path);
            var local = Parse(Text(path));
            var records = new Dictionary<string, Record>();
            var aliases = new Dictionary<string, string>();
            string Resolve(string id) => aliases.TryGetValue(id, out string target) ? target : id;
            foreach (var record in local) record.Resolve = Resolve;
            foreach (var instance in local.Where(r => r.Type == "PrefabInstance"))
            {
                string sourcePath = GuidPath(instance.Field("m_SourcePrefab"));
                if (sourcePath == null) { Note("Unresolved historical prefab source: " + path); continue; }
                string prefix = instance.Id + ":";
                var source = Document(sourcePath);
                string SourceId(string id) => documentAliases[sourcePath].TryGetValue(id, out string mapped) ? mapped : id;
                foreach (var record in source.Values) records[prefix + record.Id] = record.Clone(prefix);
                foreach (var alias in local.Where(r => r.Reference("m_PrefabInstance") == instance.Id))
                {
                    string sourceId = Regex.Match(alias.Field("m_CorrespondingSourceObject") ?? "", @"fileID: (-?\d+)").Groups[1].Value;
                    aliases[alias.Id] = prefix + SourceId(sourceId);
                }
                string parent = instance.Reference("m_TransformParent");
                foreach (var transform in source.Values.Where(r => IsTransform(r) && Parent(r) == null))
                    records[prefix + transform.Id].ParentOverride = parent ?? "0";
                foreach (Match modification in Regex.Matches(instance.Raw,
                    @"(?m)^\s*- target: \{fileID: (-?\d+), guid: [a-f0-9]+, type: \d+\}\r?\n\s*propertyPath: ([^\r\n]+)\r?\n\s*value:([^\r\n]*)\r?\n\s*objectReference: ([^\r\n]+)"))
                {
                    if (!records.TryGetValue(prefix + SourceId(modification.Groups[1].Value), out var record)) continue;
                    string property = modification.Groups[2].Value.Trim();
                    string scalar = modification.Groups[3].Value.Trim();
                    record.Overrides[property] = scalar.Length > 0 ? scalar : modification.Groups[4].Value.Trim();
                }
                string Removed(string field) => Regex.Match(instance.Raw, @"(?ms)^    " + field + @":\r?\n(.*?)(?=^    \w|\z)").Groups[1].Value;
                foreach (Match removed in Regex.Matches(Removed("m_RemovedComponents"), @"fileID: (-?\d+)"))
                    records.Remove(prefix + SourceId(removed.Groups[1].Value));
                var removedObjects = Regex.Matches(Removed("m_RemovedGameObjects"), @"fileID: (-?\d+)").Cast<Match>()
                    .Select(m => prefix + SourceId(m.Groups[1].Value)).ToHashSet();
                bool expanded;
                do
                {
                    expanded = false;
                    foreach (var transform in records.Values.Where(IsTransform))
                    {
                        string parentId = Parent(transform);
                        if (parentId != null && records.TryGetValue(parentId, out var removedParent) &&
                            removedObjects.Contains(removedParent.Reference("m_GameObject")) && removedObjects.Add(transform.Reference("m_GameObject"))) expanded = true;
                    }
                } while (expanded);
                foreach (string id in records.Values.Where(r => removedObjects.Contains(r.Id) || removedObjects.Contains(r.Reference("m_GameObject"))).Select(r => r.Id).ToArray()) records.Remove(id);
            }
            foreach (var record in local.Where(r => r.Type == "SceneRoots" || r.Type != "PrefabInstance" && r.Field("m_CorrespondingSourceObject")?.Contains("fileID: 0") == true))
                records[record.Id] = record;
            resolving.Remove(path);
            documentAliases[path] = aliases;
            return documents[path] = records;
        }
        private static bool IsTransform(Record record) => record.Type is "Transform" or "RectTransform";
        private static string Parent(Record record) => record.ParentOverride == "0" ? null : record.ParentOverride ?? record.Reference("m_Father");
        private void Note(string text) { if (noted.Add(text)) result.Notes.Add(text); }

        private void ExportDocument(string path)
        {
            var records = Document(path);
            var transforms = records.Values.Where(IsTransform).ToDictionary(r => r.Id);
            var byObject = transforms.Values.Where(r => r.Reference("m_GameObject") != null)
                .GroupBy(r => r.Reference("m_GameObject")).ToDictionary(g => g.Key, g => g.First());
            var roots = transforms.Values.Where(r => Parent(r) == null).ToList();
            var sceneRoots = records.Values.FirstOrDefault(r => r.Type == "SceneRoots");
            if (sceneRoots != null)
            {
                var rootIds = Regex.Matches(sceneRoots.Raw, @"fileID: (-?\d+)").Cast<Match>()
                    .Select(m => sceneRoots.Resolve(m.Groups[1].Value)).ToList();
                roots = roots.OrderBy(r => rootIds.IndexOf(r.Id)).ToList();
            }
            var siblings = transforms.Values.GroupBy(r => Parent(r) ?? "0")
                .ToDictionary(g => g.Key, g => g.ToList());
            var childrenByParent = new Dictionary<string, List<string>>();
            foreach (var parent in transforms.Values)
            {
                string children = Regex.Match(parent.Raw, @"(?ms)^  m_Children:\s*\r?\n(.*?)(?=^  \w|\z)").Groups[1].Value;
                childrenByParent[parent.Id] = Regex.Matches(children, @"fileID: (-?\d+)").Cast<Match>()
                    .Select(m => parent.Resolve(m.Groups[1].Value)).Where(transforms.ContainsKey).ToList();
            }
            var ordinals = new Dictionary<string, int>();
            foreach (var group in records.Values.GroupBy(r => (r.Reference("m_GameObject"),
                r.Type == "MonoBehaviour" && r.Field("m_LightType") != null ? "Light2D" : r.Type)))
            {
                int index = 0;
                foreach (var record in group) ordinals[record.Id] = index++;
            }
            string NodeName(Record transform)
            {
                string id = transform.Reference("m_GameObject");
                return id != null && records.TryGetValue(id, out var go) ? go.Field("m_Name")?.Trim('"') ?? "?" : "?";
            }
            int Sibling(Record transform)
            {
                string parentId = Parent(transform);
                if (parentId == null) return roots.IndexOf(transform);
                if (childrenByParent.TryGetValue(parentId, out var ids))
                {
                    int index = ids.IndexOf(transform.Id);
                    if (index >= 0) return index;
                }
                if (int.TryParse(transform.Field("m_RootOrder"), out int order)) return order;
                return siblings[parentId].IndexOf(transform);
            }
            string RelativePath(Record transform, HashSet<string> seen)
            {
                if (!seen.Add(transform.Id)) throw new InvalidOperationException("Transform cycle in " + path);
                string parentId = Parent(transform);
                if (parentId == null) return path.EndsWith(".prefab") ? "" : NodeName(transform) + "[" + Sibling(transform) + "]";
                if (!transforms.TryGetValue(parentId, out var parent)) return "?";
                string parentPath = RelativePath(parent, seen);
                return (parentPath.Length == 0 ? "" : parentPath + "/") + NodeName(transform) + "[" + Sibling(transform) + "]";
            }
            foreach (var record in records.Values)
            {
                string component = record.Type == "MonoBehaviour" && record.Field("m_LightType") != null ? "Light2D" : record.Type;
                if (component is not ("SpriteRenderer" or "MeshRenderer" or "LineRenderer" or "TrailRenderer" or "ParticleSystem" or "Light2D" or "Light" or "Camera")) continue;
                string objectId = record.Reference("m_GameObject");
                if (objectId == null || !byObject.TryGetValue(objectId, out var transform)) continue;
                int ordinal = ordinals[record.Id];
                string relativePath = RelativePath(transform, new HashSet<string>());
                if (relativePath.Contains("?")) { Note("Unresolved historical transform paths omitted: " + path); continue; }
                string target = path + "::" + relativePath + "::" + component + ":" + ordinal;
                if (component == "SpriteRenderer")
                {
                    Color(record, "m_Color", target, "sprite.tint");
                    Scalar(record, "m_SortingOrder", target, "sprite.order");
                    Scalar(record, "m_FlipX", target, "sprite.flipX"); Scalar(record, "m_FlipY", target, "sprite.flipY");
                }
                if (component is "SpriteRenderer" or "MeshRenderer" or "LineRenderer" or "TrailRenderer") ExportMaterial(record, target);
                if (component is "LineRenderer" or "TrailRenderer")
                {
                    Scalar(record, "widthMultiplier", target, component == "LineRenderer" ? "line.width" : "trail.width");
                    if (component == "TrailRenderer") Scalar(record, "m_Time", target, "trail.time");
                    else
                    {
                        Color(record, "key0", target, "line.start"); Color(record, "key1", target, "line.end");
                    }
                }
                if (component is "Light2D" or "Light")
                {
                    Color(record, "m_Color", target, "light.color"); Scalar(record, "m_Intensity", target, "light.intensity");
                    Scalar(record, "m_PointLightInnerRadius", target, "light.innerRadius"); Scalar(record, "m_PointLightOuterRadius", target, "light.outerRadius");
                    Scalar(record, "m_FalloffIntensity", target, "light.falloff"); Scalar(record, "m_ShadowIntensity", target, "light.shadowIntensity");
                }
                if (component == "Camera") Scalar(record, "orthographic size", target, "camera.size");
                if (component == "ParticleSystem")
                {
                    Particle(record, "startLifetime", target, "particles.lifetime"); Particle(record, "startSpeed", target, "particles.speed");
                    Particle(record, "startSize", target, "particles.size"); Particle(record, "rateOverTime", target, "particles.emission");
                    Scalar(record, "maxNumParticles", target, "particles.maxParticles");
                }
            }
        }
        private void Scalar(Record record, string field, string target, string property)
        {
            if (float.TryParse(record.Field(field), NumberStyles.Float, Invariant, out float value) && !float.IsNaN(value) && !float.IsInfinity(value)) Add(target, property, value);
        }
        private void Color(Record record, string field, string target, string property)
        {
            string raw = record.Field(field);
            foreach (string channel in new[] { "r", "g", "b", "a" })
            {
                Match match = Regex.Match(raw ?? "", @"\b" + channel + @": ([^,}]+)");
                string scalar = record.Overrides.TryGetValue(field + "." + channel, out string changed) ? changed : match.Groups[1].Value;
                if (float.TryParse(scalar, NumberStyles.Float, Invariant, out float value)) Add(target, property + "." + channel, value);
            }
        }
        private void Particle(Record record, string field, string target, string property)
        {
            Match match = Regex.Match(record.Raw, @"(?m)^    " + field + @":\r?\n(?:      [^\r\n]*\r?\n)*?      scalar: ([^\r\n]+)");
            if (match.Success && float.TryParse(match.Groups[1].Value, NumberStyles.Float, Invariant, out float value)) Add(target, property, value);
        }
        private void Add(string target, string property, float value)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                Note("Non-finite historical value omitted: " + target + " / " + property);
                return;
            }
            result.Values.Add(new Entry { Target = target, Property = property, Value = value });
        }

        private void ExportMaterial(Record renderer, string target)
        {
            string materialReference = renderer.Overrides.TryGetValue("m_Materials.Array.data[0]", out string changed) ? changed :
                Regex.Match(renderer.Raw, @"(?m)^  m_Materials:\r?\n  - ([^\r\n]+)").Groups[1].Value;
            string materialPath = GuidPath(materialReference);
            if (materialPath == null)
            {
                string packageMaterial = materialReference.Contains("a97c105638bdf8b4a8650670310a4cd3") ? "Sprite-Lit-Default" :
                    materialReference.Contains("9dfc825aed78fcd4ba02077103263b40") ? "Sprite-Unlit-Default" : null;
                if (packageMaterial != null)
                {
                    Text("Packages/packages-lock.json");
                    result.MaterialSources.Add(new MaterialSource { Target = target,
                        AssetPath = "Packages/com.unity.render-pipelines.universal/Runtime/Materials/" + packageMaterial + ".mat",
                        ShaderName = "Universal Render Pipeline/2D/" + packageMaterial });
                    Note("Builtin URP material identity is resolved from the historical renderer GUID and the Git-pinned packages-lock.json; no scalar values are borrowed from the installed package.");
                }
                else Note("Material outside tracked Git assets: " + target);
                return;
            }
            string raw = Text(materialPath);
            string shaderPath = GuidPath(Regex.Match(raw, @"(?m)^  m_Shader: ([^\r\n]+)").Groups[1].Value);
            string shaderName = shaderPath != null && shaderPath.EndsWith(".shader") ? Regex.Match(Text(shaderPath), "Shader\\s+\"([^\"]+)\"").Groups[1].Value : "";
            result.MaterialSources.Add(new MaterialSource { Target = target, AssetPath = materialPath, ShaderName = shaderName });
            foreach (Match match in Regex.Matches(raw, @"(?m)^    - (_[^:]+): ([^\r\n]+)$"))
            {
                string name = match.Groups[1].Value;
                string value = match.Groups[2].Value;
                if (float.TryParse(value, NumberStyles.Float, Invariant, out float scalar)) Add(target, "shader." + name, scalar);
                else if (value.Contains("r:")) Color(new Record { Raw = "  value: " + value }, "value", target, "shader." + name);
            }
        }

        private void ExportConfig(string path)
        {
            var config = Parse(Text(path)).First();
            string[] scalars = { "ShotHaloDuration", "ShotHaloAlpha", "ShotHaloScale", "ProjectileMuzzleDuration", "ProjectileImpactDuration", "ProjectileImpactSize", "RingLineWidth", "RingLineAlpha", "RingUpgradeBrightness", "RingUpgradeGlow", "RingHighlightBrightness", "RingDimmedAlpha" };
            foreach (string field in scalars) Scalar(config, field, "config:orbital", "config." + field);
            foreach (string section in new[] { "PistolProjectile", "ShotgunProjectile", "SniperProjectile", "RingSurface", "TierI", "TierII", "TierIII", "TierIV" })
            {
                string raw = Regex.Match(config.Raw, @"(?ms)^  " + section + @":\r?\n(.*?)(?=^  \S|\z)").Groups[1].Value;
                var values = new Record { Raw = raw };
                foreach (Match match in Regex.Matches(raw, @"(?m)^    (\w+): ([^\r\n]+)$"))
                {
                    string field = match.Groups[1].Value;
                    string property = "config." + section + "." + field;
                    if (match.Groups[2].Value.Contains("r:")) Color(values, field, "config:orbital", property);
                    else Scalar(values, field, "config:orbital", property);
                }
            }
        }

        private void ExportRecognizedCodeValues()
        {
            const string path = "Assets/_Project/scripts/Combat/OrbitalStation/OrbitalStationRuntime.cs";
            string code = Text(path);
            if (code.Contains("flashes.Add((line, Time.time + life));"))
            {
                Add("config:orbital", "config.FlashTailFade", 0f);
                Note("config.FlashTailFade=0 is an adapter for the recognized historical FlashLink implementation: constant alpha until expiration.");
            }
            if (code.Contains("FlashLink(point - side * size, point + side * size, color, life);"))
            {
                Add("config:orbital", "config.ProjectileImpactWhitening", 0f);
                Note("config.ProjectileImpactWhitening=0 is an adapter for the recognized historical impact code using the unwhitened weapon color.");
            }
            if (code.Contains(".025f * Core.Level + .2f * energy"))
            {
                Note("Core halo alpha coefficients and neutral RGB gain are extracted from a recognized historical C# expression; they do not change charge gameplay.");
            }
            if (code.Contains("public void FlashLink(Vector2 from, Vector2 to, Color color, float life)"))
            {
                foreach (string family in new[] { "Other", "MuzzlePistol", "MuzzleShotgun", "MuzzleSniper", "Impact", "Arc", "Relocation", "ImpactPistol", "ImpactShotgun", "ImpactSniper" })
                {
                    string prefix = "flash." + family + ".";
                    foreach (string channel in new[] { "r", "g", "b", "a" }) Add("config:orbital", prefix + "Tint." + channel, 1f);
                    Add("config:orbital", prefix + "TintAmount", 0f);
                    foreach (string field in new[] { "Brightness", "Opacity", "Duration", "Width" }) Add("config:orbital", prefix + field, 1f);
                }
                Note("Flash family entries are explicit neutral adapters (white tint, no tint mix, unit gain/opacity/duration/width) for historical FlashLink arguments, not serialized asset literals. They disable Custom family overrides when comparing Original.");
            }
        }
    }
}
#endif
