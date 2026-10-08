using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class SceneTransitionView : MonoBehaviour
{
    [SerializeField] private Canvas canvas;
    [SerializeField] private CanvasGroup group;
    [SerializeField] private Image scan;
    [SerializeField] private TextMeshProUGUI errorText;
    public float Alpha => group.alpha;
    public void SetAlpha(float alpha) => group.alpha = alpha;
    public void SetVisible(bool visible) { canvas.enabled = visible; group.blocksRaycasts = visible; }
    public void SetFailure(bool failed) => errorText.gameObject.SetActive(failed);
    public void SetFailureText(string text) { if (errorText.text != text) errorText.text = text; }
    public void AnimateScan(bool returning)
    {
        float phase = Mathf.Repeat(Time.unscaledTime * .24f, 1f);
        float x = returning ? 1f - phase : phase;
        scan.rectTransform.anchorMin = new Vector2(x, 0f);
        scan.rectTransform.anchorMax = new Vector2(x, 1f);
        scan.color = new Color(.12f, .72f, .9f, .06f + .04f * Mathf.Sin(Time.unscaledTime * 3f));
    }
}
