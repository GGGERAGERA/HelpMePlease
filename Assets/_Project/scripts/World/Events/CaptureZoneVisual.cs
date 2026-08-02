using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(CaptureZoneEvent))]
public sealed class CaptureZoneVisual : MonoBehaviour
{
    private static readonly int EdgeWidthId =
        Shader.PropertyToID("_EdgeWidth");
    private static readonly int ProgressId =
        Shader.PropertyToID("_Progress");
    private static readonly int CompletionFlashId =
        Shader.PropertyToID("_CompletionFlash");
    private static readonly int FadeId =
        Shader.PropertyToID("_Fade");

    [Header("Material")]
    [SerializeField] private Material visualMaterial;

    [Header("Capture Zone Visual")]
    [SerializeField, Min(1f)] private float visualRadiusMultiplier = 1.18f;
    [SerializeField, Range(0.01f, 0.3f)] private float edgeWidth = 0.075f;
    [SerializeField, Range(0f, 3f)] private float completionFlash = 1.15f;
    [SerializeField, Min(0.01f)] private float fadeDuration = 0.45f;
    [SerializeField, Min(0.01f)] private float completionFadeDuration = 0.4f;

    private CaptureZoneEvent captureZoneEvent;
    private GameObject visualObject;
    private MeshRenderer visualRenderer;
    private Mesh visualMesh;
    private MaterialPropertyBlock visualProperties;
    private float visualFade;
    private bool detachedForCompletion;

    private void Awake()
    {
        captureZoneEvent = GetComponent<CaptureZoneEvent>();
        BuildVisual();
    }

    private void OnEnable()
    {
        visualFade = 0f;
        ApplyVisualProperties();
    }

    private void Update()
    {
        if (visualRenderer == null || captureZoneEvent == null)
            return;

        float targetFade = captureZoneEvent.IsStarted &&
            !captureZoneEvent.IsCompleted
            ? 1f
            : 0f;
        visualFade = Mathf.MoveTowards(
            visualFade,
            targetFade,
            Time.unscaledDeltaTime / Mathf.Max(0.01f, fadeDuration)
        );
        ApplyVisualProperties();
    }

    public void PlayCompletion()
    {
        if (visualObject == null || visualRenderer == null ||
            detachedForCompletion)
        {
            return;
        }

        detachedForCompletion = true;
        visualProperties.SetFloat(ProgressId, 1f);
        visualProperties.SetFloat(FadeId, 1f);
        visualRenderer.SetPropertyBlock(visualProperties);
        visualObject.transform.SetParent(null, true);

        CaptureZoneCompletionVisual completion =
            visualObject.AddComponent<CaptureZoneCompletionVisual>();
        completion.Initialize(
            visualRenderer,
            visualMesh,
            visualProperties,
            completionFlash,
            completionFadeDuration
        );

        visualObject = null;
        visualRenderer = null;
        visualMesh = null;
        visualProperties = null;
    }

    private void BuildVisual()
    {
        if (captureZoneEvent == null || visualMaterial == null)
            return;

        visualObject = new GameObject("CaptureZoneVisual");
        visualObject.transform.SetParent(transform, false);

        visualMesh = CreateQuad();
        MeshFilter meshFilter = visualObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = visualMesh;

        visualRenderer = visualObject.AddComponent<MeshRenderer>();
        visualRenderer.sharedMaterial = visualMaterial;
        visualRenderer.shadowCastingMode = ShadowCastingMode.Off;
        visualRenderer.receiveShadows = false;
        visualRenderer.sortingLayerName = "Midground";
        visualRenderer.sortingOrder = -1;

        float diameter =
            captureZoneEvent.CaptureRadius *
            2f *
            Mathf.Max(1f, visualRadiusMultiplier);
        Vector3 parentScale = transform.lossyScale;
        visualObject.transform.localScale = new Vector3(
            diameter * SafeInverse(parentScale.x),
            diameter * SafeInverse(parentScale.y),
            1f
        );

        visualProperties = new MaterialPropertyBlock();
        ApplyVisualProperties();
    }

    private void ApplyVisualProperties()
    {
        if (visualRenderer == null || visualProperties == null)
            return;

        visualProperties.SetFloat(EdgeWidthId, edgeWidth);
        visualProperties.SetFloat(
            ProgressId,
            captureZoneEvent != null ? captureZoneEvent.Progress : 0f
        );
        visualProperties.SetFloat(CompletionFlashId, 0f);
        visualProperties.SetFloat(FadeId, visualFade);
        visualRenderer.SetPropertyBlock(visualProperties);
    }

    private static Mesh CreateQuad()
    {
        Mesh mesh = new Mesh
        {
            name = "CaptureZoneVisualQuad"
        };
        mesh.vertices = new[]
        {
            new Vector3(-0.5f, -0.5f, 0f),
            new Vector3(-0.5f, 0.5f, 0f),
            new Vector3(0.5f, 0.5f, 0f),
            new Vector3(0.5f, -0.5f, 0f)
        };
        mesh.uv = new[]
        {
            new Vector2(0f, 0f),
            new Vector2(0f, 1f),
            new Vector2(1f, 1f),
            new Vector2(1f, 0f)
        };
        mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        mesh.RecalculateBounds();
        return mesh;
    }

    private static float SafeInverse(float value)
    {
        return Mathf.Abs(value) > 0.0001f ? 1f / value : 1f;
    }

    private void OnDestroy()
    {
        if (!detachedForCompletion && visualMesh != null)
            Destroy(visualMesh);
    }
}

public sealed class CaptureZoneCompletionVisual : MonoBehaviour
{
    private static readonly int CompletionFlashId =
        Shader.PropertyToID("_CompletionFlash");
    private static readonly int FadeId =
        Shader.PropertyToID("_Fade");

    private MeshRenderer visualRenderer;
    private Mesh visualMesh;
    private MaterialPropertyBlock visualProperties;
    private float completionFlash;
    private float fadeDuration;

    public void Initialize(
        MeshRenderer renderer,
        Mesh mesh,
        MaterialPropertyBlock properties,
        float flash,
        float duration)
    {
        visualRenderer = renderer;
        visualMesh = mesh;
        visualProperties = properties ?? new MaterialPropertyBlock();
        completionFlash = Mathf.Max(0f, flash);
        fadeDuration = Mathf.Max(0.01f, duration);
        StartCoroutine(Play());
    }

    private IEnumerator Play()
    {
        float elapsed = 0f;

        while (elapsed < fadeDuration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / fadeDuration);
            float flashEnvelope = 1f - Mathf.Abs(t * 2f - 1f);
            float fade = 1f - t * t;

            if (visualRenderer != null)
            {
                visualProperties.SetFloat(
                    CompletionFlashId,
                    completionFlash * flashEnvelope
                );
                visualProperties.SetFloat(FadeId, fade);
                visualRenderer.SetPropertyBlock(visualProperties);
            }

            yield return null;
        }

        Destroy(gameObject);
    }

    private void OnDestroy()
    {
        if (visualMesh != null)
            Destroy(visualMesh);
    }
}
