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

    private void Awake()
    {
        if (targetRenderer == null)
            targetRenderer = GetComponentInChildren<SpriteRenderer>();

        if (targetRenderer != null)
            originalMaterial = targetRenderer.material;
    }

    public void Flash()
    {
        if (targetRenderer == null)
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
        targetRenderer.material = flashMaterial;

        yield return new WaitForSeconds(flashDuration);

        targetRenderer.material = originalMaterial;

        flashCoroutine = null;
    }
}