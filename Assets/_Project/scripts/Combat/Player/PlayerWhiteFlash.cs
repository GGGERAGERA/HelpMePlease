using System.Collections;
using UnityEngine;

public class PlayerWhiteFlash : MonoBehaviour
{
    [Header("Flash Settings")]
    [SerializeField] private Material flashMaterial;
    [SerializeField] private float flashDuration = .14f;
    [SerializeField] private Color flashTint = new(1f, .26f, .18f, 1f);

    private SpriteRenderer[] spriteRenderers;
    private Material[] originalMaterials;
    private Coroutine flashCoroutine;
    private Material damageMaterial;
    private static readonly int FlashColor = Shader.PropertyToID("_FlashColor");

    private void Awake()
    {
        spriteRenderers = System.Array.FindAll(GetComponentsInChildren<SpriteRenderer>(true),
            sprite => sprite.name.IndexOf("shadow", System.StringComparison.OrdinalIgnoreCase) < 0);
        originalMaterials = new Material[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
            originalMaterials[i] = spriteRenderers[i].sharedMaterial;
    }

    public void Flash()
    {
        if (flashMaterial == null)
        {
            Debug.LogWarning("PlayerWhiteFlash: Flash Material not assigned.");
            return;
        }
        if (damageMaterial == null)
        {
            // One owned material per player; never tint the shared enemy flash.
            damageMaterial = new Material(flashMaterial) { name = "Player damage flash", hideFlags = HideFlags.DontSave };
            damageMaterial.SetColor(FlashColor, flashTint);
        }
        if (flashCoroutine != null)
            StopCoroutine(flashCoroutine);
        else
            for (int i = 0; i < spriteRenderers.Length; i++)
                if (spriteRenderers[i] != null) originalMaterials[i] = spriteRenderers[i].sharedMaterial;
        flashCoroutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null) spriteRenderers[i].sharedMaterial = damageMaterial;
        yield return new WaitForSecondsRealtime(flashDuration);
        RestoreMaterials();
        flashCoroutine = null;
    }

    private void RestoreMaterials()
    {
        for (int i = 0; i < spriteRenderers.Length; i++)
            if (spriteRenderers[i] != null) spriteRenderers[i].sharedMaterial = originalMaterials[i];
    }

    private void OnDisable()
    {
        if (flashCoroutine == null) return;
        StopCoroutine(flashCoroutine);
        flashCoroutine = null;
        RestoreMaterials();
    }

    private void OnDestroy()
    {
        if (damageMaterial != null) Destroy(damageMaterial);
    }
}
