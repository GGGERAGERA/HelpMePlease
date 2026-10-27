using System;
using System.Collections.Generic;

namespace Subject42.Visuals
{
    public enum VisualComparisonMode { Original, Current, Custom }

    [Serializable]
    public sealed class VisualOverride
    {
        public string Scope, Target, Property;
        public float Value;
        public VisualOverride Copy() => new() { Scope = Scope, Target = Target, Property = Property, Value = Value };
    }

    [Serializable]
    public sealed class VisualMaterialSource
    {
        public string Target, AssetPath, ShaderName;
    }

    [Serializable]
    public sealed class VisualProfile
    {
        public int Version = 1;
        public string Name = "Custom", BaselineRevision;
        public List<VisualOverride> Values = new();
        public List<VisualMaterialSource> MaterialSources = new();
        public List<string> Notes = new();

        public void Validate()
        {
            if (Version != 1 || Values == null || Values.Count > 12000)
                throw new ArgumentException("Неподдерживаемая версия или размер визуального профиля.");
            var keys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var value in Values)
            {
                if (value == null) throw new ArgumentException("Пустое значение профиля.");
                ValidateKey(value.Scope, value.Target, value.Property, value.Value);
                if (!keys.Add(value.Scope + "\n" + value.Target + "\n" + value.Property))
                    throw new ArgumentException("Повторяющийся параметр: " + value.Property);
            }
        }

        private static void ValidateKey(string scope, string target, string property, float value)
        {
            if (scope != "Group" && scope != "Type" && scope != "Instance")
                throw new ArgumentException("Неизвестная область: " + scope);
            if (string.IsNullOrWhiteSpace(target) || target.Length > 2048 ||
                string.IsNullOrWhiteSpace(property) || property.Length > 256 ||
                float.IsNaN(value) || float.IsInfinity(value) || Math.Abs(value) > 100000)
                throw new ArgumentException("Недопустимый визуальный параметр.");
        }

        public void Set(string scope, string target, string property, float value)
        {
            ValidateKey(scope, target, property, value);
            foreach (var entry in Values)
                if (entry.Scope == scope && entry.Target == target && entry.Property == property)
                { entry.Value = value; return; }
            if (Values.Count >= 12000) throw new ArgumentException("Профиль содержит слишком много параметров.");
            Values.Add(new VisualOverride { Scope = scope, Target = target, Property = property, Value = value });
        }

        public bool TryGet(string scope, string target, string property, out float value)
        {
            for (int i = Values.Count - 1; i >= 0; i--)
            {
                var entry = Values[i];
                if (entry.Scope == scope && entry.Target == target && entry.Property == property)
                { value = entry.Value; return true; }
            }
            value = 0; return false;
        }

        public bool TryResolve(string group, string type, string instance, string property, out float value) =>
            TryGet("Instance", instance, property, out value) || TryGet("Type", type, property, out value) ||
            TryGet("Group", group, property, out value);

        public float Resolve(string group, string type, string instance, string property, float fallback) =>
            TryResolve(group, type, instance, property, out float value) ? value : fallback;

        public VisualProfile Copy()
        {
            var copy = new VisualProfile { Version = Version, Name = Name, BaselineRevision = BaselineRevision };
            foreach (var value in Values) copy.Values.Add(value.Copy());
            copy.MaterialSources.AddRange(MaterialSources ?? new List<VisualMaterialSource>());
            copy.Notes.AddRange(Notes ?? new List<string>());
            return copy;
        }

        public VisualProfile ForProduction()
        {
            var copy = Copy(); copy.Values.RemoveAll(v => v.Scope == "Instance"); return copy;
        }
    }
}
