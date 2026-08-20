using System.Collections.Generic;
using UnityEngine;

public sealed class LaserBeamRenderer : MonoBehaviour
{
    private sealed class BeamPair
    {
        public LineRenderer Core;
        public LineRenderer Glow;
        public float ReleaseAt;
        public bool Active;
    }

    [Header("Core")]
    [SerializeField] private Material coreMaterial;
    [SerializeField] private Color coreColor = Color.white;
    [SerializeField] private float coreWidth = 0.035f;

    [Header("Glow")]
    [SerializeField] private Material glowMaterial;
    [SerializeField] private Color glowColor = new Color(0.1f, 0.85f, 1f, 0.45f);
    [SerializeField] private float glowWidth = 0.16f;

    [Header("Lifetime")]
    [SerializeField] private float beamDuration = 0.1f;

    [Header("Feel")]
    [SerializeField] private float endJitter = 0.035f;
    [SerializeField] private float widthJitter = 0.01f;

    [Header("Sorting")]
    [SerializeField] private string sortingLayerName = "Effects";
    [SerializeField] private int glowSortingOrder = 20;
    [SerializeField] private int coreSortingOrder = 21;
    [SerializeField, Min(0)] private int prewarmPairs = 4;

    private readonly List<BeamPair> beamPairs = new();

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float DebugLastWidthScale { get; private set; } = 1f;
    public float DebugLastCoreWidth { get; private set; }
    public float DebugLastGlowWidth { get; private set; }
#endif

    private void Awake()
    {
        for (int i = 0; i < prewarmPairs; i++)
            beamPairs.Add(CreatePair());
    }

    private void Update()
    {
        float now = Time.time;

        for (int i = 0; i < beamPairs.Count; i++)
        {
            BeamPair pair = beamPairs[i];
            if (!pair.Active || now < pair.ReleaseAt)
                continue;

            pair.Active = false;
            pair.Core.gameObject.SetActive(false);
            pair.Glow.gameObject.SetActive(false);
        }
    }

    public void Render(Vector2 start, Vector2 end, float widthScale = 1f)
    {
        Vector2 jitteredEnd = end + Random.insideUnitCircle * endJitter;
        widthScale = Mathf.Max(0.1f, widthScale);

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLastWidthScale = widthScale;
#endif

        BeamPair pair = AcquirePair();
        ConfigureGlow(pair.Glow, start, jitteredEnd, widthScale);
        ConfigureCore(pair.Core, start, jitteredEnd, widthScale);
        pair.ReleaseAt = Time.time + Mathf.Max(0f, beamDuration);
        pair.Active = true;
    }

    private void ConfigureCore(
        LineRenderer line,
        Vector2 start,
        Vector2 end,
        float widthScale)
    {

        float width = Mathf.Max(
            0.01f,
            (coreWidth + Random.Range(-widthJitter, widthJitter)) * widthScale
        );

        line.startWidth = width;
        line.endWidth = width;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLastCoreWidth = width;
#endif

        line.startColor = coreColor;
        line.endColor = coreColor;

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private void ConfigureGlow(
        LineRenderer line,
        Vector2 start,
        Vector2 end,
        float widthScale)
    {
        float width = Mathf.Max(
            0.01f,
            (glowWidth + Random.Range(-widthJitter, widthJitter)) * widthScale
        );

        line.startWidth = width;
        line.endWidth = width;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        DebugLastGlowWidth = width;
#endif

        line.startColor = glowColor;
        line.endColor = new Color(
            glowColor.r,
            glowColor.g,
            glowColor.b,
            0f
        );

        line.SetPosition(0, start);
        line.SetPosition(1, end);
    }

    private BeamPair AcquirePair()
    {
        for (int i = 0; i < beamPairs.Count; i++)
        {
            if (beamPairs[i].Active)
                continue;

            Activate(beamPairs[i]);
            return beamPairs[i];
        }

        BeamPair created = CreatePair();
        beamPairs.Add(created);
        Activate(created);
        return created;
    }

    private static void Activate(BeamPair pair)
    {
        pair.Core.gameObject.SetActive(true);
        pair.Glow.gameObject.SetActive(true);
    }

    private BeamPair CreatePair()
    {
        BeamPair pair = new()
        {
            Core = CreateLine(
                "LaserBeam_Core",
                coreMaterial,
                coreSortingOrder),
            Glow = CreateLine(
                "LaserBeam_Glow",
                glowMaterial,
                glowSortingOrder)
        };
        pair.Core.gameObject.SetActive(false);
        pair.Glow.gameObject.SetActive(false);
        return pair;
    }

    private LineRenderer CreateLine(string objectName, Material material, int sortingOrder)
    {
        GameObject beamObject = new GameObject(objectName);
        beamObject.transform.SetParent(transform, false);
        LineRenderer line = beamObject.AddComponent<LineRenderer>();

        line.positionCount = 2;
        line.useWorldSpace = true;

        line.sharedMaterial = material;
        line.sortingLayerName = sortingLayerName;
        line.sortingOrder = sortingOrder;

        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.textureMode = LineTextureMode.Stretch;
        line.alignment = LineAlignment.View;

        return line;
    }
}
