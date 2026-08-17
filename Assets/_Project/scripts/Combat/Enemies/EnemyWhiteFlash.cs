using System.Collections;
using UnityEngine;

public class EnemyWhiteFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = 0.08f;

    [Header("Target")]
    [SerializeField] private SpriteRenderer targetRenderer;

    private Material originalMaterial;
    private Coroutine flashCoroutine;
    private WaitForSeconds flashWait;

    public SpriteRenderer TargetRenderer => targetRenderer;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
    public float FlashDuration => flashDuration;

    public void SetFlashDuration(float value)
    {
        flashDuration = Mathf.Max(0f, value);
        flashWait = new WaitForSeconds(flashDuration);

        if (flashDuration <= 0f && flashCoroutine != null)
        {
            StopCoroutine(flashCoroutine);
            flashCoroutine = null;
            if (targetRenderer != null)
                targetRenderer.sharedMaterial = originalMaterial;
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

        flashWait = new WaitForSeconds(flashDuration);
    }

    public void Flash()
    {
        if (targetRenderer == null)
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

        yield return flashWait;

        targetRenderer.sharedMaterial = originalMaterial;

        flashCoroutine = null;
    }
}
