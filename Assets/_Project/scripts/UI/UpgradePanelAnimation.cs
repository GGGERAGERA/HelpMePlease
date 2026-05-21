using System.Collections;
using UnityEngine;

public class UpgradePanelAnimation : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform panel;

    [SerializeField] private float duration = 0.18f;

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(Animate());
    }

    private IEnumerator Animate()
    {
        float time = 0f;

        canvasGroup.alpha = 0f;
        panel.localScale = Vector3.one * 0.8f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;

            float t = time / duration;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            panel.localScale = Vector3.Lerp(
                Vector3.one * 0.8f,
                Vector3.one,
                t
            );

            yield return null;
        }

        canvasGroup.alpha = 1f;
        panel.localScale = Vector3.one;
    }
}