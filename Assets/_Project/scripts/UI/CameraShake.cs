using UnityEngine;
using System.Collections;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance;

    private Coroutine shakeCoroutine;
    private bool isGameOver = false;

    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);
    }

    public void Shake(float duration, float magnitude)
    {
        if (isGameOver) return; // не трясём, если игра окончена


        if (shakeCoroutine != null)
            StopCoroutine(shakeCoroutine);
        shakeCoroutine = StartCoroutine(DoShake(duration, magnitude));
    }

    public void StopAllShakes()
    {
        isGameOver = true;
        if (shakeCoroutine != null)
        {
            StopCoroutine(shakeCoroutine);
            shakeCoroutine = null;
        }
        // Возвращаем камеру на место
        transform.position = new Vector3(transform.position.x, transform.position.y, -10f);
    }

    public void ResetShake()
    {
        isGameOver = false;
    }

    IEnumerator DoShake(float duration, float magnitude)
    {
        Vector3 originalPosition = transform.position; // ← берём текущую позицию камеры

        float elapsed = 0f;
        while (elapsed < duration && !isGameOver)
        {
            float x = Random.Range(-1f, 1f) * magnitude;
            float y = Random.Range(-1f, 1f) * magnitude;
            transform.position = originalPosition + new Vector3(x, y, 0);
            elapsed += Time.deltaTime;
            yield return null;
        }
        transform.position = originalPosition; // возврат к позиции, которая была до тряски
        shakeCoroutine = null;
    }
}