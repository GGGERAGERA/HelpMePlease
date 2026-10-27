using System;
using System.Collections.Generic;
using UnityEngine;

namespace Subject42.Visuals
{
    public sealed class VisualProperty
    {
        public readonly string Id, Label, Category;
        public readonly Func<float> Read;
        public readonly Action<float> Write;
        public readonly float Min, Max;
        public float Current { get; private set; }
        public bool Touched;
        public bool IsInteger;
        public string Note;
        public Func<bool> Available;
        public Action Restore;
        public void RestoreCurrent() { if (Restore != null) Restore(); else Write(Current); }
        internal void CaptureConfiguredCurrent() => Current = Read();
        public VisualProperty(string id, string label, string category, Func<float> read,
            Action<float> write, float min = 0, float max = 4)
        {
            Id = id; Label = label; Category = category; Read = read; Write = write;
            Min = min; Max = max; Current = read();
        }
        public float Sanitize(float value)
        {
            value = Mathf.Clamp(value, Min, Max);
            return IsInteger ? Mathf.Round(value) : value;
        }
    }

    public sealed class VisualTarget
    {
        public readonly string Key, TypeKey, Group, Label;
        public readonly Component Component;
        public readonly List<VisualProperty> Properties = new();
        public readonly List<string> Unsupported = new();
        public Action<VisualComparisonMode> SetComparison;
        public Action Release;
        public Action Maintain;
        public Func<bool> Alive;
        public bool IsAlive => Alive?.Invoke() ?? Component != null;
        public bool IsActive => Component == null || Component.gameObject.activeInHierarchy;
        public VisualTarget(string key, string typeKey, string group, string label, Component component)
        { Key = key; TypeKey = typeKey; Group = group; Label = label; Component = component; }
    }

    public static class VisualProperties
    {
        public static void Color(List<VisualProperty> properties, string id, string label, string category,
            Func<Color> read, Action<Color> write, float max = 8, bool includeAlpha = true)
        {
            string[] channels = { "r", "g", "b", "a" };
            for (int i = 0; i < (includeAlpha ? 4 : 3); i++)
            {
                int channel = i;
                properties.Add(new VisualProperty(id + "." + channels[i], label + " " + channels[i].ToUpperInvariant(), category,
                    () => read()[channel], value => { var color = read(); color[channel] = value; write(color); }, 0, i == 3 ? 1 : max));
            }
        }
    }

    [Serializable] public sealed class LiveVisualSource { public GameObject Prefab; public string AssetPath; }
    [Serializable] public sealed class LiveVisualMaterial { public Material Material; public string AssetPath; }
}
