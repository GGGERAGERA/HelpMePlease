using System.Collections;
using UnityEngine;

public sealed class BunkerObservationWatcher : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private SpriteRenderer silhouette;
    [SerializeField] private Transform player;

    [Header("Timing")]
    [SerializeField] private Vector2 appearanceInterval = new(8f, 20f);
    [SerializeField] private Vector2 watchDuration = new(1f, 2f);
    [SerializeField, Min(0.05f)] private float fadeDuration = 0.45f;

    [Header("Window Bounds")]
    [SerializeField] private Vector2 horizontalBounds = new(-5.5f, 5.5f);
    [SerializeField, Min(0.1f)] private float crossingDuration = 4.5f;
    [SerializeField, Min(0f)] private float trackingResponsiveness = 0.55f;

    [Header("Appearance")]
    [SerializeField, Range(0f, 1f)] private float visibleAlpha = 0.58f;
    [SerializeField, Range(0.5f, 1f)] private float distantScale = 0.78f;

    private Coroutine watchRoutine;
    private Vector3 baseLocalPosition;
    private Vector3 baseLocalScale;

    private void Awake()
    {
        baseLocalPosition = transform.localPosition;
        baseLocalScale = transform.localScale;
        SetAlpha(0f);
    }

    private void OnEnable()
    {
        if (silhouette != null)
            watchRoutine = StartCoroutine(WatchLoop());
    }

    private void OnDisable()
    {
        if (watchRoutine != null)
            StopCoroutine(watchRoutine);

        watchRoutine = null;
        ResetVisual();
    }

    private IEnumerator WatchLoop()
    {
        yield return null;

        while (true)
        {
            yield return new WaitForSeconds(RandomInRange(appearanceInterval));

            switch (Random.Range(0, 3))
            {
                case 0:
                    yield return WalkAcrossWindow();
                    break;

                case 1:
                    yield return ApproachAndWatch();
                    break;

                default:
                    yield return BriefGlimpse();
                    break;
            }

            ResetVisual();
        }
    }

    private IEnumerator WalkAcrossWindow()
    {
        bool leftToRight = Random.value >= 0.5f;
        float startX = leftToRight ? horizontalBounds.x : horizontalBounds.y;
        float endX = leftToRight ? horizontalBounds.y : horizontalBounds.x;
        SetLocalX(startX);
        transform.localScale = baseLocalScale * Random.Range(0.84f, 0.94f);

        yield return Fade(0f, visibleAlpha * 0.72f, fadeDuration);

        float elapsed = 0f;
        float duration = crossingDuration * Random.Range(0.85f, 1.2f);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            SetLocalX(Mathf.Lerp(startX, endX, easedT));
            yield return null;
        }

        yield return Fade(CurrentAlpha(), 0f, fadeDuration);
    }

    private IEnumerator ApproachAndWatch()
    {
        SetLocalX(Random.Range(horizontalBounds.x, horizontalBounds.y));
        Vector3 nearScale = baseLocalScale;
        Vector3 farScale = baseLocalScale * distantScale;
        transform.localScale = farScale;

        yield return FadeAndScale(
            0f,
            visibleAlpha,
            farScale,
            nearScale,
            fadeDuration * 1.35f);

        float elapsed = 0f;
        float duration = RandomInRange(watchDuration);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;

            if (player != null && transform.parent != null)
            {
                float playerLocalX =
                    transform.parent.InverseTransformPoint(player.position).x;
                float targetX = Mathf.Clamp(
                    playerLocalX,
                    horizontalBounds.x,
                    horizontalBounds.y);
                Vector3 position = transform.localPosition;
                position.x = Mathf.Lerp(
                    position.x,
                    targetX,
                    Mathf.Clamp01(Time.deltaTime * trackingResponsiveness));
                transform.localPosition = position;
            }

            yield return null;
        }

        yield return FadeAndScale(
            CurrentAlpha(),
            0f,
            nearScale,
            farScale,
            fadeDuration * 1.2f);
    }

    private IEnumerator BriefGlimpse()
    {
        SetLocalX(Random.Range(horizontalBounds.x, horizontalBounds.y));
        transform.localScale = baseLocalScale * Random.Range(0.82f, 0.92f);

        yield return Fade(0f, visibleAlpha * 0.62f, fadeDuration * 0.65f);
        yield return new WaitForSeconds(Random.Range(0.45f, 0.9f));
        yield return Fade(CurrentAlpha(), 0f, fadeDuration * 0.8f);
    }

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t * t * (3f - 2f * t)));
            yield return null;
        }

        SetAlpha(to);
    }

    private IEnumerator FadeAndScale(
        float fromAlpha,
        float toAlpha,
        Vector3 fromScale,
        Vector3 toScale,
        float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float easedT = t * t * (3f - 2f * t);
            SetAlpha(Mathf.Lerp(fromAlpha, toAlpha, easedT));
            transform.localScale = Vector3.Lerp(fromScale, toScale, easedT);
            yield return null;
        }

        SetAlpha(toAlpha);
        transform.localScale = toScale;
    }

    private void ResetVisual()
    {
        if (silhouette == null)
            return;

        baseLocalPosition.x = Mathf.Clamp(
            baseLocalPosition.x,
            horizontalBounds.x,
            horizontalBounds.y);
        transform.localPosition = baseLocalPosition;
        transform.localScale = baseLocalScale;
        SetAlpha(0f);
    }

    private void SetLocalX(float x)
    {
        Vector3 position = transform.localPosition;
        position.x = Mathf.Clamp(x, horizontalBounds.x, horizontalBounds.y);
        transform.localPosition = position;
    }

    private void SetAlpha(float alpha)
    {
        if (silhouette == null)
            return;

        Color color = silhouette.color;
        color.a = Mathf.Clamp01(alpha);
        silhouette.color = color;
    }

    private float CurrentAlpha()
    {
        return silhouette != null ? silhouette.color.a : 0f;
    }

    private static float RandomInRange(Vector2 range)
    {
        return Random.Range(Mathf.Min(range.x, range.y), Mathf.Max(range.x, range.y));
    }
}
