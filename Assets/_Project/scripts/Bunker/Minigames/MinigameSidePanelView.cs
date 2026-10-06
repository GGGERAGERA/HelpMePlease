using TMPro;
using UnityEngine;

/// <summary>Authored presentation only; callers provide already localized content.</summary>
[DisallowMultipleComponent]
public sealed class MinigameSidePanelView : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text scoreLabel;
    [SerializeField] private TMP_Text bestLabel;
    [SerializeField] private TMP_Text timeLabel;
    [SerializeField] private TMP_Text timeText;
    [SerializeField] private TMP_Text scoreText;
    [SerializeField] private TMP_Text bestScoreText;
    [SerializeField] private TMP_Text statusText;
    [SerializeField] private TMP_Text controlsText;
    [SerializeField] private GameObject extraSection;
    [SerializeField] private TMP_Text legendText;
    [SerializeField] private TMP_Text detailsText;

    public void SetContent(string title, string score, string best, string time, string controls)
    {
        SetText(titleText, title);
        SetText(scoreLabel, score);
        SetText(bestLabel, best);
        SetText(timeLabel, time);
        SetText(controlsText, controls);
    }

    public void SetValues(string time, string score, string best)
    {
        SetText(timeText, time);
        SetText(scoreText, score);
        SetText(bestScoreText, best);
    }

    public void SetStatus(string status) => SetText(statusText, status);

    public void SetExtraSection(string legend, string details, bool visible)
    {
        SetText(legendText, legend);
        SetText(detailsText, details);
        if (extraSection != null) extraSection.SetActive(visible);
    }

    public void SetVisible(bool visible)
    {
        if (gameObject.activeSelf != visible) gameObject.SetActive(visible);
    }

    private static void SetText(TMP_Text target, string value)
    {
        if (target != null && target.text != value) target.text = value;
    }
}
