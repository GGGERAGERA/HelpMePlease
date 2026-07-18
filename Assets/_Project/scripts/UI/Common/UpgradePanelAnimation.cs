using System.Collections;
using UnityEngine;

public class UpgradePanelAnimation : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;
    [SerializeField] private CanvasGroup dimmerCanvasGroup;
    [SerializeField] private float duration = 0.13f;
    [SerializeField] private float startScale = 0.94f;
    [SerializeField, Range(0f, 1f)] private float dimmerTargetAlpha = 0.65f;

    public void PlayShow()
    {
        StopAllCoroutines();

        SetAnimationStartState();
        StartCoroutine(Animate());
    }

    private void SetAnimationStartState()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;
        }

        if (panel != null)
            panel.localScale = Vector3.one * startScale;

        if (dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.alpha = 0f;
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = true;
        }
    }

    private IEnumerator Animate()
    {
        float animationDuration = Mathf.Max(0f, duration);

        if (animationDuration <= 0f)
        {
            CompleteAnimation();
            yield break;
        }

        float time = 0f;

        while (time < animationDuration)
        {
            time += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(time / animationDuration);
            float eased = t * t * (3f - 2f * t);

            if (canvasGroup != null)
                canvasGroup.alpha = eased;

            if (panel != null)
            {
                panel.localScale = Vector3.LerpUnclamped(
                    Vector3.one * startScale,
                    Vector3.one,
                    eased
                );
            }

            if (dimmerCanvasGroup != null)
                dimmerCanvasGroup.alpha = dimmerTargetAlpha * eased;

            yield return null;
        }

        CompleteAnimation();
    }

    private void CompleteAnimation()
    {
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        if (panel != null)
            panel.localScale = Vector3.one;

        if (dimmerCanvasGroup != null)
        {
            dimmerCanvasGroup.alpha = dimmerTargetAlpha;
            dimmerCanvasGroup.interactable = false;
            dimmerCanvasGroup.blocksRaycasts = true;
        }
    }
}
