using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UICardHoverAnimation : MonoBehaviour,
    IPointerEnterHandler,
    IPointerExitHandler
{
    [Header("References")]
    [SerializeField] private RectTransform target;
    [SerializeField] private Button button;
    [SerializeField] private Graphic accentGraphic;

    [Header("Hover")]
    [SerializeField, Min(1f)] private float scaleMultiplier = 1.055f;
    [SerializeField] private float verticalOffset = 10f;
    [SerializeField, Min(0f)] private float hoverDuration = 0.13f;
    [SerializeField, Min(0f)] private float returnDuration = 0.11f;
    [SerializeField, Min(1f)] private float accentAlphaMultiplier = 2f;

    private Vector3 restingScale;
    private Vector2 restingPosition;
    private Color restingAccentColor;
    private Coroutine animationRoutine;
    private bool initialized;
    private bool hovered;

    private void Awake()
    {
        ResolveReferences();
        CaptureRestingState();
    }

    private void OnEnable()
    {
        ResolveReferences();

        if (!initialized)
            CaptureRestingState();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!isActiveAndEnabled || button == null || !button.interactable)
            return;

        hovered = true;
        StartAnimation(
            restingScale * scaleMultiplier,
            restingPosition + Vector2.up * verticalOffset,
            GetHoverAccentColor(),
            hoverDuration
        );
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (!hovered)
            return;

        hovered = false;
        StartAnimation(
            restingScale,
            restingPosition,
            restingAccentColor,
            returnDuration
        );
    }

    public void RefreshRestingState()
    {
        if (hovered)
            return;

        CaptureRestingState();
    }

    public void SetRestingScale(Vector3 scale)
    {
        restingScale = scale;
        initialized = true;

        if (target == null)
            return;

        if (hovered)
        {
            StartAnimation(
                restingScale * scaleMultiplier,
                restingPosition + Vector2.up * verticalOffset,
                GetHoverAccentColor(),
                hoverDuration
            );
        }
        else
        {
            target.localScale = restingScale;
        }
    }

    public void SetRestingAccentColor(Color color)
    {
        restingAccentColor = color;
        initialized = true;

        if (accentGraphic == null)
            return;

        if (hovered)
        {
            StartAnimation(
                restingScale * scaleMultiplier,
                restingPosition + Vector2.up * verticalOffset,
                GetHoverAccentColor(),
                hoverDuration
            );
        }
        else
        {
            accentGraphic.color = restingAccentColor;
        }
    }

    private void ResolveReferences()
    {
        if (target == null)
            target = transform as RectTransform;

        if (button == null)
            button = GetComponent<Button>();
    }

    private void CaptureRestingState()
    {
        if (target == null)
            return;

        restingScale = target.localScale;
        restingPosition = target.anchoredPosition;

        if (accentGraphic != null)
            restingAccentColor = accentGraphic.color;

        initialized = true;
    }

    private Color GetHoverAccentColor()
    {
        Color color = restingAccentColor;
        color.a = Mathf.Clamp01(color.a * accentAlphaMultiplier);
        return color;
    }

    private void StartAnimation(
        Vector3 targetScale,
        Vector2 targetPosition,
        Color targetAccentColor,
        float duration)
    {
        if (target == null)
            return;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(Animate(
            targetScale,
            targetPosition,
            targetAccentColor,
            duration
        ));
    }

    private IEnumerator Animate(
        Vector3 targetScale,
        Vector2 targetPosition,
        Color targetAccentColor,
        float duration)
    {
        Vector3 startScale = target.localScale;
        Vector2 startPosition = target.anchoredPosition;
        Color startAccentColor = accentGraphic != null
            ? accentGraphic.color
            : targetAccentColor;

        if (duration <= 0f)
        {
            ApplyState(targetScale, targetPosition, targetAccentColor);
            animationRoutine = null;
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = 1f - Mathf.Pow(1f - t, 3f);

            ApplyState(
                Vector3.LerpUnclamped(startScale, targetScale, eased),
                Vector2.LerpUnclamped(startPosition, targetPosition, eased),
                Color.LerpUnclamped(startAccentColor, targetAccentColor, eased)
            );

            yield return null;
        }

        ApplyState(targetScale, targetPosition, targetAccentColor);
        animationRoutine = null;
    }

    private void ApplyState(Vector3 scale, Vector2 position, Color accentColor)
    {
        if (target != null)
        {
            target.localScale = scale;
            target.anchoredPosition = position;
        }

        if (accentGraphic != null)
            accentGraphic.color = accentColor;
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        hovered = false;

        if (initialized)
            ApplyState(restingScale, restingPosition, restingAccentColor);
    }
}
