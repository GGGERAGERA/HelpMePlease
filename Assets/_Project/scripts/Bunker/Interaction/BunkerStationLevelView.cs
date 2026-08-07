using System.Collections;
using TMPro;
using UnityEngine;

public sealed class BunkerStationLevelView : MonoBehaviour
{
    [SerializeField] private BunkerStationId stationId;
    [SerializeField] private Vector3 indicatorOffset = new(0f, 2f, 0f);

    private TextMeshPro levelText;
    private TextMeshPro feedbackText;
    private Coroutine feedbackRoutine;

    private void Awake()
    {
        EnsureView();
        Refresh();
    }

    private void OnEnable()
    {
        BindProgression();
        Refresh();
    }

    private void Start()
    {
        // BunkerContext may initialize after prefab OnEnable.
        BindProgression();
        Refresh();
    }

    private void OnDisable()
    {
        UnbindProgression();
        if (feedbackRoutine != null)
        {
            StopCoroutine(feedbackRoutine);
            feedbackRoutine = null;
        }
    }

    private void BindProgression()
    {
        if (BunkerStationProgressionService.Instance == null)
            return;

        BunkerStationProgressionService.Instance.StationLevelChanged -= HandleLevelChanged;
        BunkerStationProgressionService.Instance.StationLevelChanged += HandleLevelChanged;
    }

    private void UnbindProgression()
    {
        if (BunkerStationProgressionService.Instance != null)
            BunkerStationProgressionService.Instance.StationLevelChanged -= HandleLevelChanged;
    }

    private void HandleLevelChanged(BunkerStationId changedStationId, int level)
    {
        if (changedStationId != stationId)
            return;

        Refresh();
        if (feedbackRoutine != null)
            StopCoroutine(feedbackRoutine);
        feedbackRoutine = StartCoroutine(PlayLevelUpFeedback(level));
    }

    private void Refresh()
    {
        EnsureView();
        int level = BunkerStationProgressionService.GetStoredLevel(stationId);
        levelText.text = $"LV.{level}";
        levelText.color = level switch
        {
            3 => new Color(0.55f, 1f, 1f, 1f),
            2 => new Color(0.35f, 0.9f, 0.94f, 1f),
            _ => new Color(0.26f, 0.72f, 0.76f, 1f)
        };
    }

    private IEnumerator PlayLevelUpFeedback(int level)
    {
        feedbackText.text = $"STATION LV.{level}";
        feedbackText.gameObject.SetActive(true);
        Vector3 baseScale = Vector3.one * 0.22f;
        float elapsed = 0f;
        const float duration = 1f;

        while (elapsed < duration)
        {
            elapsed += Time.unscaledDeltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float pulse = 1f + Mathf.Sin(t * Mathf.PI) * 0.28f;
            levelText.transform.localScale = baseScale * pulse;
            feedbackText.color = new Color(0.35f, 0.95f, 1f, 1f - t);
            feedbackText.transform.localPosition =
                indicatorOffset + Vector3.up * Mathf.Lerp(0.52f, 0.8f, t);
            yield return null;
        }

        levelText.transform.localScale = baseScale;
        feedbackText.gameObject.SetActive(false);
        feedbackRoutine = null;
    }

    private void EnsureView()
    {
        if (levelText == null)
            levelText = CreateWorldText("StationLevelIndicator", indicatorOffset, 4f, 0.22f);

        if (feedbackText == null)
        {
            feedbackText = CreateWorldText(
                "StationLevelUpFeedback",
                indicatorOffset + Vector3.up * 0.52f,
                3.2f,
                0.22f);
            feedbackText.gameObject.SetActive(false);
        }
    }

    private TextMeshPro CreateWorldText(string objectName, Vector3 localPosition, float fontSize, float scale)
    {
        GameObject textObject = new(objectName, typeof(TextMeshPro));
        textObject.transform.SetParent(transform, false);
        textObject.transform.localPosition = localPosition;
        textObject.transform.localScale = Vector3.one * scale;

        TextMeshPro text = textObject.GetComponent<TextMeshPro>();
        text.font = TMP_Settings.defaultFontAsset;
        text.fontSize = fontSize;
        text.alignment = TextAlignmentOptions.Center;
        text.rectTransform.sizeDelta = new Vector2(5f, 1.2f);
        text.raycastTarget = false;
        text.GetComponent<MeshRenderer>().sortingOrder = 80;
        return text;
    }
}
