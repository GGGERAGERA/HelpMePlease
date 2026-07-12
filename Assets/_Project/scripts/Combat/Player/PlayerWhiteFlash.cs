using System.Collections;
using UnityEngine;

public class PlayerWhiteFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = 0.08f;

    private SpriteRenderer[] spriteRenderers;
    private Material[] originalMaterials;
    private Coroutine flashCoroutine;

    private void Awake()
    {
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>(true);
        originalMaterials = new Material[spriteRenderers.Length];

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalMaterials[i] = spriteRenderers[i].material;
        }
    }

    public void Flash()
    {
        if (flashMaterial == null)
        {
            Debug.LogWarning("PlayerWhiteFlash: Flash Material не назначен.");
            return;
        }

        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);

        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].material = flashMaterial;
        }

        yield return new WaitForSeconds(flashDuration);

        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].material = originalMaterials[i];
        }

        flashCoroutine = null;
    }
}