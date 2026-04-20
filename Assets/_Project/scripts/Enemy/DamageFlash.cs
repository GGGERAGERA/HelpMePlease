using UnityEngine;
using System.Collections;

public class DamageFlash : MonoBehaviour
{
    public float flashDuration = 0.1f;  // длительность красного цвета
    public Color flashColor = Color.red; // цвет при получении урона

    private SpriteRenderer[] spriteRenderers;
    private Color[] originalColors;

    void Start()
    {
        // Находим все SpriteRenderer на этом объекте и на всех дочерних
        spriteRenderers = GetComponentsInChildren<SpriteRenderer>();
        originalColors = new Color[spriteRenderers.Length];
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            originalColors[i] = spriteRenderers[i].color;
        }

        // Подписываемся на событие получения урона
        EnemyHealth health = GetComponent<EnemyHealth>();
        if (health != null)
            health.OnDamageTaken.AddListener(Flash);
        else
            Debug.LogError("DamageFlash: EnemyHealth not found!");
    }

    public void Flash()
    {
        StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        // Устанавливаем красный цвет для всех частей
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = flashColor;
        }

        // Ждём указанное время
        yield return new WaitForSeconds(flashDuration);

        // Возвращаем исходные цвета
        for (int i = 0; i < spriteRenderers.Length; i++)
        {
            if (spriteRenderers[i] != null)
                spriteRenderers[i].color = originalColors[i];
        }
    }
}