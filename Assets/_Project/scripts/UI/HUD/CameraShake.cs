using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalLocalPosition;
    private Coroutine shakeRoutine;

    private void Awake()
    {
        Instance = this;
        originalLocalPosition = transform.localPosition;
    }

    public void Shake(float duration, float magnitude)
    {
        if (shakeRoutine != null)
            StopCoroutine(shakeRoutine);

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    private IEnumerator ShakeRoutine(float duration, float magnitude)
    {
        Vector3 basePosition = transform.localPosition;

        float elapsed = 0f;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            float damping = 1f - progress;

            float x = Mathf.PerlinNoise(Time.time * 25f, 0f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0f, Time.time * 25f) * 2f - 1f;

            Vector3 offset = new Vector3(x, y, 0f) * magnitude * damping;

            transform.localPosition = basePosition + offset;

            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.localPosition = basePosition;
        shakeRoutine = null;
    }
    public void StopAllShakes()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }
    }
}