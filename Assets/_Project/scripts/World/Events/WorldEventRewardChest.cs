using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public sealed class WorldEventRewardChest : Interactable
{
    [Header("Appearance")]
    [SerializeField, Min(0f)] private float appearDuration = 0.28f;
    [SerializeField, Range(0.1f, 1f)] private float appearStartScale = 0.72f;
    [SerializeField, Min(0f)] private float pulseDuration = 0.16f;
    [SerializeField, Min(1f)] private float pulseScale = 1.12f;

    [Header("Opening")]
    [SerializeField, Min(0f)] private float openFeedbackDuration = 0.12f;

    private bool opened;
    private bool improved;
    private bool numericOnly;
    private bool appearanceComplete;
    private Collider2D interactionCollider;
    private SpriteRenderer visualRenderer;
    private Vector3 restingScale;
    private Color restingColor;
    private Coroutine animationRoutine;

    public override bool CanInteract => appearanceComplete && !opened;

    private void Awake()
    {
        interactionCollider = GetComponent<Collider2D>();
        interactionCollider.isTrigger = true;
        visualRenderer = GetComponent<SpriteRenderer>();
        restingScale = transform.localScale;
        restingColor = visualRenderer != null
            ? visualRenderer.color
            : Color.white;
    }

    private void OnEnable()
    {
        opened = false;
        appearanceComplete = false;

        if (interactionCollider != null)
            interactionCollider.enabled = true;

        animationRoutine = StartCoroutine(PlayAppearance());
    }

    public void Initialize(
        bool isImproved,
        DoubleOrLeave rewardChoice,
        bool forceNumeric = false)
    {
        improved = isImproved;
        numericOnly = forceNumeric;
    }

    public override void Interact()
    {
        if (!CanInteract)
            return;

        UpgradeManager upgradeManager = UpgradeManager.Instance;

        if (upgradeManager == null)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogWarning(
                "[WorldEventRewardChest] UpgradeManager is not available."
            );
#endif
            Destroy(gameObject);
            return;
        }

        opened = true;
        appearanceComplete = false;

        if (interactionCollider != null)
            interactionCollider.enabled = false;

        if (animationRoutine != null)
            StopCoroutine(animationRoutine);

        animationRoutine = StartCoroutine(PlayOpenFeedback(upgradeManager));
    }

    private IEnumerator PlayAppearance()
    {
        SetVisual(appearStartScale, 0f);
        yield return AnimateVisual(
            appearStartScale,
            1f,
            0f,
            1f,
            appearDuration
        );
        yield return AnimateVisual(
            1f,
            pulseScale,
            1f,
            1f,
            pulseDuration * 0.45f
        );
        yield return AnimateVisual(
            pulseScale,
            1f,
            1f,
            1f,
            pulseDuration * 0.55f
        );

        SetVisual(1f, 1f);
        appearanceComplete = true;
        animationRoutine = null;
    }

    private IEnumerator PlayOpenFeedback(UpgradeManager upgradeManager)
    {
        yield return AnimateVisual(
            1f,
            pulseScale,
            1f,
            1f,
            openFeedbackDuration * 0.45f
        );
        yield return AnimateVisual(
            pulseScale,
            1f,
            1f,
            1f,
            openFeedbackDuration * 0.55f
        );

        animationRoutine = null;

        if (upgradeManager == null)
        {
            Destroy(gameObject);
            yield break;
        }

        if (numericOnly)
        {
            upgradeManager.ShowNumericChestRewardChoices(
                choiceCount: 3,
                onClosed: HandleRewardClosed
            );
        }
        else
        {
            upgradeManager.ShowChestRewardChoices(
                choiceCount: 3,
                guaranteeBehavior: improved,
                onClosed: HandleRewardClosed
            );
        }
    }

    private void HandleRewardClosed()
    {
        if (this != null)
            Destroy(gameObject);
    }

    private IEnumerator AnimateVisual(
        float fromScale,
        float toScale,
        float fromAlpha,
        float toAlpha,
        float duration)
    {
        if (duration <= 0f)
        {
            SetVisual(toScale, toAlpha);
            yield break;
        }

        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float eased = t * t * (3f - 2f * t);
            SetVisual(
                Mathf.LerpUnclamped(fromScale, toScale, eased),
                Mathf.LerpUnclamped(fromAlpha, toAlpha, eased)
            );
            yield return null;
        }

        SetVisual(toScale, toAlpha);
    }

    private void SetVisual(float scaleMultiplier, float alpha)
    {
        transform.localScale = restingScale * scaleMultiplier;

        if (visualRenderer == null)
            return;

        Color color = restingColor;
        color.a *= alpha;
        visualRenderer.color = color;
    }

    private void OnDisable()
    {
        if (animationRoutine != null)
        {
            StopCoroutine(animationRoutine);
            animationRoutine = null;
        }

        appearanceComplete = false;
        transform.localScale = restingScale;

        if (visualRenderer != null)
            visualRenderer.color = restingColor;
    }
}
