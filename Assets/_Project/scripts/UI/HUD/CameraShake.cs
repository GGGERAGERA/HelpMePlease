using System.Collections;
using UnityEngine;

public class CameraShake : MonoBehaviour
{
    public static CameraShake Instance { get; private set; }

    private Vector3 originalLocalPosition;
    private Coroutine shakeRoutine;
    private Coroutine worldEventStartRoutine;
    private Camera targetCamera;
    private float originalOrthographicSize;

    private void Awake()
    {
        Instance = this;
        originalLocalPosition = transform.localPosition;
        targetCamera = GetComponent<Camera>();

        if (targetCamera != null)
            originalOrthographicSize = targetCamera.orthographicSize;
    }

    public void Shake(float duration, float magnitude)
    {
        StopAllShakes();

        shakeRoutine = StartCoroutine(ShakeRoutine(duration, magnitude));
    }

    public void PlayWorldEventStartPulse(float duration)
    {
        StopAllShakes();
        worldEventStartRoutine = StartCoroutine(
            WorldEventStartPulseRoutine(
                Mathf.Clamp(duration, 0.5f, 0.8f)
            )
        );
    }

    public void StopWorldEventStartPulse()
    {
        if (worldEventStartRoutine == null)
            return;

        StopCoroutine(worldEventStartRoutine);
        worldEventStartRoutine = null;
        RestoreCameraState();
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

    private IEnumerator WorldEventStartPulseRoutine(float duration)
    {
        Vector3 basePosition = transform.localPosition;
        float baseSize = targetCamera != null
            ? targetCamera.orthographicSize
            : 0f;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float normalized = Mathf.Clamp01(elapsed / duration);
            float pulse = Mathf.Sin(normalized * Mathf.PI);
            float noiseTime = Time.unscaledTime * 28f;
            float x = Mathf.PerlinNoise(noiseTime, 0f) * 2f - 1f;
            float y = Mathf.PerlinNoise(0f, noiseTime) * 2f - 1f;
            transform.localPosition = basePosition +
                new Vector3(x, y, 0f) * (0.045f * pulse);

            if (targetCamera != null && targetCamera.orthographic)
                targetCamera.orthographicSize = baseSize - 0.22f * pulse;

            yield return null;
        }

        transform.localPosition = basePosition;

        if (targetCamera != null && targetCamera.orthographic)
            targetCamera.orthographicSize = baseSize;

        worldEventStartRoutine = null;
    }
    public void StopAllShakes()
    {
        if (shakeRoutine != null)
        {
            StopCoroutine(shakeRoutine);
            shakeRoutine = null;
        }

        if (worldEventStartRoutine != null)
        {
            StopCoroutine(worldEventStartRoutine);
            worldEventStartRoutine = null;
        }

        RestoreCameraState();
    }

    private void RestoreCameraState()
    {
        transform.localPosition = originalLocalPosition;

        if (targetCamera != null && targetCamera.orthographic)
            targetCamera.orthographicSize = originalOrthographicSize;
    }

    private void OnDisable()
    {
        StopAllShakes();

        if (Instance == this)
            Instance = null;
    }
}
