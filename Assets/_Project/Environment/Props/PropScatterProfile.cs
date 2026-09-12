using System;
using UnityEngine;

[CreateAssetMenu(menuName = "Subject42/Environment/Prop Scatter Profile")]
public sealed class PropScatterProfile : ScriptableObject
{
    [Serializable]
    public sealed class Entry
    {
        public GameObject prefab;
        [Min(0)] public float weight = 1;
        [Min(0)] public int minCount = 8;
        [Min(0)] public int maxCount = 40;
        public bool varyScale = true;
        public Vector2 scaleRange = new(.92f, 1.06f);
        public bool varyRotation = true;
        public Vector2 rotationRange = new(-8, 8);
    }
    public Entry[] entries = Array.Empty<Entry>();
    public int seed = 42073;
    [Tooltip("Minima first, then weighted extras. Safety clearances always take priority.")]
    [Min(0)] public int totalCount = 238;
    [Min(.1f)] public float minDistance = 3.5f;
    [Min(0)] public float edgeClearance = 1;
    [Min(0)] public float wallClearance = .6f;
    [Min(0)] public float playerClearance = 3;
    [Min(0)] public float exitClearance = 3;
    [Min(0)] public float interactableClearance = 2.5f;
}
