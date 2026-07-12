using System.Collections;
using UnityEngine;

public class PanelAppearAnimation : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private RectTransform window;
    [SerializeField] private float duration = 0.15f;

    private void Awake()
    {
        if (canvasGroup == null)
            canvasGroup = GetComponent<CanvasGroup>();

        if (window == null)
            window = GetComponent<RectTransform>();
    }

    private void OnEnable()
    {
        StopAllCoroutines();
        StartCoroutine(AnimateIn());
    }

    private IEnumerator AnimateIn()
    {
        float time = 0f;

        canvasGroup.alpha = 0f;
        window.localScale = Vector3.one * 0.92f;

        while (time < duration)
        {
            time += Time.unscaledDeltaTime;
            float t = time / duration;

            canvasGroup.alpha = Mathf.Lerp(0f, 1f, t);
            window.localScale = Vector3.Lerp(Vector3.one * 0.92f, Vector3.one, t);

            yield return null;
        }

        canvasGroup.alpha = 1f;
        window.localScale = Vector3.one;
    }
}