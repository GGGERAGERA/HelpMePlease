using System.Collections;
using UnityEngine;

public class EnemyWhiteFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = 0.08f;
    [SerializeField, Range(0f, 1f)] private float flashBlend = .55f;

    [Header("Target")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private Material originalMaterial;
    private Coroutine flashCoroutine;
    private WaitForSecondsRealtime flashWait;
    private MaterialPropertyBlock flashProperties;
    private static readonly int FlashBlendId = Shader.PropertyToID("_FlashBlend");

    public SpriteRenderer TargetRenderer => targetRenderer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float FlashDuration => flashDuration;

    public void SetFlashDuration(float value)
    {
        flashDuration = Mathf.Max(0f, value);
        flashWait = new WaitForSecondsRealtime(flashDuration);

        if (flashDuration <= 0f && flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            Restore();
        }
    }
#endif

    public void SetRuntimeBaseMaterial(Material material)
    {
        originalMaterial = material;

        if (targetRenderer != null && flashCoroutine == null)
            targetRenderer.sharedMaterial = material;
    }

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();

        if (targetRenderer != null)
            originalMaterial = targetRenderer.sharedMaterial;

        flashWait = new WaitForSecondsRealtime(flashDuration);
        flashProperties = new MaterialPropertyBlock();
    }

    private void OnDisable()
    {
        if (flashCoroutine == null)
            return;

        // A pooled despawn can interrupt the coroutine before material restoration.
        StopCoroutine(flashCoroutine);
        flashCoroutine = null;
        Restore();
    }

    public void Flash()
    {
        if (targetRenderer == null || !isActiveAndEnabled || !gameObject.activeInHierarchy)
            return;

        if (flashDuration <= 0f)
            return;

        if (flashMaterial == null)
        {
            Debug.LogWarning("EnemyWhiteFlash: Flash Material not assigned.");
            return;
        }

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        targetRenderer.sharedMaterial = flashMaterial;
        SetFlashBlend(Mathf.Clamp01(flashBlend));
        flashWait.Reset();
        yield return flashWait;
        Restore();

        flashCoroutine = null;
    }

    private void SetFlashBlend(float value)
    {
        if (targetRenderer == null) return;
        flashProperties ??= new MaterialPropertyBlock();
        // Merge current properties so concurrent tint/outline owners keep their values.
        targetRenderer.GetPropertyBlock(flashProperties);
        flashProperties.SetFloat(FlashBlendId, value);
        targetRenderer.SetPropertyBlock(flashProperties);
    }

    private void Restore()
    {
        if (targetRenderer == null) return;
        SetFlashBlend(1f);
        targetRenderer.sharedMaterial = originalMaterial;
    }
}
